// Recording over the protocol (docs/protocol.md, "startRecording",
// "stopRecording", "verifyRecording"; docs/recording.md). One recording at a
// time, and none while a run is going. The recording's events are held back
// until the answer to startRecording has been sent, then sent in order. A
// recording ends on stopRecording, when the person closes the browser, or when
// the engine shuts down or loses its client; the file keeps what was recorded.
// verifyRecording runs the file with the run manager, exactly as startRun, and
// sends recordingVerified after runFinished.

import { randomBytes } from 'node:crypto';
import {
  PRODUCT,
  type StartRecordingParams,
  type StartRecordingResult,
  type StartRunResult,
  type StopRecordingResult,
} from '@cfe/protocol';
import type { Project } from '../project/project.js';
import { RpcError } from '../rpc/rpc-error.js';
import type { EventChannel, RunManager } from '../runner/run.js';
import { targetValue, type RecordedStep, type RecordedTarget } from './document.js';
import { RecordingError, RecordingSession, type RecordingEnd } from './session.js';

/**
 * Environment variable for tests only: `1` runs the recording browser without
 * a window. Over the protocol the recording browser is otherwise always visible.
 */
export const RECORDING_HEADLESS_VARIABLE = `${PRODUCT.envPrefix}TEST_RECORDING_HEADLESS`;

/**
 * Environment variable for tests only: a port for the recording browser's
 * remote debugging, so a test can drive it from its own process.
 */
export const RECORDING_DEBUG_PORT_VARIABLE = `${PRODUCT.envPrefix}TEST_RECORDING_DEBUG_PORT`;

/** The command that installs the browser a visible recording needs. */
const INSTALL_FULL_BROWSER = 'npx playwright install chromium';

/** A recording id such as `rec-20261010-101500-123-1a2b`. */
function newRecordingId(now: Date): string {
  const iso = now.toISOString();
  const date = iso.slice(0, 10).replaceAll('-', '');
  const time = iso.slice(11, 19).replaceAll(':', '');
  return `rec-${date}-${time}-${iso.slice(20, 23)}-${randomBytes(2).toString('hex')}`;
}

/** A step as the protocol carries it: the canonical long form, without the review reason. */
function protocolStep(step: RecordedStep): Record<string, unknown> {
  return {
    action: step.action,
    params: step.params,
    ...(step.page === undefined ? {} : { page: step.page }),
    ...(step.opens === undefined ? {} : { opens: step.opens }),
  };
}

/** The recording in progress. */
interface Active {
  readonly id: string;
  readonly project: Project;
  readonly file: string;
  readonly session: RecordingSession;
}

/**
 * Starts, stops and verifies recordings for one engine session.
 *
 * @example
 * ```ts
 * const recordings = new RecordingManager(channel, runs);
 * const { recordingId } = await recordings.start(project, { file: 'tests/a.test.yaml' });
 * await recordings.stop(recordingId);
 * ```
 */
export class RecordingManager {
  private readonly channel: EventChannel;
  private readonly runs: RunManager;
  private current: Active | undefined;
  private readonly ended = new Map<string, { project: Project; file: string; steps: number }>();

  /**
   * @param channel - Renders and sends events to the client, masked like any message.
   * @param runs - The engine's runs: a recording waits for none, and verify starts one.
   */
  constructor(channel: EventChannel, runs: RunManager) {
    this.channel = channel;
    this.runs = runs;
  }

  /** Whether a recording is in progress. */
  get recording(): boolean {
    return this.current !== undefined;
  }

  private send(method: string, params: Record<string, unknown>): void {
    this.channel.send(this.channel.render(method, params));
  }

