// The protocol's own version and the rule that decides whether a client and an
// engine can talk to each other (docs/protocol.md, "Versioning"; ADR 0011).

/**
 * Version of the engine protocol described in `docs/protocol.md`.
 *
 * Clients send the version they speak in `initialize`; the engine refuses a
 * client whose version is not compatible (see {@link isCompatibleProtocol}).
 *
 * @example
 * ```ts
 * import { PROTOCOL_VERSION } from '@cfe/protocol';
 * console.log(`speaking protocol ${PROTOCOL_VERSION}`);
 * ```
 */
export const PROTOCOL_VERSION = '0.1.0';

const SEMVER = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;

/**
 * A protocol version split into its numeric parts.
 */
export interface ProtocolVersionParts {
  /** Major version. */
  readonly major: number;
  /** Minor version. */
  readonly minor: number;
  /** Patch version. */
  readonly patch: number;
}

/**
 * Splits a protocol version of the form `major.minor.patch`.
 *
 * @param version - A version such as `"0.1.0"`. Pre-release and build suffixes
 *   are not part of the protocol's versions and are rejected.
 * @returns The numeric parts, or `undefined` when `version` is not of that form.
 *
 * @example
 * ```ts
 * parseProtocolVersion('1.2.3'); // { major: 1, minor: 2, patch: 3 }
 * parseProtocolVersion('1.2');   // undefined
 * ```
 */
export function parseProtocolVersion(version: string): ProtocolVersionParts | undefined {
  const match = SEMVER.exec(version);
  if (match === null) {
    return undefined;
  }
  const [, major, minor, patch] = match;
  return { major: Number(major), minor: Number(minor), patch: Number(patch) };
}

/**
 * Says whether a client speaking `clientVersion` and an engine speaking
 * `engineVersion` are compatible.
 *
 * The rule from `docs/protocol.md`: from `1.0.0` on, the major versions must be
 * equal; while the version is `0.x`, major **and** minor must be equal, because
 * a `0.x` minor bump may break. A version that is not `major.minor.patch` is
 * never compatible.
 *
 * @param clientVersion - The version the client sent in `initialize`.
 * @param engineVersion - The version the engine speaks, normally
 *   {@link PROTOCOL_VERSION}.
 * @returns `true` when the two may talk to each other.
 *
 * @example
 * ```ts
 * isCompatibleProtocol('0.1.3', '0.1.0'); // true
 * isCompatibleProtocol('0.2.0', '0.1.0'); // false
 * isCompatibleProtocol('1.4.0', '1.0.0'); // true
 * ```
 */
export function isCompatibleProtocol(clientVersion: string, engineVersion: string): boolean {
  const client = parseProtocolVersion(clientVersion);
  const engine = parseProtocolVersion(engineVersion);
  if (client === undefined || engine === undefined) {
    return false;
  }
  if (client.major !== engine.major) {
    return false;
  }
  return engine.major !== 0 || client.minor === engine.minor;
}
