// Runs steps with their events, from `stepStarted` to `stepPassed` or
// `stepFailed`, and `call` steps with the steps of their flow nested under them
// (docs/actions.md, "Flows"; docs/protocol.md, `stepId`). A flow gets its own
// variables, its parameters (defaults, then `with`) and its own targets; the
// names it lists in `outputs` are copied back into the caller's variables when
// it ends. Its first failing step skips the rest and fails the call with
// `FlowFailed`. A `call` step has no timeout of its own: each step of the flow
// has its own. Used for the steps of tests and of login flows.

import type { ErrorInfo, Location } from '@cfe/protocol';
import type { ActionRegistry } from '../actions/registry.js';
import type { EnvironmentProfile } from '../context/environment.js';
import { interpolate } from '../context/interpolate.js';
import { VariableStore } from '../context/variables.js';
import { targetLookup, type TargetLookup } from '../locate/locate.js';
import { durationToMs } from '../schema/common.js';
import type { FlowFile } from '../schema/files.js';
import type { TargetValue } from '../schema/targets.js';
import type { Secrets } from '../sdk/context.js';
import type { DataRow } from '../stepfile/data-rows.js';
import type { SourceFile } from '../stepfile/source.js';
import type { NormalizedStep } from '../stepfile/steps.js';
import { StepError, toErrorInfo } from './errors.js';
import type { PageSet } from './pages.js';
import type { ResponseLog } from './responses.js';
import { executeStep, type StepResult } from './step.js';
import type { EmitEvent } from './test-run.js';

/** The section a step belongs to; a flow's steps belong to their call's section. */
export type Section = 'before' | 'steps' | 'after';

/** A flow file, read and validated. */
export interface LoadedFlow {
  /** Its data. */
  readonly data: FlowFile;
  /** Its steps, in the canonical long form. */
  readonly steps: readonly NormalizedStep[];
  /** Its source, for step locations. */
  readonly source: SourceFile;
}

/** The file a step is written in and the values its `${…}` can read. */
export interface StepScope {
  /** Project-relative path of the file. */
  readonly file: string;
  /** The file's source, for step locations. */
  readonly source: SourceFile;
  /** Variables of the test, or of the flow. */
  readonly vars: VariableStore;
  /** Named targets of the file, then the shared ones. */
  readonly targets: TargetLookup;
  /** The data row, in a test with `data`. */
  readonly row?: DataRow | undefined;
  /** The flow's parameters, in a flow. */
  readonly flowParams?: Readonly<Record<string, unknown>> | undefined;
  /** The flows being called, outermost first, to stop a cycle. */
  readonly chain: readonly string[];
}

/** Where a step runs: its id, section, time limit and cancellation. */
export interface StepPlace {
  /** The step's id, such as `steps.4` or `steps.4/steps.1`. */
  readonly stepId: string;
  /** The id of the `call` step whose flow the step belongs to. */
  readonly parentStepId?: string | undefined;
  /** The section, for events and for the page replaced in `after` steps. */
  readonly section: Section;
  /** A time (on the `performance.now()` clock) no step may run past. */
  readonly limitAt?: number | undefined;
  /** Aborted when the run is cancelled; absent for steps that are not cancelled. */
  readonly cancel?: AbortSignal | undefined;
}

/** What every step of one test instance (or login flow) shares. */
export interface StepRunnerOptions {
  /** Every action. */
  readonly registry: ActionRegistry;
  /** The selected environment. */
  readonly profile: EnvironmentProfile;
  /** The declared secrets. */
  readonly secrets: Secrets;
  /** The attribute `testId` candidates match. */
  readonly testIdAttribute: string;
  /** The project's shared targets, for the targets of flows. */
  readonly sharedTargets: ReadonlyMap<string, TargetValue>;
  /** The test's pages. */
  readonly pages: PageSet;
  /** The test's response log. */
  readonly responses?: ResponseLog | undefined;
  /** The test instance, for events. */
  readonly testId: string;
  /** The project's root folder. */
  readonly root: string;
  /** Reads a flow file; undefined when it cannot be read or is invalid. */
  readonly readFlow: (file: string) => LoadedFlow | undefined;
  /** Registers a value found while a step runs as a secret (ADR 0014). */
  readonly registerSecret: (value: string) => boolean;
  /** Sends events. */
  readonly emit: EmitEvent;
}

