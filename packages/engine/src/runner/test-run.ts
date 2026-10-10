// Runs one test instance (docs/step-format.md, "Execution rules";
// docs/architecture.md, "Lifecycle of a run"): its pages and browser contexts
// with the environment's settings, `before` then `steps` until the first
// failure, the rest skipped, and `after` steps always, each on its own. When
// the run is cancelled, the current step fails with `Cancelled`, the rest are
// skipped, and the `after` steps still run within 30 seconds in all.

import type { Browser } from 'playwright';
import type { ActionRegistry } from '../actions/registry.js';
import type { EnvironmentProfile } from '../context/environment.js';
import { unsetVariables } from '../context/interpolate.js';
import { VariableStore } from '../context/variables.js';
import { targetLookup } from '../locate/locate.js';
import { durationToMs } from '../schema/common.js';
import type { TestFile } from '../schema/files.js';
import type { TargetValue } from '../schema/targets.js';
import type { Secrets } from '../sdk/context.js';
import type { DataRow } from '../stepfile/data-rows.js';
import type { SourceFile } from '../stepfile/source.js';
import type { NormalizedStep } from '../stepfile/steps.js';
import type { TestSteps } from '../stepfile/validate-file.js';
import { PageSet, type LoginStates } from './pages.js';
import { ResponseLog } from './responses.js';
import { executeStep } from './step.js';

/** Sends one event of the run; the run adds `runId` and `seq`. */
export type EmitEvent = (method: string, params: Record<string, unknown>) => void;

/** How long `after` steps may run in all once a run is cancelled (docs/architecture.md). */
export const AFTER_LIMIT_MS = 30_000;

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
  /** The data row, for tests with `data`. */
  readonly row?: DataRow | undefined;
  /** The data row's index, for tests with `data`. */
  readonly rowIndex?: number | undefined;
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
  /** Gives the storage state of a saved login for this test. */
  readonly logins: (fresh: boolean) => LoginStates;
  /** The project's root folder. */
  readonly root: string;
  /** Registers a value found while a step runs as a secret (ADR 0014). */
  readonly registerSecret: (value: string) => boolean;
  /** Aborted when the run is cancelled. */
  readonly cancel: AbortSignal;
  /** Sends events. */
  readonly emit: EmitEvent;
}

/** How a test instance ended. */
export type TestStatus = 'passed' | 'failed' | 'cancelled';

type Section = 'before' | 'steps' | 'after';

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

/** The login of each declared page; `login:` is the shorthand for `pages.main.login`. */
function pageLogins(data: TestFile): Map<string, string | undefined> {
  const logins = new Map<string, string | undefined>();
  for (const [name, page] of Object.entries(data.pages ?? {})) logins.set(name, page.login);
  if (data.login !== undefined) logins.set('main', data.login);
  return logins;
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

  // Every response of the test, for wait.response and expect.response.
  const responses = new ResponseLog();
  const pages = new PageSet({
    browser: options.browser,
    profile,
    pageLogins: pageLogins(test.data),
    logins: options.logins(test.data.freshLogin === true),
    responses,
    testId: test.testId,
    emit,
  });
  const vars = new VariableStore(test.data.vars ?? {});
  const targets = targetLookup(test.data.targets, options.sharedTargets);
  let status: TestStatus = 'passed';
  let cancelledAt: number | undefined;
  // When the previous step started: the response log is read from then on.
  let previousStepStart = started;

  const runStep = async (
    step: NormalizedStep,
    section: Section,
    index: number,
  ): Promise<'passed' | 'failed' | 'cancelled' | 'skipped'> => {
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
    let timeoutMs = durationToMs(step.timeout ?? profile.settings.timeout);
    if (section === 'after' && cancelledAt !== undefined) {
      // After a cancellation, the after steps share 30 seconds.
      timeoutMs = Math.max(
        1,
        Math.min(timeoutMs, cancelledAt + AFTER_LIMIT_MS - performance.now()),
      );
    }
    const stepStart = performance.now();
    const result = await executeStep(step, {
      registry: options.registry,
      profile,
      secrets: options.secrets,
      testIdAttribute: options.testIdAttribute,
      vars,
      targets,
      row: test.row,
      pages,
      section,
      testId: test.testId,
      stepId,
      location,
      root: options.root,
      registerSecret: options.registerSecret,
      responses,
      responsesSince: previousStepStart,
      timeoutMs,
      // after steps run even when the run is cancelled.
      cancel: section === 'after' ? undefined : options.cancel,
      emit,
    });
    previousStepStart = stepStart;
    if (result.outcome === 'passed') {
      emit('stepPassed', {
        testId: test.testId,
        stepId,
        durationMs: result.durationMs,
        locators: result.locators,
        snapshot: { state: 'skipped' },
      });
    } else {
      emit('stepFailed', {
        testId: test.testId,
        stepId,
        durationMs: result.durationMs,
        error: result.error,
        locators: result.locators,
        snapshot: { state: 'skipped' },
      });
    }
    return result.outcome;
  };

  try {
    // before, then steps: the first failure stops the rest.
    let stopped: { outcome: 'failed' | 'cancelled'; stepId: string; section: Section } | undefined;
    for (const section of ['before', 'steps'] as const) {
      for (const [index, step] of test.steps[section].entries()) {
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
    if (options.cancel.aborted) {
      status = 'cancelled';
      cancelledAt = performance.now();
    }

    // after steps always run, each on its own.
    for (const [index, step] of test.steps.after.entries()) {
      const outcome = await runStep(step, 'after', index);
      if (outcome === 'failed' && status === 'passed') status = 'failed';
    }
  } catch (error) {
    // The browser went away, or another unexpected engine failure.
    status = 'failed';
    emit('log', {
      level: 'error',
      code: 'TestError',
      message: `The test could not run: ${error instanceof Error ? error.message : String(error)}`,
      testId: test.testId,
    });
  } finally {
    await pages.close();
  }
  emit('testFinished', {
    testId: test.testId,
    status,
    durationMs: Math.round(performance.now() - started),
  });
  return status;
}
