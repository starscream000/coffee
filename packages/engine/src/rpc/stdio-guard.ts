// stdout and stderr belong to the engine (review 0004's accepted suggestion;
// review 0005, finding 6). The message writer keeps the only handle to the
// real stdout; anything else written to stdout, by any code, goes to stderr
// instead. Every write to stderr, including direct `process.stderr.write`
// calls from user actions, passes through `mask` first. Installed once, at
// engine start-up, before any user code runs.

import type { LineSink } from './message-writer.js';

type WriteCallback = (error?: Error | null) => void;

/** A chunk written to a stream as text; bytes are read as UTF-8. */
function asText(chunk: unknown): string {
  if (typeof chunk === 'string') return chunk;
  if (chunk instanceof Uint8Array) return Buffer.from(chunk).toString('utf8');
  return String(chunk);
}

function callbackOf(rest: readonly unknown[]): WriteCallback | undefined {
  return rest.find((item): item is WriteCallback => typeof item === 'function');
}

/**
 * Takes over the process's stdout and stderr.
 *
 * @param mask - Masks secrets in text.
 * @returns The sink for protocol messages: the real stdout.
 *
 * @example
 * ```ts
 * const sink = guardStdio((text) => secrets.mask(text));
 * const writer = new MessageWriter(sink, { mask });
 * ```
 */
export function guardStdio(mask: (text: string) => string): LineSink {
  const stdoutWrite = process.stdout.write.bind(process.stdout);
  const stderrWrite = process.stderr.write.bind(process.stderr);

  const maskedStderrWrite = (chunk: unknown, ...rest: unknown[]): boolean =>
    stderrWrite(mask(asText(chunk)), callbackOf(rest));
  process.stderr.write = maskedStderrWrite;
  // Only the message writer may write to stdout; everything else goes to stderr.
  process.stdout.write = maskedStderrWrite;

  return {
    write: (text: string, callback?: WriteCallback): boolean => stdoutWrite(text, callback),
  };
}
