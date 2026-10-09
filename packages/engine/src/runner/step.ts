// Runs one step: resolves its page, fills in its `${…}` values, checks its
// parameters, builds `ctx` and calls the action, within the step's timeout
// (docs/actions.md, "ctx.signal"). When the timeout passes or the run is
// cancelled, the signal is aborted and the action gets 2 seconds to return; an
// action that does not is a stray: its page is closed, its `ctx` is sealed and
// a `StrayActionCode` warning is sent. Used for the steps of tests and of
// login flows; the caller sends the step's own events.

import type { ErrorInfo, Location, LocatorUse } from '@cfe/protocol';
import type { Page } from 'playwright';
import type { ActionRegistry } from '../actions/registry.js';
import type { EnvironmentProfile } from '../context/environment.js';
import { interpolate } from '../context/interpolate.js';
import type { VariableStore } from '../context/variables.js';
import type { TargetLookup } from '../locate/locate.js';
import { durationToMs } from '../schema/common.js';
import type { Secrets } from '../sdk/context.js';
import type { DataRow } from '../stepfile/data-rows.js';
import type { NormalizedStep } from '../stepfile/steps.js';
import { createStepContext, type LogLevel } from './context.js';
import { StepError, toErrorInfo, type StepStop } from './errors.js';
import type { PageSet } from './pages.js';
import type { EmitEvent } from './test-run.js';

/** Time the wait for an `opens` page keeps back before the step's deadline. */
const OPENS_MARGIN_MS = 150;

/** How long an action may keep running after its signal is aborted. */
export const STRAY_GRACE_MS = 2_000;

/** Everything one step needs. */
export interface StepRun {
  /** Every action. */
  readonly registry: ActionRegistry;
  /** The selected environment. */
  readonly profile: EnvironmentProfile;
  /** The declared secrets. */
  readonly secrets: Secrets;
  /** The attribute `testId` candidates match. */
  readonly testIdAttribute: string;
  /** Variables of the test (or flow). */
  readonly vars: VariableStore;
  /** Named targets of the file, then the shared ones. */
  readonly targets: TargetLookup;
  /** The data row, for tests with `data`. */
  readonly row?: DataRow | undefined;
  /** The flow's parameters, inside a flow. */
  readonly flowParams?: Readonly<Record<string, unknown>> | undefined;
  /** The test's pages. */
  readonly pages: PageSet;
  /** The section the step is in; only `after` steps get a replaced page. */
  readonly section: 'before' | 'steps' | 'after';
  /** The test instance and step, for events. */
  readonly testId: string;
  /** The step's id. */
  readonly stepId: string;
  /** Where the step is written. */
  readonly location: Location;
  /** The step's timeout in milliseconds. */
  readonly timeoutMs: number;
  /** Aborted when the run is cancelled; absent for steps that are not cancelled. */
  readonly cancel?: AbortSignal | undefined;
  /** Sends events (logs and warnings). */
  readonly emit: EmitEvent;
}

/** How a step ended. */
export interface StepResult {
  /** Passed, failed, or ended by cancellation. */
  readonly outcome: 'passed' | 'failed' | 'cancelled';
  /** Why it failed, for `stepFailed`. */
  readonly error?: ErrorInfo;
  /** Every target the step resolved. */
  readonly locators: readonly LocatorUse[];
  /** How long it took. */
  readonly durationMs: number;
}

/** Thrown into a step's signal when the runner ends the step. */
export class StepStopped extends Error {
  /** Why the runner ended the step. */
  readonly stop: StepStop;

  /**
   * @param stop - Why the step was ended.
   */
  constructor(stop: StepStop) {
    super(stop === 'timeout' ? 'The step timed out.' : 'The run was cancelled.');
    this.name = 'StepStopped';
    this.stop = stop;
  }
}

/** The names of the built-in actions that have a `run`, for messages. */
function runnableBuiltins(registry: ActionRegistry): string {
  const names = registry
    .list()
    .filter((action) => action.source.kind === 'builtin' && action.run !== undefined)
    .map((action) => action.spec.name);
  return names.length > 0 ? names.join(', ') : 'none yet';
}

/** Rejects when the signal is aborted. */
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
 * Runs one step.
 *
 * @param step - The step in its canonical long form.
 * @param run - Its environment, page set, timing and event sink.
 * @returns How the step ended; never throws for a failing step.
 */
