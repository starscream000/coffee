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
import { interpolate } from '../context/interpolate.js';
import { VariableStore } from '../context/variables.js';
import { readDataRows } from '../stepfile/data-rows.js';
import { availableBrowsers, INSTALL_COMMAND, launchBrowser } from './browser.js';
import { LoginStore, loginsDir, pruneLogins, type LoginPruneResult } from './logins.js';
import { DEFAULT_KEEP_RUNS, pruneRuns, type PruneResult } from './keep-runs.js';
import { RunFolder, type RunFolderInfo } from './run-folder.js';
import { runTest, type EmitEvent, type TestInstance, type TestStatus } from './test-run.js';

/**
 * How a run's events reach the client: each is rendered first (masked, within
 * the size limit), recorded in the run folder, and only then sent, so a
 * client that reads the folder on any event finds that event there already.
 */
export interface EventChannel {
  /** Renders an event as the line that will be sent. */
  render(method: string, params: Record<string, unknown>): string;
  /** Sends a rendered line. */
  send(line: string): void;
}

/** How long `after` steps may run once a run is cancelled (docs/architecture.md). */
export const CANCEL_LIMIT_MS = 30_000;

/**
 * A run id such as `20261009-054902-123-1a2b`: UTC date, time and
 * milliseconds, then 4 random hex digits, so run ids sort by start time
 * (ADR 0015).
 */
