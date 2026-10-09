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
 * A stream that masks secrets in everything written to it, then writes it to
 * stderr (ADR 0014: stderr is an exit point too).
 *
 * @param mask - Masks secrets in text.
 * @param target - Where masked text goes; defaults to `process.stderr`.
 * @returns A writable stream for {@link redirectConsoleToStderr}.
 */
export function maskingStream(
  mask: (text: string) => string,
  target: NodeJS.WritableStream = process.stderr,
): ConsoleStream {
  // Not a stream.Writable: that queues chunks behind a tick, so lines could
  // reach stderr after a response sent later. Each chunk is handed on at once.
  return Object.assign(Object.create(target) as ConsoleStream, {
    write(chunk: Uint8Array | string, ...rest: unknown[]): boolean {
      const text = typeof chunk === 'string' ? chunk : Buffer.from(chunk).toString('utf8');
      const callback = rest.find((item) => typeof item === 'function') as
        ((error?: Error | null) => void) | undefined;
      return target.write(mask(text), callback);
    },
  });
}

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
