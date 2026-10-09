// Public entry point of the engine package. Engine modules (parser, validator,
// action registry, runner) are added in Milestone 1 once the docs are approved.

import { PROTOCOL_VERSION } from '@cfe/protocol';

/**
 * Identity of this engine build, reported to clients during `initialize`.
 */
export interface EngineInfo {
  /** Package name of the engine. */
  readonly name: string;
  /** Protocol version this engine speaks (see `docs/protocol.md`). */
  readonly protocolVersion: string;
}

/**
 * Returns the identity of this engine build.
 *
 * @returns The engine name and the protocol version it implements.
 *
 * @example
 * ```ts
 * const { protocolVersion } = getEngineInfo();
 * ```
 */
export function getEngineInfo(): EngineInfo {
  return { name: '@cfe/engine', protocolVersion: PROTOCOL_VERSION };
}
