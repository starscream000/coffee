// Runs one test instance (docs/step-format.md, "Execution rules";
// docs/architecture.md, "Lifecycle of a run"): a fresh browser context with
// the environment's settings, `before` then `steps` until the first failure,
// the rest skipped, and `after` steps always, each on its own. Every step has a
// timeout; when it passes, the step's signal is aborted and the step fails with
// ActionTimeout.

import type { LocatorUse } from '@cfe/protocol';
import type { Browser, BrowserContext } from 'playwright';
import type { ActionRegistry } from '../actions/registry.js';
import type { EnvironmentProfile } from '../context/environment.js';
import { interpolate, unsetVariables } from '../context/interpolate.js';
import { VariableStore } from '../context/variables.js';
import { targetLookup } from '../locate/locate.js';
import { durationToMs } from '../schema/common.js';
import type { TestFile } from '../schema/files.js';
import type { TargetValue } from '../schema/targets.js';
import type { Secrets } from '../sdk/context.js';
import type { SourceFile } from '../stepfile/source.js';
import type { NormalizedStep } from '../stepfile/steps.js';
import type { TestSteps } from '../stepfile/validate-file.js';
import { createStepContext, type LogLevel } from './context.js';
import { StepError, toErrorInfo, type StepStop } from './errors.js';

/** Sends one event of the run; the run adds `runId` and `seq`. */
export type EmitEvent = (method: string, params: Record<string, unknown>) => void;

/** One test instance to run. */
export interface TestInstance {
  /** `<file>#<row>`. */
  readonly testId: string;
  /** Project-relative path of the test file. */
  readonly file: string;
  /** The test's data. */
  readonly data: TestFile;
  /** Its steps, in the canonical long form. */
  readonly steps: TestSteps;
  /** The file's source, for step locations. */
  readonly source: SourceFile;
}

/** What every test of a run shares. */
export interface TestRunOptions {
  /** The run's browser. */
  readonly browser: Browser;
  /** The selected environment with its settings. */
  readonly profile: EnvironmentProfile;
  /** Every action. */
  readonly registry: ActionRegistry;
  /** The declared secrets. */
  readonly secrets: Secrets;
  /** The project's shared targets. */
  readonly sharedTargets: ReadonlyMap<string, TargetValue>;
  /** The attribute `testId` candidates match. */
  readonly testIdAttribute: string;
  /** Aborted when the run is cancelled. */
  readonly cancel: AbortSignal;
  /** Sends events. */
  readonly emit: EmitEvent;
}

/** How a test instance ended. */
export type TestStatus = 'passed' | 'failed' | 'cancelled';

type Section = 'before' | 'steps' | 'after';

type StepOutcome = 'passed' | 'failed' | 'cancelled' | 'skipped';

/** Thrown into a step's signal when the runner ends the step. */
class StepStopped extends Error {
  readonly stop: StepStop;

  constructor(stop: StepStop) {
    super(stop === 'timeout' ? 'The step timed out.' : 'The run was cancelled.');
    this.name = 'StepStopped';
    this.stop = stop;
  }
}

/**
 * The label of a step in results: its `name`, or the action and its main
 * value (the URL, the target's name, or its first parameter).
 *
 * @param step - A step in its canonical long form.
 * @returns Such as `click checkout.submit` or `goto /todos`.
 */
export function stepTitle(step: NormalizedStep): string {
  if (step.name !== undefined) return step.name;
  const main = step.params.url ?? step.params.target ?? Object.values(step.params)[0];
  return typeof main === 'string' || typeof main === 'number'
    ? `${step.action} ${String(main)}`
    : step.action;
}

/** The names of the built-in actions that have a `run`, for messages. */
function runnableBuiltins(registry: ActionRegistry): string {
  const names = registry
    .list()
    .filter((action) => action.source.kind === 'builtin' && action.run !== undefined)
    .map((action) => action.spec.name);
  return names.length > 0 ? names.join(', ') : 'none yet';
}

