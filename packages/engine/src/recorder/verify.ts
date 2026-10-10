// Verify (docs/recording.md, "Verify"; ADR 0022, R6): the recorded file runs
// with the normal runner, exactly as startRun runs it (RunManager, a fresh
// browser context, a run folder, masked events), and each step is reported.
// A recording counts as runnable only after a verify that passed.

import type { ErrorInfo } from '@cfe/protocol';
import { getEngineInfo } from '../engine-info.js';
import type { Project } from '../project/project.js';
import { MessageWriter } from '../rpc/message-writer.js';
import { RunManager } from '../runner/run.js';

/** How one step of the recorded test ended. */
export interface VerifiedStep {
  /** Such as `steps.2`. */
  readonly stepId: string;
  /** The action. */
  readonly action: string;
  /** The step's label in results, such as `click todos.add`. */
  readonly title: string;
  /** How it ended. */
  readonly status: 'passed' | 'failed' | 'skipped';
  /** Why it failed: code, message, location and, for a target, every candidate's match count. */
  readonly error?: ErrorInfo | undefined;
}

/** The outcome of a verify. */
export interface VerifyResult {
  /** `passed` only when every step passed. */
  readonly status: 'passed' | 'failed' | 'cancelled';
  /** The run's id. */
  readonly runId: string;
  /** The run's folder. */
  readonly resultsDir: string;
  /** Every step, in order. */
  readonly steps: readonly VerifiedStep[];
  /** The first step that failed, when one did. */
  readonly failed?: VerifiedStep | undefined;
}

/** Options of {@link verifyRecording}. */
export interface VerifyOptions {
  /** The environment; default: the config's default. */
  readonly environment?: string | undefined;
  /** Show the browser window. */
  readonly headed?: boolean | undefined;
}

/**
 * Runs a recorded test file with the normal runner and reports each step.
 *
 * @param project - The open project.
 * @param file - The recorded test file, relative to the project root.
 * @param options - Environment, and whether the browser is visible.
 * @returns The run's outcome, step by step.
 * @throws RpcError when the run cannot start, as `startRun` would answer:
 *   `StepFilesInvalid` for a file with errors, for example.
 * @example
 * ```ts
 * const result = await verifyRecording(project, 'tests/add-todo.test.yaml');
 * if (result.status !== 'passed') console.log(result.failed?.error?.message);
 * ```
 */
export async function verifyRecording(
  project: Project,
  file: string,
  options: VerifyOptions = {},
): Promise<VerifyResult> {
  const events: { method: string; params: Record<string, unknown> }[] = [];
  let finished: () => void = () => undefined;
  const done = new Promise<void>((resolve) => {
    finished = resolve;
  });
  // Rendered as for a client, so the events are masked and within size limits.
  const writer = new MessageWriter({ write: () => true }, { mask: (text) => project.mask(text) });
  const info = getEngineInfo();
  const runs = new RunManager(
    {
      render: (method, params) => writer.render({ jsonrpc: '2.0', method, params }),
      send: (line) => {
        const message = JSON.parse(line) as { method: string; params: Record<string, unknown> };
        events.push(message);
        if (message.method === 'runFinished') finished();
      },
    },
    { engineVersion: info.version, protocolVersion: info.protocolVersion },
  );
  const { runId, resultsDir } = await runs.start(project, {
    files: [file],
    ...(options.environment === undefined ? {} : { env: options.environment }),
    options: { headed: options.headed === true },
  });
  await done;
  await runs.stop();

  const steps = new Map<string, VerifiedStep>();
  for (const { method, params } of events) {
    const stepId = typeof params.stepId === 'string' ? params.stepId : undefined;
    if (stepId === undefined) continue;
    const known = steps.get(stepId);
    if (method === 'stepStarted') {
      steps.set(stepId, {
        stepId,
        action: String(params.action),
        title: String(params.title),
        status: 'skipped',
      });
    } else if (method === 'stepPassed' && known !== undefined) {
      steps.set(stepId, { ...known, status: 'passed' });
    } else if (method === 'stepFailed' && known !== undefined) {
      steps.set(stepId, { ...known, status: 'failed', error: params.error as ErrorInfo });
    } else if (method === 'stepSkipped') {
      steps.set(stepId, { stepId, action: '', title: stepId, status: 'skipped' });
    }
  }
  const finishedEvent = events.find((event) => event.method === 'runFinished');
  const status = finishedEvent?.params.status;
  const list = [...steps.values()];
  return {
    status: status === 'passed' ? 'passed' : status === 'cancelled' ? 'cancelled' : 'failed',
    runId,
    resultsDir,
    steps: list,
    failed: list.find((step) => step.status === 'failed'),
  };
}
