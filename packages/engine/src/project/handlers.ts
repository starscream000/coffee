// Registers the project requests on a session: `openProject` opens (or
// replaces) the one project of this engine process, and `validate` checks
// files in it (docs/protocol.md, "Requests").

import type { ActionSpec } from '../actions/action-spec.js';
import { RpcError } from '../rpc/rpc-error.js';
import type { Session } from '../rpc/session.js';
import { Project } from './project.js';

/**
 * Adds `openProject` and `validate` to a session. Only one project is open at
 * a time; opening another replaces it.
 *
 * @param session - The protocol session.
 * @param actions - Every known action by name (built-ins only until user
 *   actions are loaded in plan branch 4).
 *
 * @example
 * ```ts
 * registerProjectHandlers(session, BUILTIN_SPECS_BY_NAME);
 * ```
 */
export function registerProjectHandlers(
  session: Session,
  actions: ReadonlyMap<string, ActionSpec>,
): void {
  let project: Project | undefined;

  session.register('openProject', (params) => {
    project = Project.open(params.root, actions);
    return Promise.resolve(project.summary());
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
}