export async function executeStep(step: NormalizedStep, run: StepRun): Promise<StepResult> {
  const start = performance.now();
  // The step's timeout starts once its page is ready: signing in with a saved
  // login runs the login flow, whose steps have timeouts of their own.
  let deadline = start + run.timeoutMs;
  const controller = new AbortController();
  let timer: NodeJS.Timeout | undefined;
  const startTimer = (): void => {
    deadline = performance.now() + run.timeoutMs;
    timer = setTimeout(() => {
      controller.abort(new StepStopped('timeout'));
    }, run.timeoutMs);
  };
  const onCancel = (): void => {
    controller.abort(new StepStopped('cancelled'));
  };
  if (run.cancel !== undefined) {
    if (run.cancel.aborted) onCancel();
    run.cancel.addEventListener('abort', onCancel, { once: true });
  }
  const pageName = step.page ?? 'main';
  const locators: LocatorUse[] = [];
  let running: Promise<void> | undefined;
  // Changed from callbacks, so kept in an object the type checker does not narrow.
  const state = { settled: false, strayed: false };
  const settled = (): boolean => state.settled;
  const event = { testId: run.testId, stepId: run.stepId, location: run.location };

  try {
    run.pages.beginStep(run.stepId, run.location, step.opens);
    const action = run.registry.get(step.action);
    if (action?.run === undefined) {
      throw new StepError(
        'NotImplemented',
        `The built-in action "${step.action}" cannot run yet in this engine version.`,
        `The built-in actions this engine can run: ${runnableBuiltins(run.registry)}.`,
      );
    }
    const page: Page = await Promise.race([
      run.pages.page(pageName, run.section),
      whenAborted(controller.signal),
    ]);
    startTimer();
    const interpolation = {
      vars: run.vars,
      env: run.profile,
      secrets: run.secrets,
      row: run.row,
      params: run.flowParams,
      step: `${run.stepId} (${step.action})`,
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
      run.emit('log', { level, message, ...event });
    };
    const ctx = createStepContext({
      page,
      request: page.request,
      vars: run.vars,
      env: run.profile,
      secrets: run.secrets,
      interpolation,
      targets: run.targets,
      testIdAttribute: run.testIdAttribute,
      fallbackGraceMs: durationToMs(run.profile.settings.fallbackGrace),
      deadline,
      signal: controller.signal,
      params: checked.data,
      reporter: {
        locatorUsed: (use) => locators.push(use),
        locatorFallback: (warning) => {
          run.emit('log', {
            level: 'warn',
            code: warning.code,
            message: warning.message,
            ...event,
            data: warning.data,
          });
        },
      },
      log,
      sealed: () => state.strayed,
    });
    running = action.run(ctx, checked.data);
    running.then(
      () => {
        state.settled = true;
      },
      () => {
        state.settled = true;
      },
    );
    await Promise.race([running, whenAborted(controller.signal)]);
    // An action that returns once its signal is aborted did not pass: the
    // step was ended by its timeout or by cancellation.
    controller.signal.throwIfAborted();
    // Keep a margin, so a missing tab is PageNotOpened rather than ActionTimeout.
    await run.pages.endStep(deadline - OPENS_MARGIN_MS, controller.signal);
    return { outcome: 'passed', locators, durationMs: Math.round(performance.now() - start) };
  } catch (error) {
    run.pages.clearStep();
    const reason: unknown = controller.signal.reason;
    const stop =
      controller.signal.aborted && reason instanceof StepStopped ? reason.stop : undefined;
    let info = toErrorInfo(error, run.location, stop, run.timeoutMs);
    if (stop !== undefined && running !== undefined && !state.settled) {
      // The action did not stop with its signal: give it a moment, then cut it off.
      await Promise.race([
        running.catch(() => undefined),
        new Promise((resolve) => setTimeout(resolve, STRAY_GRACE_MS)),
      ]);
      if (!settled()) {
        state.strayed = true;
        await run.pages.closePage(pageName);
        run.emit('log', {
          level: 'warn',
          code: 'StrayActionCode',
          message: `The action "${step.action}" kept running after its step ended, so its page "${pageName}" was closed. Its later calls to ctx.vars.set, ctx.log and ctx.locate are ignored.`,
          ...event,
        });
        info = { ...info, hint: 'The action did not stop when ctx.signal was aborted.' };
      }
    }
    return {
      outcome: stop === 'cancelled' ? 'cancelled' : 'failed',
      error: info,
      locators,
      durationMs: Math.round(performance.now() - start),
    };
  } finally {
    clearTimeout(timer);
    run.cancel?.removeEventListener('abort', onCancel);
  }
}
