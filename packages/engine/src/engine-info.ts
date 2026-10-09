// Identity of this engine build, reported to clients in `initialize`.

import { readFileSync } from 'node:fs';
import { PRODUCT, PROTOCOL_VERSION } from '@cfe/protocol';

/**
 * Identity of this engine build.
 */
export interface EngineInfo {
  /** Package name of the engine, for example `@cfe/engine`. */
  readonly name: string;
  /** Version of the engine package. */
  readonly version: string;
  /** Protocol version this engine speaks (see `docs/protocol.md`). */
  readonly protocolVersion: string;
}

function readPackageVersion(): string {
  // Both src/ and dist/ sit one level below the package root.
  const manifest: unknown = JSON.parse(
    readFileSync(new URL('../package.json', import.meta.url), 'utf8'),
  );
  if (
    typeof manifest === 'object' &&
    manifest !== null &&
    'version' in manifest &&
    typeof manifest.version === 'string'
  ) {
    return manifest.version;
  }
  throw new Error('The engine package.json has no "version" field.');
}

/**
 * Returns the identity of this engine build.
 *
 * @returns The engine's package name and version, and the protocol version it
 *   implements.
 * @throws Error when the engine's `package.json` cannot be read; this means
 *   the engine is installed incompletely.
 *
 * @example
 * ```ts
 * const { protocolVersion } = getEngineInfo();
 * ```
 */
export function getEngineInfo(): EngineInfo {
  return {
    name: `${PRODUCT.npmScope}/engine`,
    version: readPackageVersion(),
    protocolVersion: PROTOCOL_VERSION,
  };
}