/**
 * The label of a step in results: its `name`, or the action and its main
 * value (the URL, the target's name, the flow, or its first parameter).
 *
 * @param step - A step in its canonical long form.
 * @returns Such as `click checkout.submit` or `goto /todos`.
 */
export function stepTitle(step: NormalizedStep): string {
  if (step.name !== undefined) return step.name;
  const main =
    step.params.url ?? step.params.target ?? step.params.flow ?? Object.values(step.params)[0];
  return typeof main === 'string' || typeof main === 'number'
    ? `${step.action} ${String(main)}`
    : step.action;
}

/**
 * Runs the steps of one test instance, or of one login flow.
 *
 * @example
 * ```ts
 * const runner = new StepRunner(options);
 * const result = await runner.run(step, scope, { stepId: 'steps.0', section: 'steps' });
 * ```
 */
export class StepRunner {
  private readonly options: StepRunnerOptions;
  // When the previous step started: the response log is read from then on.
  private previousStepStart = performance.now();

  /**
   * @param options - What every step shares.
   */
  constructor(options: StepRunnerOptions) {
    this.options = options;
  }

  /**
   * Runs one step and sends its events; a `call` step runs its flow's steps
   * under it.
   *
   * @param step - The step in its canonical long form.
   * @param scope - Its file and the values its `${…}` can read.
   * @param place - Its id, section, time limit and cancellation.
   * @returns How the step ended; never throws for a failing step.
   */
  async run(step: NormalizedStep, scope: StepScope, place: StepPlace): Promise<StepResult> {
    const { emit, testId } = this.options;
    const at = scope.source.positionOf(step.path);
    const location = { file: scope.file, line: at.line, column: at.column };
    emit('stepStarted', {
      testId,
      stepId: place.stepId,
      ...(place.parentStepId === undefined ? {} : { parentStepId: place.parentStepId }),
      section: place.section,
      action: step.action,
      params: step.params,
      page: step.page ?? 'main',
      title: stepTitle(step),
      location,
    });
    const result =
      step.action === 'call'
        ? await this.call(step, scope, place, location)
        : await this.execute(step, scope, place, location);
    const ended = {
      testId,
      stepId: place.stepId,
      durationMs: result.durationMs,
      locators: result.locators,
      snapshot: { state: 'skipped' },
    };
    if (result.outcome === 'passed') emit('stepPassed', ended);
    else emit('stepFailed', { ...ended, error: result.error });
    return result;
  }

  private async execute(
    step: NormalizedStep,
    scope: StepScope,
    place: StepPlace,
    location: Location,
  ): Promise<StepResult> {
    const options = this.options;
    const stepStart = performance.now();
    const result = await executeStep(step, {
      registry: options.registry,
      profile: options.profile,
      secrets: options.secrets,
      testIdAttribute: options.testIdAttribute,
      vars: scope.vars,
      targets: scope.targets,
      row: scope.row,
      flowParams: scope.flowParams,
      pages: options.pages,
      section: place.section,
      testId: options.testId,
      stepId: place.stepId,
      location,
      root: options.root,
      registerSecret: options.registerSecret,
      responses: options.responses,
      responsesSince: this.previousStepStart,
      timeoutMs: this.timeoutOf(step, place),
      cancel: place.cancel,
      emit: options.emit,
    });
    this.previousStepStart = stepStart;
    return result;
  }

  /** The step's own timeout, cut to the place's limit. */
  private timeoutOf(step: NormalizedStep, place: StepPlace): number {
    const own = durationToMs(step.timeout ?? this.options.profile.settings.timeout);
    return place.limitAt === undefined
      ? own
      : Math.max(1, Math.min(own, place.limitAt - performance.now()));
  }

