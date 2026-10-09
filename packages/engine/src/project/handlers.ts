// Registers the project requests on a session: `openProject` opens (or
// replaces) the one project of this engine process, `validate` checks files in
// it, and `listActions` describes every action (docs/protocol.md, "Requests").

import type { ActionSummary } from '@cfe/protocol';
import { z } from 'zod';
import type { ActionSpec } from '../actions/action-spec.js';
import type { RegisteredAction } from '../actions/registry.js';
import { RpcError } from '../rpc/rpc-error.js';
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
 *
 * @example
 * ```ts
 * registerProjectHandlers(session, BUILTIN_SPECS);
 * ```
 */
export function registerProjectHandlers(session: Session, builtins: readonly ActionSpec[]): void {
  let project: Project | undefined;

  session.register('openProject', async (params) => {
    project = await Project.open(params.root, builtins);
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

  session.register('listActions', () => {
    // Without an open project there are no user actions yet: list the built-ins.
    const actions: RegisteredAction[] =
      project?.registry.list().slice() ??
      builtins.map((spec) => ({ spec, run: undefined, source: { kind: 'builtin' } as const }));
    return Promise.resolve({ actions: actions.map(summariseAction) });
  });
}
