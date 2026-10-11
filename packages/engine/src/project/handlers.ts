// Registers the project requests on a session: `openProject` opens (or
// replaces) the one project of this engine process, `validate` checks files in
// it, `listTests` and `listActions` describe tests and actions, `startRun` runs tests,
// and `startRecording`, `stopRecording` and `verifyRecording` record them
// (docs/protocol.md, "Requests"). A run and a recording never overlap.

import type { ActionSummary } from '@cfe/protocol';
import { z } from 'zod';
import type { SecretRegistry } from '../context/mask.js';
import { ActionRegistry, type BuiltinAction, type RegisteredAction } from '../actions/registry.js';
import { RpcError } from '../rpc/rpc-error.js';
import type { RecordingManager } from '../recorder/manager.js';
import type { RunManager } from '../runner/run.js';
import type { Session } from '../rpc/session.js';
import { Project } from './project.js';

/**
 * Describes an action for `listActions`, with its parameters as JSON Schema.
 *
 * @param action - A registered action.
 * @returns The protocol's action summary.
 */
export function summariseAction(action: RegisteredAction): ActionSummary {
  let paramsSchema: Record<string, unknown>;
  try {
    paramsSchema = z.toJSONSchema(action.spec.params, { unrepresentable: 'any' });
  } catch {
    // A user action built with a different Zod copy may not convert; say nothing rather than fail.
    paramsSchema = {};
  }
  return {
    name: action.spec.name,
    description: action.spec.description,
    shorthand: action.spec.shorthand ?? null,
    paramsSchema,
    source:
      action.source.kind === 'builtin'
        ? { kind: 'builtin' }
        : { kind: 'file', file: action.source.file },
  };
}

/**
 * Adds `openProject`, `validate` and `listActions` to a session. Only one
 * project is open at a time; opening another replaces it.
 *
 * @param session - The protocol session.
 * @param builtins - The built-in action specs.
 * @param secrets - The engine-wide secret registry the message writer masks with.
 * @param runs - Starts runs; with it, `startRun` is handled too.
 * @param recordings - Starts recordings; with it, the recording requests are handled too.
 *
 * @example
 * ```ts
 * registerProjectHandlers(session, BUILTIN_SPECS);
 * ```
 */
export function registerProjectHandlers(
  session: Session,
  builtins: readonly BuiltinAction[],
  secrets: SecretRegistry,
  runs?: RunManager,
  recordings?: RecordingManager,
): void {
  let project: Project | undefined;
  const refuseWhileRecording = (what: string): void => {
    if (recordings?.recording === true) {
      throw new RpcError(
        'RecordingInProgress',
        `A recording is in progress; ${what} once it has been stopped.`,
      );
    }
  };

  session.register('openProject', async (params) => {
    refuseWhileRecording('open another project');
    if (runs?.running === true) {
      throw new RpcError(
        'RunInProgress',
        'A run is in progress; open another project once it has finished.',
      );
    }
    // A newly opened project brings its own secrets; forget the previous ones.
    secrets.clear();
    project = await Project.open(params.root, builtins, { secrets });
    return project.summary();
  });

  session.register('validate', (params) => {
    if (project === undefined) {
      return Promise.reject(
        new RpcError(
          'ProjectNotOpen',
          'Open a project with "openProject" before validating files.',
        ),
      );
    }
    return Promise.resolve({ diagnostics: project.validate(params) });
  });

  if (runs !== undefined) {
    session.register('startRun', (params) => {
      refuseWhileRecording('start a run');
      if (project === undefined) {
        return Promise.reject(
          new RpcError(
            'ProjectNotOpen',
            'Open a project with "openProject" before starting a run.',
          ),
        );
      }
      return runs.start(project, params);
    });
    session.register('cancelRun', (params) => {
      runs.cancel(params.runId);
      return Promise.resolve(null);
    });
  }

  if (recordings !== undefined) {
    session.register('startRecording', (params) => {
      if (project === undefined) {
        return Promise.reject(
          new RpcError(
            'ProjectNotOpen',
            'Open a project with "openProject" before starting a recording.',
          ),
        );
      }
      return recordings.start(project, params);
    });
    session.register('stopRecording', (params) => recordings.stop(params.recordingId));
    session.register('verifyRecording', (params) => recordings.verify(params.recordingId));
  }

  session.register('listTests', (params) => {
    if (project === undefined) {
      return Promise.reject(
        new RpcError('ProjectNotOpen', 'Open a project with "openProject" before listing tests.'),
      );
    }
    return Promise.resolve({ tests: project.listTests(params.tags) });
  });

  session.register('listActions', () => {
    // Without an open project there are no user actions yet: list the built-ins.
    const actions = (project?.registry ?? new ActionRegistry(builtins)).list();
    return Promise.resolve({ actions: actions.map(summariseAction) });
  });
}
