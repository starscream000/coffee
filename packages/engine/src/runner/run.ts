// `startRun` and the run it starts (docs/protocol.md, "startRun" and
// "Events"; docs/architecture.md, "Lifecycle of a run"). Before answering,
// every selected file is validated (StepFilesInvalid, nothing runs), the
// environment and the browser are checked, and the run's folder is created.
// The answer `{ runId, resultsDir }` is sent first; the run then starts one
// browser and runs the test instances one at a time, sending runStarted …
// runFinished with `seq` rising by one. Only one run at a time.

import { randomBytes } from 'node:crypto';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { PRODUCT, type Diagnostic, type StartRunParams, type StartRunResult } from '@cfe/protocol';
import type { Browser } from 'playwright';
import { selectEnvironment, UnknownEnvironmentError } from '../context/environment.js';
import type { Project } from '../project/project.js';
import { RpcError } from '../rpc/rpc-error.js';
import type { Secrets } from '../sdk/context.js';
import { availableBrowsers, INSTALL_COMMAND, launchBrowser } from './browser.js';
import { runTest, type EmitEvent, type TestInstance, type TestStatus } from './test-run.js';

/** Sends one notification to the client. */
export type Notify = (method: string, params: Record<string, unknown>) => void;

/** How long `after` steps may run once a run is cancelled (docs/architecture.md). */
export const CANCEL_LIMIT_MS = 30_000;

/** A run id such as `20261009-054902-1a2b`: UTC date and time, then 4 random hex digits. */
function newRunId(now: Date): string {
  const iso = now.toISOString(); // 2026-10-09T05:49:02.123Z
  const date = iso.slice(0, 10).replaceAll('-', '');
  const time = iso.slice(11, 19).replaceAll(':', '');
  return `${date}-${time}-${randomBytes(2).toString('hex')}`;
}

function posix(path: string): string {
  return path.replaceAll('\\', '/');
}

function problem(file: string, code: string, message: string): Diagnostic {
  return { file, line: 1, column: 1, severity: 'error', code, message };
}

/** A test selected for a run: an instance to run, or one to report as skipped. */
interface PlannedTest {
  readonly instance: TestInstance;
  readonly name: string;
  readonly skip: string | undefined;
}

/**
 * Starts and stops runs for one engine session.
 *
 * @example
 * ```ts
 * const runs = new RunManager(notify);
 * const { runId } = await runs.start(project, params);
 * await runs.stop(); // on shutdown
 * ```
 */
export class RunManager {
  private readonly notify: Notify;
  private current: { cancel: AbortController; done: Promise<void> } | undefined;

  /**
   * @param notify - Sends events to the client.
   */
  constructor(notify: Notify) {
    this.notify = notify;
  }

  /** Whether a run is in progress. */
  get running(): boolean {
    return this.current !== undefined;
  }

  /**
   * Checks a `startRun` request and starts the run once the answer is sent.
   *
   * @param project - The open project.
   * @param params - The request's parameters.
   * @returns The run's id and folder.
   * @throws RpcError `RunInProgress`, `StepFilesInvalid` (with
   *   `data.diagnostics`), or `InvalidParams` for an unknown environment or
   *   browser, a browser that is not installed, or tests this engine cannot
   *   run yet.
   */
  start(project: Project, params: StartRunParams): Promise<StartRunResult> {
    if (this.current !== undefined) {
      throw new RpcError(
        'RunInProgress',
        'A run is already in progress. Wait for runFinished, or cancel it first.',
      );
    }
    const config = project.config;
    const secrets = project.secrets;
    if (config === undefined || secrets === undefined) {
      throw new RpcError(
        'StepFilesInvalid',
        `${PRODUCT.configFile} has errors, so nothing can run. Fix them first.`,
        { diagnostics: project.configValidation.diagnostics },
      );
    }

    // Every selected file is validated first (docs/protocol.md, "startRun").
    const planned = this.select(project, params);

    let profile;
    try {
      profile = selectEnvironment(config, params.env);
    } catch (error) {
      if (error instanceof UnknownEnvironmentError) {
        throw new RpcError('InvalidParams', error.message);
      }
      throw error;
    }

    const browser = params.options?.browser ?? config.defaults?.browser ?? 'chromium';
    const browsers = availableBrowsers();
    if (!browsers.includes(browser)) {
      throw new RpcError(
        'InvalidParams',
        browser === 'chromium'
          ? `Chromium is not installed for this engine. Install it with: ${INSTALL_COMMAND}`
          : `This engine cannot run the browser "${browser}". It can run: ${browsers.join(', ') || 'none (install Chromium with: ' + INSTALL_COMMAND + ')'}.`,
      );
    }

    const withRows = planned.filter((test) => test.instance.data.data !== undefined);
    if (withRows.length > 0) {
      throw new RpcError(
        'InvalidParams',
        `Tests with data rows cannot run in this engine version yet: ${withRows.map((test) => test.instance.file).join(', ')}.`,
      );
    }

    const runId = newRunId(new Date());
    const resultsDir = join(project.root, PRODUCT.dataDir, 'runs', runId);
    mkdirSync(resultsDir, { recursive: true });

    const cancel = new AbortController();
    let seq = 0;
    const emit: EmitEvent = (method, eventParams) => {
      seq += 1;
      this.notify(method, { runId, seq, ...eventParams });
    };
    // Start once the answer has been written: the session writes it right
    // after this handler resolves, before the next macrotask.
    const done = new Promise<void>((resolve) => {
      setImmediate(() => {
        void this.execute(project, planned, {
          browserName: browser,
          secrets,
          headed: params.options?.headed === true,
          profile,
          emit,
          cancel: cancel.signal,
        }).finally(() => {
          this.current = undefined;
          resolve();
        });
      });
    });
    this.current = { cancel, done };
    return Promise.resolve({ runId, resultsDir: posix(resultsDir) });
  }