  private async call(
    step: NormalizedStep,
    scope: StepScope,
    place: StepPlace,
    location: Location,
  ): Promise<StepResult> {
    const start = performance.now();
    const ended = (outcome: StepResult['outcome'], error?: ErrorInfo): StepResult => ({
      outcome,
      ...(error === undefined ? {} : { error }),
      locators: [],
      durationMs: Math.round(performance.now() - start),
    });
    let file: string;
    let flow: LoadedFlow;
    let flowScope: StepScope;
    try {
      ({ file, flow, flowScope } = this.enter(step, scope, place));
    } catch (error) {
      return ended('failed', toErrorInfo(error, location));
    }

    let failed: { stepId: string; error: ErrorInfo | undefined } | undefined;
    let cancelled = false;
    for (const [index, inner] of flow.steps.entries()) {
      const stepId = `${place.stepId}/steps.${String(index)}`;
      if (failed === undefined && !cancelled && place.cancel?.aborted === true) cancelled = true;
      if (failed !== undefined || cancelled) {
        this.options.emit('stepSkipped', {
          testId: this.options.testId,
          stepId,
          reason: cancelled ? 'cancelled' : 'previousFailure',
          message: cancelled
            ? 'skipped: the run was cancelled'
            : `skipped: ${failed?.stepId ?? ''} failed`,
        });
        continue;
      }
      const result = await this.run(inner, flowScope, {
        ...place,
        stepId,
        parentStepId: place.stepId,
      });
      if (result.outcome === 'cancelled') cancelled = true;
      if (result.outcome === 'failed') failed = { stepId, error: result.error };
    }

    // The outputs a flow set are copied back even when it failed, for `after` steps.
    for (const name of flow.data.outputs ?? []) {
      if (flowScope.vars.has(name)) {
        scope.vars.set(name, flowScope.vars.get(name));
      } else if (failed === undefined && !cancelled) {
        this.options.emit('log', {
          level: 'warn',
          code: 'FlowOutputNotSet',
          message: `The flow "${file}" finished without setting its output "${name}".`,
          testId: this.options.testId,
          stepId: place.stepId,
          location,
        });
      }
    }
    if (cancelled) return ended('cancelled', toErrorInfo(undefined, location, 'cancelled'));
    if (failed !== undefined) {
      const inner = failed.error;
      const where =
        inner?.location === undefined
          ? ''
          : ` (${inner.location.file}:${String(inner.location.line)}:${String(inner.location.column)})`;
      return ended('failed', {
        code: 'FlowFailed',
        message: `The flow "${file}" failed at ${failed.stepId}${where}: ${inner?.message ?? 'unknown error'}`,
        location,
      });
    }
    return ended('passed');
  }

  /** Reads the flow a `call` step names and builds its scope; throws a StepError when it cannot. */
  private enter(
    step: NormalizedStep,
    scope: StepScope,
    place: StepPlace,
  ): { file: string; flow: LoadedFlow; flowScope: StepScope } {
    const options = this.options;
    const params = interpolate(step.params, {
      vars: scope.vars,
      env: options.profile,
      secrets: options.secrets,
      row: scope.row,
      params: scope.flowParams,
      step: `${place.stepId} (call)`,
    }) as Record<string, unknown>;
    const spec = options.registry.get('call')?.spec;
    const checked = spec?.params.safeParse(params);
    if (checked?.success === false) {
      throw new StepError(
        'InvalidParameters',
        `After its \${…} values were filled in, the parameters of "call" are invalid: ${checked.error.issues
          .map((issue) => `${issue.path.join('.') || 'parameters'}: ${issue.message}`)
          .join('; ')}.`,
      );
    }
    const file = String(params.flow);
    const given = (params.with ?? {}) as Record<string, unknown>;
    if (scope.chain.includes(file)) {
      throw new StepError('FlowCycle', `Flow calls itself: ${[...scope.chain, file].join(' → ')}.`);
    }
    const flow = options.readFlow(file);
    if (flow === undefined) {
      throw new StepError(
        'FlowNotFound',
        `The flow "${file}" cannot be read as a valid flow file.`,
        'Check the path (relative to the project root) and validate the flow.',
      );
    }
    const flowParams: Record<string, unknown> = {};
    for (const [name, param] of Object.entries(flow.data.params ?? {})) {
      const value = name in given ? given[name] : param.default;
      if (value === undefined) {
        throw new StepError(
          'MissingParameter',
          `The flow "${file}" needs the parameter "${name}".`,
        );
      }
      if (typeof value !== param.type) {
        throw new StepError(
          'InvalidParameterType',
          `The flow parameter "${name}" must be a ${param.type}, but its value is a ${typeof value}.`,
        );
      }
      flowParams[name] = value;
    }
    for (const name of Object.keys(given)) {
      if (!(name in flowParams)) {
        throw new StepError('UnknownParameter', `The flow "${file}" has no parameter "${name}".`);
      }
    }
    return {
      file,
      flow,
      flowScope: {
        file,
        source: flow.source,
        vars: new VariableStore(),
        targets: targetLookup(flow.data.targets, options.sharedTargets),
        flowParams,
        chain: [...scope.chain, file],
      },
    };
  }
}