/** Resolves when the signal is aborted. */
function whenAborted(signal: AbortSignal): Promise<never> {
  return new Promise((_resolve, reject) => {
    const fail = (): void => {
      reject(signal.reason instanceof Error ? signal.reason : new StepStopped('cancelled'));
    };
    if (signal.aborted) fail();
    else signal.addEventListener('abort', fail, { once: true });
  });
}

/**
 * Runs one test instance and sends its events, from `testStarted` to
 * `testFinished`.
 *
 * @param test - The test instance.
 * @param options - The run's browser, environment, actions and event sink.
 * @returns How the test ended.
 */
export async function runTest(test: TestInstance, options: TestRunOptions): Promise<TestStatus> {
  const { emit, profile } = options;
  const started = performance.now();
  emit('testStarted', { testId: test.testId, startedAt: new Date().toISOString() });

  let context: BrowserContext | undefined;
  let status: TestStatus = 'passed';
  try {
    context = await options.browser.newContext({
      viewport: profile.settings.viewport,
      locale: profile.settings.locale,
      timezoneId: profile.settings.timezone,
      baseURL: profile.baseUrl,
    });
    const page = await context.newPage();
    const vars = new VariableStore(test.data.vars ?? {});
    const targets = targetLookup(test.data.targets, options.sharedTargets);

    const runStep = async (
      step: NormalizedStep,
      section: Section,
      index: number,
    ): Promise<StepOutcome> => {
      const stepId = `${section}.${String(index)}`;
      const at = test.source.positionOf(step.path);
      const location = { file: test.file, line: at.line, column: at.column };
      if (section === 'after') {
        const [unset] = unsetVariables(step.params, vars);
        if (unset !== undefined) {
          emit('stepSkipped', {
            testId: test.testId,
            stepId,
            reason: 'variableNotSet',
            message: `skipped: ${unset} was never set`,
            variable: unset,
          });
          return 'skipped';
        }
      }
      emit('stepStarted', {
        testId: test.testId,
        stepId,
        section,
        action: step.action,
        params: step.params,
        page: step.page ?? 'main',
        title: stepTitle(step),
        location,
      });

      const stepStart = performance.now();
      const timeoutMs = durationToMs(step.timeout ?? profile.settings.timeout);
      const controller = new AbortController();
      const timer = setTimeout(() => {
        controller.abort(new StepStopped('timeout'));
      }, timeoutMs);
      // `after` steps run even when the run is cancelled.
      const onCancel = (): void => {
        controller.abort(new StepStopped('cancelled'));
      };
      if (section !== 'after') {
        if (options.cancel.aborted) onCancel();
        options.cancel.addEventListener('abort', onCancel, { once: true });
      }
      const locators: LocatorUse[] = [];
      try {
        const action = options.registry.get(step.action);
        if (step.page !== undefined && step.page !== 'main') {
          throw new StepError(
            'NotImplemented',
            `Steps on named pages ("${step.page}") cannot run yet in this engine version.`,
          );
        }
        if (step.opens !== undefined) {
          throw new StepError('NotImplemented', `"opens" cannot run yet in this engine version.`);
        }
        if (action?.run === undefined) {
          throw new StepError(
            'NotImplemented',
            `The built-in action "${step.action}" cannot run yet in this engine version.`,
            `The built-in actions this engine can run: ${runnableBuiltins(options.registry)}.`,
          );
        }
        const interpolation = {
          vars,
          env: profile,
          secrets: options.secrets,
          step: `${stepId} (${step.action})`,
        };
        const params = interpolate(step.params, interpolation) as Record<string, unknown>;
        const checked = action.spec.params.safeParse(params);
        if (!checked.success) {
          throw new StepError(
            'InvalidParameters',
            `After its \${…} values were filled in, the parameters of "${step.action}" are invalid: ${checked.error.issues
              .map((issue) => `${issue.path.join('.') || 'parameters'}: ${issue.message}`)
              .join('; ')}.`,
          );
        }
        const log = (level: LogLevel, message: string): void => {
          emit('log', { level, message, testId: test.testId, stepId, location });
        };
        const ctx = createStepContext({
          page,
          request: page.request,
          vars,
          env: profile,
          secrets: options.secrets,
          interpolation,
          targets,
          testIdAttribute: options.testIdAttribute,
          fallbackGraceMs: durationToMs(profile.settings.fallbackGrace),
          deadline: stepStart + timeoutMs,
          signal: controller.signal,
          params: checked.data,
          reporter: {
            locatorUsed: (use) => locators.push(use),
            locatorFallback: (warning) => {
              emit('log', {
                level: 'warn',
                code: warning.code,
                message: warning.message,
                testId: test.testId,
                stepId,
                location,
                data: warning.data,
              });
            },
          },
          log,
        });
        await Promise.race([action.run(ctx, checked.data), whenAborted(controller.signal)]);
        // An action that returns once its signal is aborted did not pass: the
        // step was ended by its timeout or by cancellation.
        controller.signal.throwIfAborted();
        emit('stepPassed', {
          testId: test.testId,
          stepId,
          durationMs: Math.round(performance.now() - stepStart),
          locators,
          snapshot: { state: 'skipped' },
        });
        return 'passed';
      } catch (error) {
        const reason: unknown = controller.signal.reason;
        const stop =
          controller.signal.aborted && reason instanceof StepStopped ? reason.stop : undefined;
        emit('stepFailed', {
          testId: test.testId,
          stepId,
          durationMs: Math.round(performance.now() - stepStart),
          error: toErrorInfo(error, location, stop, timeoutMs),
          locators,
          snapshot: { state: 'skipped' },
        });
        return stop === 'cancelled' ? 'cancelled' : 'failed';
      } finally {
        clearTimeout(timer);
        options.cancel.removeEventListener('abort', onCancel);
      }
    };

    // before, then steps: the first failure stops the rest.
    let stopped: { outcome: 'failed' | 'cancelled'; stepId: string; section: Section } | undefined;
    for (const section of ['before', 'steps'] as const) {
      const steps = test.steps[section];
      for (const [index, step] of steps.entries()) {
        const stepId = `${section}.${String(index)}`;
        if (stopped === undefined && options.cancel.aborted) {
          stopped = { outcome: 'cancelled', stepId, section };
        }
        if (stopped !== undefined) {
          emit('stepSkipped', {
            testId: test.testId,
            stepId,
            reason: stopped.outcome === 'cancelled' ? 'cancelled' : 'previousFailure',
            message:
              stopped.outcome === 'cancelled'
                ? 'skipped: the run was cancelled'
                : stopped.section === 'before'
                  ? `skipped: the test's setup failed at ${stopped.stepId}`
                  : `skipped: ${stopped.stepId} failed`,
          });
          continue;
        }
        const outcome = await runStep(step, section, index);
        if (outcome === 'failed' || outcome === 'cancelled') {
          stopped = { outcome, stepId, section };
        }
      }
    }
    if (stopped !== undefined) status = stopped.outcome;

    // after steps always run, each on its own.
    for (const [index, step] of test.steps.after.entries()) {
      const outcome = await runStep(step, 'after', index);
      if (outcome === 'failed' && status === 'passed') status = 'failed';
    }
  } catch (error) {
    // The browser context could not be created, or the browser went away.
    status = 'failed';
    emit('log', {
      level: 'error',
      code: 'TestError',
      message: `The test could not run: ${error instanceof Error ? error.message : String(error)}`,
      testId: test.testId,
    });
  } finally {
    await context?.close().catch(() => undefined);
  }
  emit('testFinished', {
    testId: test.testId,
    status,
    durationMs: Math.round(performance.now() - started),
  });
  return status;
}
