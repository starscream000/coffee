// Keeps stdout for protocol messages only: every console method writes to
// stderr instead (ADR 0005). Installed once, at engine start-up, before any
// other engine or user code runs.

import { formatWithOptions } from 'node:util';

/**
 * Something that accepts text; satisfied by `process.stderr`.
 */
export interface TextSink {
  /** Writes text. */
  write(text: string): unknown;
}

const METHODS = ['log', 'info', 'debug', 'warn', 'error', 'trace'] as const;

/**
 * Redirects `console.log`, `info`, `debug`, `warn`, `error` and `trace` to
 * `sink`, so nothing but protocol messages reaches stdout.
 *
 * @param target - The console object to change; defaults to the global console.
 * @param sink - Where console output goes; defaults to `process.stderr`.
 *
 * @example
 * ```ts
 * redirectConsoleToStderr();
 * console.log('debug output'); // appears on stderr
 * ```
 */
export function redirectConsoleToStderr(
  target: Pick<Console, (typeof METHODS)[number]> = console,
  sink: TextSink = process.stderr,
): void {
  for (const method of METHODS) {
    target[method] = (...args: unknown[]): void => {
      sink.write(`${formatWithOptions({ colors: false }, ...args)}\n`);
    };
  }
}
