// Turns the bytes arriving on stdin into protocol lines: one JSON message per
// line, UTF-8, with the 4 MiB limit of ADR 0005. Works on bytes, so a
// multi-byte character split across chunks is decoded only once it is whole.

import { MAX_MESSAGE_BYTES } from '@cfe/protocol';

const NEWLINE = 0x0a;
const CARRIAGE_RETURN = 0x0d;

/**
 * What the reader found in the input: a complete line of text, or a line that
 * was longer than the limit and was discarded without being decoded.
 */
export type LineReaderItem = { kind: 'line'; text: string } | { kind: 'tooLarge'; bytes: number };

/**
 * Splits a byte stream into lines.
 *
 * - Lines end with `\n`; a `\r` just before it is dropped, so `\r\n` works.
 * - Empty lines are skipped.
 * - A line whose content (without the line ending) exceeds `maxBytes` is
 *   discarded unparsed and reported as `tooLarge` once its end arrives; the
 *   reader keeps going with the next line.
 *
 * @example
 * ```ts
 * const reader = new LineReader();
 * process.stdin.on('data', (chunk: Buffer) => {
 *   for (const item of reader.push(chunk)) handle(item);
 * });
 * ```
 */
export class LineReader {
  private readonly maxBytes: number;
  private readonly decoder = new TextDecoder('utf-8');
  private pending: Buffer[] = [];
  private pendingBytes = 0;
  private discarding = false;

  /**
   * @param maxBytes - Longest allowed line in bytes, not counting the line
   *   ending. Defaults to the protocol's `MAX_MESSAGE_BYTES`.
   */
  constructor(maxBytes: number = MAX_MESSAGE_BYTES) {
    this.maxBytes = maxBytes;
  }

  /**
   * Feeds the next chunk of input.
   *
   * @param chunk - Bytes as they arrived; may hold part of a line, one line or
   *   many lines.
   * @returns Every line completed by this chunk, in order.
   */
  push(chunk: Uint8Array): LineReaderItem[] {
    const items: LineReaderItem[] = [];
    let start = 0;
    while (start < chunk.length) {
      const newline = chunk.indexOf(NEWLINE, start);
      const end = newline === -1 ? chunk.length : newline;
      this.append(chunk.subarray(start, end));
      if (newline === -1) {
        break;
      }
      const item = this.finishLine();
      if (item !== undefined) {
        items.push(item);
      }
      start = newline + 1;
    }
    return items;
  }

  /**
   * Ends the input. A last line without a final newline is returned too.
   *
   * @returns The last line, if any input was left over.
   */
  end(): LineReaderItem[] {
    const item = this.finishLine();
    return item === undefined ? [] : [item];
  }

  private append(part: Uint8Array): void {
    if (part.length === 0) {
      return;
    }
    this.pendingBytes += part.length;
    if (this.discarding) {
      return;
    }
    // Allow one extra byte for a "\r" that turns out to be part of "\r\n".
    if (this.pendingBytes > this.maxBytes + 1) {
      this.discarding = true;
      this.pending = [];
      return;
    }
    this.pending.push(Buffer.from(part));
  }

  private finishLine(): LineReaderItem | undefined {
    const bytes = this.pendingBytes;
    const discarded = this.discarding;
    const buffer = Buffer.concat(this.pending);
    this.pending = [];
    this.pendingBytes = 0;
    this.discarding = false;

    const hasCarriageReturn = !discarded && buffer.at(-1) === CARRIAGE_RETURN;
    const contentBytes = hasCarriageReturn ? bytes - 1 : bytes;
    if (discarded || contentBytes > this.maxBytes) {
      return { kind: 'tooLarge', bytes: contentBytes };
    }
    if (contentBytes === 0) {
      return undefined;
    }
    const content = hasCarriageReturn ? buffer.subarray(0, buffer.length - 1) : buffer;
    return { kind: 'line', text: this.decoder.decode(content) };
  }
}