  /**
   * Opens the recording browser and starts recording; the events follow the answer.
   *
   * @param project - The open project.
   * @param params - The request's parameters.
   * @returns The recording's id and file.
   * @throws RpcError `RecordingInProgress`, `RunInProgress`, `FileExists`,
   *   `ProjectInvalid`, or `InvalidParams` for a file name that is not a test,
   *   an unknown environment or login, or a browser that cannot start.
   */
  async start(project: Project, params: StartRecordingParams): Promise<StartRecordingResult> {
    if (this.current !== undefined) {
      throw new RpcError(
        'RecordingInProgress',
        'A recording is already in progress. Stop it first.',
      );
    }
    if (this.runs.running) {
      throw new RpcError(
        'RunInProgress',
        'A run is in progress. Record once it has finished, or cancel it first.',
      );
    }
    const recordingId = newRecordingId(new Date());
    // Held back until the answer is sent, then sent in order.
    const held: [string, Record<string, unknown>][] = [];
    let live = false;
    const emit = (method: string, eventParams: Record<string, unknown>): void => {
      const full = { recordingId, ...eventParams };
      if (live) this.send(method, full);
      else held.push([method, full]);
    };
    const debugPort = Number(process.env[RECORDING_DEBUG_PORT_VARIABLE]);
    let session: RecordingSession;
    try {
      session = await RecordingSession.start(
        {
          project,
          file: params.file,
          startUrl: params.startUrl,
          environment: params.environment,
          login: params.login,
          name: params.name,
          headless: process.env[RECORDING_HEADLESS_VARIABLE] === '1',
          debugPort: Number.isInteger(debugPort) && debugPort > 0 ? debugPort : undefined,
        },
        {
          stepRecorded: (
            index: number,
            step: RecordedStep,
            targets: Readonly<Record<string, RecordedTarget>>,
          ) => {
            emit('stepRecorded', {
              index,
              step: protocolStep(step),
              targets: Object.fromEntries(
                Object.entries(targets).map(([name, target]) => [name, targetValue(target)]),
              ),
              ...(step.review === undefined ? {} : { review: step.review }),
            });
          },
          stepChanged: (index: number, step: RecordedStep) => {
            emit('stepChanged', {
              index,
              step: protocolStep(step),
              ...(step.review === undefined ? {} : { review: step.review }),
            });
          },
          notice: (notice) => {
            emit('recordingNotice', { ...notice });
          },
        },
      );
    } catch (error) {
      throw toRpcError(error);
    }
    const active: Active = { id: recordingId, project, file: params.file, session };
    this.current = active;
    void session.ended.then((end) => {
      this.finish(active, end);
    });
    // The answer is written right after this handler resolves, before the next macrotask.
    setImmediate(() => {
      this.send('recordingStarted', {
        recordingId,
        file: params.file,
        startUrl: params.startUrl ?? '/',
      });
      for (const [method, eventParams] of held) this.send(method, eventParams);
      live = true;
    });
    return { recordingId, file: params.file };
  }

  /** Sends `recordingStopped` once, and keeps the recording for verify. */
  private finish(active: Active, end: RecordingEnd): void {
    if (this.current !== active) return;
    this.current = undefined;
    const steps = active.session.recorded.length;
    this.ended.set(active.id, { project: active.project, file: active.file, steps });
    this.send('recordingStopped', {
      recordingId: active.id,
      file: active.file,
      reason: end,
      steps,
    });
  }

  /**
   * Stops a recording: the last pending step is written and the browser closes.
   *
   * @param recordingId - The recording.
   * @returns The file and its number of steps.
   * @throws RpcError `RecordingNotFound` for an unknown id.
   */
  async stop(recordingId: string): Promise<StopRecordingResult> {
    const active = this.current;
    if (active?.id === recordingId) {
      await active.session.stop();
      this.finish(active, 'stopped');
    }
    const ended = this.ended.get(recordingId);
    if (ended === undefined) {
      throw new RpcError('RecordingNotFound', `There is no recording "${recordingId}".`);
    }
    return { file: ended.file, steps: ended.steps };
  }

  /**
   * Runs a stopped recording's file with the normal runner; `recordingVerified`
   * follows `runFinished`.
   *
   * @param recordingId - The recording.
   * @returns The run's id and folder, as `startRun` answers.
   * @throws RpcError `RecordingInProgress` while it is still recording,
   *   `RecordingNotFound` for an unknown id, and the errors of `startRun`.
   */
  async verify(recordingId: string): Promise<StartRunResult> {
    if (this.current?.id === recordingId) {
      throw new RpcError(
        'RecordingInProgress',
        'The recording is still in progress. Stop it before verifying it.',
      );
    }
    const ended = this.ended.get(recordingId);
    if (ended === undefined) {
      throw new RpcError('RecordingNotFound', `There is no recording "${recordingId}".`);
    }
    let runId = '';
    const result = await this.runs.start(ended.project, { files: [ended.file] }, (status) => {
      this.send('recordingVerified', {
        recordingId,
        runId,
        status: status === 'passed' ? 'passed' : 'failed',
      });
    });
    runId = result.runId;
    return result;
  }

  /**
   * Stops the recording in progress, if any: on `shutdown`, or when the client
   * goes away.
   *
   * @returns When its browser is closed.
   */
  async stopAll(): Promise<void> {
    const active = this.current;
    if (active !== undefined) await this.stop(active.id);
  }
}

/** The protocol error for a recording that cannot start. */
function toRpcError(error: unknown): unknown {
  if (error instanceof RecordingError) {
    switch (error.code) {
      case 'FileExists':
        return new RpcError('FileExists', error.message);
      case 'ConfigInvalid':
        return new RpcError('ProjectInvalid', error.message);
      default:
        return new RpcError('InvalidParams', error.message);
    }
  }
  if (
    error instanceof Error &&
    /Executable doesn't exist|browserType\.launch/.test(error.message)
  ) {
    return new RpcError(
      'InvalidParams',
      `The recording browser could not start. A visible recording needs the full Chromium; install it with: ${INSTALL_FULL_BROWSER}`,
    );
  }
  return error;
}
