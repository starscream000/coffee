// Unit tests for URL patterns at run time: globs (relative to the base URL
// when they start with /) and regular expressions with the regex: prefix.
import { describe, expect, it } from 'vitest';
import { urlMatcher } from './url-pattern.js';

const BASE = 'http://localhost:4310';

describe('urlMatcher', () => {
  it('reads a pattern that starts with / as a glob relative to the base URL', () => {
    const matches = urlMatcher('/orders/*', BASE);
    expect(matches(`${BASE}/orders/12`)).toBe(true);
    expect(matches(`${BASE}/orders/12/items`)).toBe(false);
    expect(matches('http://elsewhere/orders/12')).toBe(false);
    expect(urlMatcher('/orders/', BASE)(`${BASE}/orders/`)).toBe(true);
  });

  it('supports ** across slashes and {a,b} alternatives', () => {
    expect(urlMatcher('**/api/orders*', BASE)(`${BASE}/api/orders?page=2`)).toBe(true);
    expect(urlMatcher('**/{todos,fallback}', BASE)(`${BASE}/fallback`)).toBe(true);
    expect(urlMatcher('**/{todos,fallback}', BASE)(`${BASE}/frames`)).toBe(false);
  });

  it('treats other characters literally, dots and question marks included', () => {
    expect(urlMatcher('**/a.b?c', BASE)(`${BASE}/a.b?c`)).toBe(true);
    expect(urlMatcher('**/a.b?c', BASE)(`${BASE}/aXbYc`)).toBe(false);
  });

  it('reads regex: as a regular expression on the whole URL', () => {
    const matches = urlMatcher('regex:/orders/\\d+$', BASE);
    expect(matches(`${BASE}/orders/12`)).toBe(true);
    expect(matches(`${BASE}/orders/new`)).toBe(false);
  });
});
