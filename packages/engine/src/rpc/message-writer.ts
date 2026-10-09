// Writes protocol messages to stdout, one JSON object per line. Each message is
// masked field by field with the protocol's masking rules (ADR 0014), never as
// serialised text, so it stays valid JSON with its identifiers intact; then the
// size rules of ADR 0005 apply: truncate long strings; if still too large,
// replace the message. Masking comes first, so truncation never cuts a secret.

import {
  ERROR_CODES,
  EVENTS,
  MAX_MESSAGE_BYTES,
  TRUNCATED_FIELD_BYTES,
  maskMessage,
  truncationMarker,
} from '@cfe/protocol';

/**
 * Masks secrets in one string. Until secrets exist the engine uses
 * {@link identityMask}.
 */
export type MaskHook = (text: string) => string;

/**
 * A mask that changes nothing.
 *
 * @param text - A string from a message.
 * @returns The same text.
 */
export const identityMask: MaskHook = (text) => text;

/**
 * Where the writer puts its lines; satisfied by `process.stdout`.
 */
export interface LineSink {
  /** Writes text; calls `callback` once it has been handed to the OS. */
  write(text: string, callback?: (error?: Error | null) => void): boolean;
}

/**
 * Options for {@link MessageWriter}.
 */
export interface MessageWriterOptions {
  /**
   * Masks secrets in one string. Applied to the free-text fields of each
   * message, as the protocol's masking rules say; defaults to {@link identityMask}.
   */
  mask?: MaskHook;
  /** Largest line in bytes; defaults to the protocol's `MAX_MESSAGE_BYTES`. */
  maxBytes?: number;
  /** Longest string field in bytes before truncation; defaults to 64 KiB. */
  fieldBytes?: number;
}

type JsonObject = Record<string, unknown>;

/**
 * Serialises protocol messages and writes each as one line.
 *
 * @example
 * ```ts
 * const writer = new MessageWriter(process.stdout);
 * await writer.send({ jsonrpc: '2.0', id: 1, result: null });
 * ```
 */
export class MessageWriter {
  private readonly sink: LineSink;
  private readonly mask: MaskHook;
  private readonly maxBytes: number;
  private readonly fieldBytes: number;

  /**
   * @param sink - Where lines go, normally `process.stdout`.
   * @param options - Mask hook and limits; the defaults follow the protocol.
   */
  constructor(sink: LineSink, options: MessageWriterOptions = {}) {
    this.sink = sink;
    this.mask = options.mask ?? identityMask;
    this.maxBytes = options.maxBytes ?? MAX_MESSAGE_BYTES;
    this.fieldBytes = options.fieldBytes ?? TRUNCATED_FIELD_BYTES;
  }

  /**
   * Sends one message: masks it, enforces the size limit and writes it as a
   * line.
   *
   * @param message - A JSON-RPC response or notification.
   * @returns A promise that resolves once the line has been handed to the sink.
   */
  send(message: JsonObject): Promise<void> {
    return this.sendRendered(this.render(message));
  }

  /**
   * Writes a line that {@link MessageWriter.render} produced, so the caller can
   * keep exactly what was sent (for example in a run's `events.ndjson`).
   *
   * @param line - A rendered message, without the final newline.
   * @returns A promise that resolves once the line has been handed to the sink.
   */
  sendRendered(line: string): Promise<void> {
    return new Promise((resolve, reject) => {
      this.sink.write(`${line}\n`, (error) => {
        if (error) {
          reject(error);
        } else {
          resolve();
        }
      });
    });
  }

  /**
   * Produces the line that {@link MessageWriter.send} would write, without the
   * final newline. Exposed for tests.
   *
   * @param message - A JSON-RPC response or notification.
   * @returns The masked line, never longer than the limit.
   */
  render(message: JsonObject): string {
    // Mask first: a secret cut in half by truncation could no longer be found.
    const masked = maskMessage(message, this.mask) as JsonObject;
    const first = JSON.stringify(masked);
    if (Buffer.byteLength(first) <= this.maxBytes) {
      return first;
    }
    const truncated = JSON.stringify(truncateStrings(masked, this.fieldBytes));
    if (Buffer.byteLength(truncated) <= this.maxBytes) {
      return truncated;
    }
    return JSON.stringify(this.replacementFor(masked));
  }

  private replacementFor(message: JsonObject): JsonObject {
    const method = message.method;
    const params = message.params;
    if (typeof method === 'string' && method in EVENTS && isObject(params)) {
      const testId = typeof params.testId === 'string' ? params.testId : undefined;
      const stepId = typeof params.stepId === 'string' ? params.stepId : undefined;
      const where = [testId && `test ${testId}`, stepId && `step ${stepId}`]
        .filter(Boolean)
        .join(', ');
      return {
        jsonrpc: '2.0',
        method: 'log',
        params: {
          runId: params.runId,
          seq: params.seq,
          level: 'error',
          code: 'MessageTooLarge',
          message: `The engine dropped a "${method}" event${where ? ` (${where})` : ''} because it was larger than ${String(this.maxBytes)} bytes even after truncation.`,
          ...(testId === undefined ? {} : { testId }),
          ...(stepId === undefined ? {} : { stepId }),
        },
      };
    }
    return {
      jsonrpc: '2.0',
      id: 'id' in message ? message.id : null,
      error: {
        code: ERROR_CODES.MessageTooLarge,
        message: `The engine's reply was larger than ${String(this.maxBytes)} bytes even after truncation, so it was not sent.`,
        data: { name: 'MessageTooLarge' },
      },
    };
  }
}

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Returns a copy of `value` in which every string longer than `limitBytes` of
 * UTF-8 is cut to fit and marked with how many characters were removed.
 *
 * @param value - Any JSON value.
 * @param limitBytes - Longest allowed string, in bytes, including the marker.
 * @returns The value with long strings truncated; other values unchanged.
 *
 * @example
 * ```ts
 * truncateStrings({ actual: 'x'.repeat(100_000) }, 65_536);
 * ```
 */
export function truncateStrings(value: unknown, limitBytes: number): unknown {
  if (typeof value === 'string') {
    return truncateString(value, limitBytes);
  }
  if (Array.isArray(value)) {
    return value.map((item) => truncateStrings(item, limitBytes));
  }
  if (isObject(value)) {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, truncateStrings(item, limitBytes)]),
    );
  }
  return value;
}

function countCodePoints(text: string): number {
  let count = 0;
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    if (code >= 0xd800 && code <= 0xdbff && i + 1 < text.length) {
      const next = text.charCodeAt(i + 1);
      if (next >= 0xdc00 && next <= 0xdfff) i++;
    }
    count++;
  }
  return count;
}

function truncateString(text: string, limitBytes: number): string {
  if (Buffer.byteLength(text) <= limitBytes) {
    return text;
  }
  // Reserve room for the longest possible marker.
  const budget = limitBytes - Buffer.byteLength(truncationMarker(text.length));
  let low = 0;
  let high = Math.min(text.length, budget);
  while (low < high) {
    const mid = Math.ceil((low + high) / 2);
    if (Buffer.byteLength(text.slice(0, mid)) <= budget) {
      low = mid;
    } else {
      high = mid - 1;
    }
  }
  let keep = low;
  const last = text.charCodeAt(keep - 1);
  if (keep > 0 && last >= 0xd800 && last <= 0xdbff) {
    keep -= 1; // never split a surrogate pair
  }
  const removed = countCodePoints(text.slice(keep));
  return text.slice(0, keep) + truncationMarker(removed);
}
