// Unit tests for the message writer: one line per message, the masking hook,
// truncation and the replacement of messages that stay too large.
import { describe, expect, it } from 'vitest';
import { MessageWriter, truncateStrings, type LineSink } from './message-writer.js';

class MemorySink implements LineSink {
  readonly writes: string[] = [];

  write(text: string, callback?: (error?: Error | null) => void): boolean {
    this.writes.push(text);
    callback?.();
    return true;
  }
}

function parseLine(line: string): Record<string, unknown> {
  return JSON.parse(line) as Record<string, unknown>;
}

describe('MessageWriter', () => {
  it('writes each message as one JSON line', async () => {
    const sink = new MemorySink();
    const writer = new MessageWriter(sink);
    await writer.send({ jsonrpc: '2.0', id: 1, result: { text: 'line\nbreak' } });
    await writer.send({ jsonrpc: '2.0', id: 2, result: null });
    expect(sink.writes).toHaveLength(2);
    for (const write of sink.writes) {
      expect(write.endsWith('\n')).toBe(true);
      expect(write.slice(0, -1)).not.toContain('\n');
    }
    expect(parseLine(sink.writes[0] ?? '')).toEqual({
      jsonrpc: '2.0',
      id: 1,
      result: { text: 'line\nbreak' },
    });
  });

  it('passes every message through the masking hook', async () => {
    const sink = new MemorySink();
    const writer = new MessageWriter(sink, { mask: (text) => text.replaceAll('hunter2', '•••') });
    await writer.send({ jsonrpc: '2.0', id: 1, result: { password: 'hunter2' } });
    expect(sink.writes[0]).toContain('•••');
    expect(sink.writes[0]).not.toContain('hunter2');
  });

  it('truncates long strings when a message is over the limit', () => {
    const writer = new MessageWriter(new MemorySink(), { maxBytes: 2_000, fieldBytes: 200 });
    const line = writer.render({
      jsonrpc: '2.0',
      method: 'stepFailed',
      params: {
        runId: 'r',
        seq: 1,
        error: { code: 'AssertionFailed', message: 'm', actual: 'a'.repeat(5_000) },
      },
    });
    expect(Buffer.byteLength(line)).toBeLessThanOrEqual(2_000);
    const actual = (parseLine(line).params as { error: { actual: string } }).error.actual;
    expect(Buffer.byteLength(actual)).toBeLessThanOrEqual(200);
    expect(actual).toMatch(/… \[truncated \d+ characters\]$/);
    const removed = Number(/truncated (\d+) characters/.exec(actual)?.[1]);
    expect(actual.length - actual.indexOf('…') + removed).toBeGreaterThan(0);
    expect(actual.indexOf('…') + removed).toBe(5_000);
  });

  it('leaves a message under the limit unchanged', () => {
    const writer = new MessageWriter(new MemorySink(), { maxBytes: 2_000, fieldBytes: 10 });
    const message = { jsonrpc: '2.0', id: 1, result: { text: 'x'.repeat(100) } };
    expect(writer.render(message)).toBe(JSON.stringify(message));
  });

  it('replaces a response that stays too large with MessageTooLarge for the same id', () => {
    const writer = new MessageWriter(new MemorySink(), { maxBytes: 500, fieldBytes: 100 });
    const many = Array.from({ length: 100 }, (_, i) => `item ${String(i)}`);
    const line = writer.render({ jsonrpc: '2.0', id: 7, result: { many } });
    expect(parseLine(line)).toMatchObject({
      jsonrpc: '2.0',
      id: 7,
      error: { code: -32009, data: { name: 'MessageTooLarge' } },
    });
  });

  it('replaces an event that stays too large with an error log event naming it', () => {
    const writer = new MessageWriter(new MemorySink(), { maxBytes: 600, fieldBytes: 100 });
    const locators = Array.from({ length: 50 }, (_, i) => ({
      param: `p${String(i)}`,
      candidateIndex: 0,
      candidate: null,
    }));
    const line = writer.render({
      jsonrpc: '2.0',
      method: 'stepPassed',
      params: {
        runId: 'r1',
        seq: 4,
        testId: 't#0',
        stepId: 'steps.2',
        durationMs: 1,
        locators,
        snapshot: { state: 'saved' },
      },
    });
    const parsed = parseLine(line);
    expect(parsed).toMatchObject({
      method: 'log',
      params: {
        runId: 'r1',
        seq: 4,
        level: 'error',
        code: 'MessageTooLarge',
        testId: 't#0',
        stepId: 'steps.2',
      },
    });
    expect((parsed.params as { message: string }).message).toContain('"stepPassed"');
  });
});

describe('truncateStrings', () => {
  it('never splits a surrogate pair', () => {
    const text = '😀'.repeat(100);
    const out = truncateStrings(text, 60) as string;
    const kept = out.slice(0, out.indexOf('…'));
    expect(kept.length % 2).toBe(0);
    expect(/^(?:😀)+$/u.test(kept)).toBe(true);
    expect(Buffer.byteLength(out)).toBeLessThanOrEqual(60);
  });

  it('truncates strings inside arrays and objects and leaves other values alone', () => {
    const out = truncateStrings(
      { a: ['y'.repeat(500), 3, true, null], b: { c: 'short' } },
      100,
    ) as {
      a: [string, number, boolean, null];
      b: { c: string };
    };
    expect(Buffer.byteLength(out.a[0])).toBeLessThanOrEqual(100);
    expect(out.a.slice(1)).toEqual([3, true, null]);
    expect(out.b.c).toBe('short');
  });
});
