// Unit tests for the line reader: chunking, UTF-8, line endings and the size limit.
import { describe, expect, it } from 'vitest';
import { LineReader, type LineReaderItem } from './line-reader.js';

const bytes = (text: string): Buffer => Buffer.from(text, 'utf8');

function feed(reader: LineReader, ...chunks: Uint8Array[]): LineReaderItem[] {
  return chunks.flatMap((chunk) => reader.push(chunk));
}

describe('LineReader', () => {
  it('reads one line per message', () => {
    expect(feed(new LineReader(), bytes('{"a":1}\n'))).toEqual([{ kind: 'line', text: '{"a":1}' }]);
  });

  it('joins a line split across chunks', () => {
    const reader = new LineReader();
    expect(feed(reader, bytes('{"a"'), bytes(':1'))).toEqual([]);
    expect(feed(reader, bytes('}\n'))).toEqual([{ kind: 'line', text: '{"a":1}' }]);
  });

  it('splits several lines that arrive in one chunk', () => {
    expect(feed(new LineReader(), bytes('{"a":1}\n{"b":2}\n{"c"'))).toEqual([
      { kind: 'line', text: '{"a":1}' },
      { kind: 'line', text: '{"b":2}' },
    ]);
  });

  it('decodes a multi-byte character split across chunks', () => {
    const full = bytes('{"s":"€ 😀"}\n');
    const reader = new LineReader();
    // Split inside the 3-byte "€" and inside the 4-byte emoji.
    const euroStart = full.indexOf(0xe2);
    const emojiStart = full.indexOf(0xf0);
    const items = feed(
      reader,
      full.subarray(0, euroStart + 1),
      full.subarray(euroStart + 1, emojiStart + 2),
      full.subarray(emojiStart + 2),
    );
    expect(items).toEqual([{ kind: 'line', text: '{"s":"€ 😀"}' }]);
  });

  it('accepts lines ending in \\r\\n, also when \\r and \\n arrive separately', () => {
    const reader = new LineReader();
    expect(feed(reader, bytes('{"a":1}\r\n'))).toEqual([{ kind: 'line', text: '{"a":1}' }]);
    expect(feed(reader, bytes('{"b":2}\r'), bytes('\n'))).toEqual([
      { kind: 'line', text: '{"b":2}' },
    ]);
  });

  it('skips empty lines', () => {
    expect(feed(new LineReader(), bytes('\n\r\n{"a":1}\n\n'))).toEqual([
      { kind: 'line', text: '{"a":1}' },
    ]);
  });

  it('accepts a line of exactly the limit, with or without \\r', () => {
    const reader = new LineReader(10);
    expect(feed(reader, bytes(`${'x'.repeat(10)}\n`))).toEqual([
      { kind: 'line', text: 'x'.repeat(10) },
    ]);
    expect(feed(reader, bytes(`${'y'.repeat(10)}\r\n`))).toEqual([
      { kind: 'line', text: 'y'.repeat(10) },
    ]);
  });

  it('reports a line over the limit as tooLarge without decoding it, then keeps reading', () => {
    const reader = new LineReader(10);
    const items = feed(reader, bytes('x'.repeat(6)), bytes('x'.repeat(6)), bytes('xx\n{"ok":1}\n'));
    expect(items).toEqual([
      { kind: 'tooLarge', bytes: 14 },
      { kind: 'line', text: '{"ok":1}' },
    ]);
  });

  it('reports a line one byte over the limit', () => {
    expect(feed(new LineReader(10), bytes(`${'x'.repeat(11)}\n`))).toEqual([
      { kind: 'tooLarge', bytes: 11 },
    ]);
  });

  it('returns a last line without a newline when the input ends', () => {
    const reader = new LineReader();
    expect(feed(reader, bytes('{"a":1}'))).toEqual([]);
    expect(reader.end()).toEqual([{ kind: 'line', text: '{"a":1}' }]);
    expect(reader.end()).toEqual([]);
  });
});