function newRunId(now: Date): string {
  const iso = now.toISOString(); // 2026-10-09T05:49:02.123Z
  const date = iso.slice(0, 10).replaceAll('-', '');
  const time = iso.slice(11, 19).replaceAll(':', '');
  const ms = iso.slice(20, 23);
  return `${date}-${time}-${ms}-${randomBytes(2).toString('hex')}`;
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
 * const runs = new RunManager(channel, { engineVersion: '0.1.0', protocolVersion: '0.1.0' });
 * const { runId } = await runs.start(project, params);
 * await runs.stop(); // on shutdown
 * ```
 */
export class RunManager {
  private readonly channel: EventChannel;
  private readonly info: RunFolderInfo;
  private current: { runId: string; cancel: AbortController; done: Promise<void> } | undefined;

  /**
   * @param channel - Renders and sends events to the client.
   * @param info - The engine's versions, for `run.json`.
   */
  constructor(channel: EventChannel, info: RunFolderInfo) {
    this.channel = channel;
    this.info = info;
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
   *   browser, or a browser that is not installed.
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

    // keepRuns is applied before the new run folder is created (ADR 0015).
    const runsDir = join(project.root, PRODUCT.dataDir, 'runs');
    const pruned = pruneRuns(runsDir, config.defaults?.keepRuns ?? DEFAULT_KEEP_RUNS);
    // Saved logins that can no longer be used go before any test (ADR 0018).
    const prunedLogins = pruneLogins(loginsDir(project.root), config, Date.now());
    const runId = newRunId(new Date());
    const resultsDir = join(runsDir, runId);
    mkdirSync(resultsDir, { recursive: true });
    const folder = new RunFolder(resultsDir, this.info);

    const cancel = new AbortController();
    let seq = 0;
    let folderBroken = false;
    // Record first, then send (review 0006, finding 1): when a client receives
    // an event, the run folder already holds it.
    const emit: EmitEvent = (method, eventParams) => {
      seq += 1;
      const line = this.channel.render(method, { runId, seq, ...eventParams });
      let failure: string | undefined;
      if (!folderBroken) {
        try {
          folder.record(line);
        } catch (error) {
          folderBroken = true;
          failure = error instanceof Error ? error.message : String(error);
        }
      }
      this.channel.send(line);
      if (failure !== undefined) {
        // The run goes on; the client still receives every event.
        seq += 1;
        this.channel.send(
          this.channel.render('log', {
            runId,
            seq,
            level: 'error',
            code: 'RunFolderFailed',
            message: `The run folder could not be written, so it is incomplete: ${failure}`,
          }),
        );
      }
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
          cleanupFailures: pruned.failed,
          loginCleanupFailures: prunedLogins.failed,
          refreshLogins: params.options?.refreshLogins === true,
        }).finally(() => {
          this.current = undefined;
          resolve();
        });
      });
    });
    this.current = { runId, cancel, done };
    return Promise.resolve({ runId, resultsDir: posix(resultsDir) });
  }

  /**
   * Starts cancelling a run (`cancelRun`) and returns at once: the current
   * step fails with `Cancelled`, the remaining steps are skipped, `after` steps
   * still run within 30 seconds, tests not started are reported as cancelled,
   * and `runFinished` says `cancelled`.
   *
   * @param runId - The run to cancel.
   * @throws RpcError `InvalidParams` when no run with that id is in progress.
   */
  cancel(runId: string): void {
    if (this.current?.runId !== runId) {
      throw new RpcError(
        'InvalidParams',
        this.current === undefined
          ? `No run is in progress, so "${runId}" cannot be cancelled.`
          : `The run in progress is "${this.current.runId}", not "${runId}".`,
      );
    }
    this.current.cancel.abort();
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
      // One test instance per data row (docs/step-format.md, "Data rows").
      const rows = readDataRows(parsed.data.data, file, (path) => project.readText(path));
      if (rows.rows === undefined) continue; // reported by validation as DataFileInvalid
      const hasRows = parsed.data.data !== undefined;
      rows.rows.forEach((row, index) => {
        planned.push({
          instance: {
            testId: `${file}#${String(index)}`,
            file,
            data: parsed.data,
            steps: parsed.steps,
            source: validation.source,
            ...(hasRows ? { row, rowIndex: index } : {}),
          },
          name: parsed.data.name,
          skip: parsed.data.skip,
        });
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
      cleanupFailures: PruneResult['failed'];
      loginCleanupFailures: LoginPruneResult['failed'];
      refreshLogins: boolean;
    },
  ): Promise<void> {
    const { emit, profile } = run;
    const started = performance.now();
    // A name may use ${row.…}, so each data row has its own name.
    const nameOf = (test: PlannedTest): string => {
      try {
        const scope = {
          vars: new VariableStore(test.instance.data.vars ?? {}),
          env: profile,
          secrets: run.secrets,
          row: test.instance.row,
        };
        return String(interpolate(test.name, scope));
      } catch {
        return test.name;
      }
    };
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
        name: nameOf(test),
        ...(test.instance.rowIndex === undefined ? {} : { row: test.instance.rowIndex }),
        ...(test.skip === undefined ? {} : { skip: test.skip }),
      })),
    });
    for (const failure of run.loginCleanupFailures) {
      emit('log', {
        level: 'warn',
        code: 'LoginCleanupFailed',
        message: `The old saved login ${failure.file} could not be deleted; it is tried again at the next run: ${failure.reason}`,
      });
    }
    for (const failure of run.cleanupFailures) {
      emit('log', {
        level: 'warn',
        code: 'RunCleanupFailed',
        message: `The old run folder ${failure.runId} could not be deleted; it is tried again at the next run: ${failure.reason}`,
      });
    }

    const totals = { passed: 0, failed: 0, cancelled: 0, skipped: 0 };
    let browser: Browser | undefined;
    try {
      browser = await launchBrowser(run.headed);
      const config = project.config;
      if (config === undefined) throw new Error('The config is not valid.');
      const logins = new LoginStore({
        project,
        config,
        profile,
        secrets: run.secrets,
        registry: project.registry,
        sharedTargets: project.sharedTargetValues(),
        testIdAttribute: config.defaults?.testIdAttribute ?? 'data-testid',
        browser,
        refresh: run.refreshLogins,
        engineVersion: this.info.engineVersion,
      });
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
            logins: (fresh) => logins.forTest(test.instance.testId, fresh, emit),
            root: project.root,
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