  /**
   * Cancels the run in progress, if any, and waits until it has ended: the
   * current step fails with `Cancelled`, the rest are skipped, `after` steps
   * still run (for at most 30 seconds), and the browser is closed.
   *
   * @returns A promise that resolves when no run is in progress.
   */
  async stop(): Promise<void> {
    const current = this.current;
    if (current === undefined) return;
    current.cancel.abort();
    let timer: NodeJS.Timeout | undefined;
    await Promise.race([
      current.done,
      new Promise<void>((resolve) => {
        timer = setTimeout(resolve, CANCEL_LIMIT_MS);
      }),
    ]);
    clearTimeout(timer);
  }

  /** The tests a `startRun` selects, validated; throws StepFilesInvalid on any error. */
  private select(project: Project, params: StartRunParams): PlannedTest[] {
    project.refresh();
    const requested =
      params.files === undefined
        ? [...project.files.tests]
        : params.files.map((file) => project.toProjectPath(file));
    const diagnostics: Diagnostic[] = project
      .validate({ files: requested })
      .filter((diagnostic) => diagnostic.severity === 'error');
    const planned: PlannedTest[] = [];
    for (const file of requested) {
      if (!file.endsWith('.test.yaml')) {
        if (!diagnostics.some((diagnostic) => diagnostic.file === file)) {
          diagnostics.push(
            problem(
              file,
              'NotATestFile',
              `"${file}" is not a test file; only *.test.yaml files run.`,
            ),
          );
        }
        continue;
      }
      const validation = project.readStepFile(file);
      const parsed = validation?.parsed;
      if (validation === undefined || parsed?.kind !== 'test') continue;
      const tags = parsed.data.tags ?? [];
      if (params.tags !== undefined && !params.tags.some((tag) => tags.includes(tag))) continue;
      planned.push({
        instance: {
          testId: `${file}#0`,
          file,
          data: parsed.data,
          steps: parsed.steps,
          source: validation.source,
        },
        name: parsed.data.name,
        skip: parsed.data.skip,
      });
    }
    if (diagnostics.length > 0) {
      throw new RpcError(
        'StepFilesInvalid',
        `${String(diagnostics.length)} problem${diagnostics.length === 1 ? '' : 's'} in the selected step files; nothing ran. Fix them and start the run again.`,
        { diagnostics },
      );
    }
    return planned;
  }

  /** Runs the planned tests and sends every event of the run. */
  private async execute(
    project: Project,
    planned: readonly PlannedTest[],
    run: {
      browserName: string;
      secrets: Secrets;
      headed: boolean;
      profile: ReturnType<typeof selectEnvironment>;
      emit: EmitEvent;
      cancel: AbortSignal;
    },
  ): Promise<void> {
    const { emit, profile } = run;
    const started = performance.now();
    emit('runStarted', {
      env: profile.name,
      browser: run.browserName,
      settings: {
        viewport: profile.settings.viewport,
        locale: profile.settings.locale,
        timezone: profile.settings.timezone,
      },
      startedAt: new Date().toISOString(),
      tests: planned.map((test) => ({
        testId: test.instance.testId,
        file: test.instance.file,
        name: test.name,
        ...(test.skip === undefined ? {} : { skip: test.skip }),
      })),
    });

    const totals = { passed: 0, failed: 0, cancelled: 0, skipped: 0 };
    let browser: Browser | undefined;
    try {
      browser = await launchBrowser(run.headed);
      for (const test of planned) {
        if (test.skip !== undefined) {
          emit('testSkipped', { testId: test.instance.testId, reason: test.skip });
          totals.skipped += 1;
          continue;
        }
        let status: TestStatus;
        if (run.cancel.aborted) {
          // Tests not yet started are reported as cancelled.
          emit('testFinished', {
            testId: test.instance.testId,
            status: 'cancelled',
            durationMs: 0,
          });
          status = 'cancelled';
        } else {
          status = await runTest(test.instance, {
            browser,
            profile,
            registry: project.registry,
            secrets: run.secrets,
            sharedTargets: project.sharedTargetValues(),
            testIdAttribute: project.config?.defaults?.testIdAttribute ?? 'data-testid',
            cancel: run.cancel,
            emit,
          });
        }
        totals[status] += 1;
      }
    } catch (error) {
      emit('log', {
        level: 'error',
        code: 'BrowserError',
        message: `The browser could not start: ${error instanceof Error ? error.message : String(error)}`,
      });
      totals.failed +=
        planned.length - totals.passed - totals.failed - totals.cancelled - totals.skipped;
    } finally {
      await browser?.close().catch(() => undefined);
    }
    emit('runFinished', {
      status: run.cancel.aborted ? 'cancelled' : totals.failed > 0 ? 'failed' : 'passed',
      durationMs: Math.round(performance.now() - started),
      totals,
    });
  }
}
