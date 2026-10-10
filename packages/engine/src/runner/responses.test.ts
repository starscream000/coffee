// Unit tests for the response log: since() keeps the responses that arrived
// at or after a time, oldest first.
import type { Response } from 'playwright';
import { describe, expect, it, vi } from 'vitest';
import { ResponseLog } from './responses.js';

describe('ResponseLog', () => {
  it('gives the responses since a time, oldest first', () => {
    const now = vi.spyOn(performance, 'now');
    const log = new ResponseLog();
    const response = (url: string): Response => ({ url }) as unknown as Response;
    const first = response('a');
    const second = response('b');
    const third = response('c');
    now.mockReturnValue(100);
    log.add(first);
    now.mockReturnValue(200);
    log.add(second);
    now.mockReturnValue(300);
    log.add(third);
    now.mockRestore();
    expect(log.since(200).map((entry) => entry.response)).toEqual([second, third]);
    expect(log.since(301)).toEqual([]);
  });
});
