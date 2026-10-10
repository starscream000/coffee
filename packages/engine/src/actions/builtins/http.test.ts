// Unit tests for the parts of the HTTP actions that need no browser: the
// partial deep match of expect.response and the values of sensitive headers.
import { describe, expect, it } from 'vitest';
import { partialMatch, sensitiveValues } from './http.js';

describe('partialMatch', () => {
  it('matches objects key by key, ignoring keys it does not name', () => {
    expect(partialMatch({ id: 7 }, { id: 7, item: 'Grinder' })).toBe(true);
    expect(partialMatch({ id: 7 }, { id: 8 })).toBe(false);
    expect(partialMatch({ id: 7 }, { item: 'Grinder' })).toBe(false);
    expect(partialMatch({ a: { b: 1 } }, { a: { b: 1, c: 2 } })).toBe(true);
  });

  it('matches lists element by element, with the same length', () => {
    expect(partialMatch([{ id: 1 }, { id: 2 }], [{ id: 1, x: 0 }, { id: 2 }])).toBe(true);
    expect(partialMatch([{ id: 1 }], [{ id: 1 }, { id: 2 }])).toBe(false);
    expect(partialMatch([], {})).toBe(false);
  });

  it('compares other values exactly', () => {
    expect(partialMatch('3', 3)).toBe(false);
    expect(partialMatch(null, null)).toBe(true);
    expect(partialMatch({}, null)).toBe(false);
    expect(partialMatch({}, [])).toBe(false);
  });
});

describe('sensitiveValues', () => {
  it('gives the whole Authorization value and the credential after its scheme', () => {
    expect(sensitiveValues('authorization', 'Bearer abc123')).toEqual(['Bearer abc123', 'abc123']);
    expect(sensitiveValues('authorization', 'opaque-token')).toEqual(['opaque-token']);
  });

  it("gives each cookie's value from Cookie and Set-Cookie", () => {
    expect(sensitiveValues('cookie', 'session=s3cr3t; theme=dark')).toEqual(['s3cr3t', 'dark']);
    expect(
      sensitiveValues('set-cookie', 'session=s3cr3t; Path=/; HttpOnly\nlang=en-GB; Path=/'),
    ).toEqual(['s3cr3t', 'en-GB']);
  });
});
