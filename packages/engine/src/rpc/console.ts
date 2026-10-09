// Keeps stdout for protocol messages only (ADR 0005): the global console is
// replaced by one whose output and error streams are both stderr, so no
// console method (log, dir, table, group, count, time, trace, assert, …) can
// write to stdout. Installed once, at engine start-up, before any other engine
// or user code runs.

import { Console } from 'node:console';

/**
 * Something a console can write to; satisfied by `process.stderr`.
 */
export type ConsoleStream = NodeJS.WritableStream;

/**
 * Builds a console whose every method writes to `stream`.
 *
 * @param stream - Where all console output goes.
 * @returns A new console, without colours.
 */
export function createStderrConsole(stream: ConsoleStream = process.stderr): Console {
  return new Console({ stdout: stream, stderr: stream, colorMode: false });
}

/**
 * Replaces the global `console` with one that writes only to `stream`, so
 * nothing but protocol messages reaches stdout.
 *
 * @param stream - Where console output goes; defaults to `process.stderr`.
 *
 * @example
 * ```ts
 * redirectConsoleToStderr();
 * console.dir({ a: 1 }); // appears on stderr
 * ```
 */
export function redirectConsoleToStderr(stream: ConsoleStream = process.stderr): void {
  globalThis.console = createStderrConsole(stream);
}
