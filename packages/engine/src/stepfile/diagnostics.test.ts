// Unit tests for "did you mean" suggestions.
import { describe, expect, it } from 'vitest';
import { didYouMean, didYouMeanHint, editDistance } from './diagnostics.js';

describe('editDistance', () => {
  it.each([
    ['', '', 0],
    ['abc', 'abc', 0],
    ['abc', 'abd', 1],
    ['abc', 'acb', 1], // swap
    ['exepct', 'expect', 1],
    ['kitten', 'sitting', 3],
  ])('%j → %j is %d', (a, b, distance) => {
    expect(editDistance(a, b)).toBe(distance);
  });
});

describe('didYouMean', () => {
  const actions = ['click', 'expect.text', 'expect.url', 'fill', 'goto'];

  it('suggests a close match', () => {
    expect(didYouMean('exepct.text', actions)).toBe('expect.text');
    expect(didYouMean('clik', actions)).toBe('click');
    expect(didYouMean('Goto', actions)).toBe('goto');
  });

  it('suggests nothing when no word is close', () => {
    expect(didYouMean('navigate', actions)).toBeUndefined();
    expect(didYouMean('x', actions)).toBeUndefined();
  });

  it('builds a hint', () => {
    expect(didYouMeanHint('fil', actions)).toBe('Did you mean "fill"?');
    expect(didYouMeanHint('zzzz', actions)).toBeUndefined();
  });
});
