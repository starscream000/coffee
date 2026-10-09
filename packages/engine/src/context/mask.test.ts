// Unit tests for the secret registry and mask().
import { describe, expect, it } from 'vitest';
import { MASK, SecretRegistry, encodedForms } from './mask.js';

const SECRET = 'p@ss "word" <&> it\'s';

describe('encodedForms', () => {
  it('lists the value as is, JSON-escaped, URL-encoded, form-encoded and HTML-escaped', () => {
    expect(encodedForms(SECRET)).toEqual([
      SECRET,
      'p@ss \\"word\\" <&> it\'s',
      "p%40ss%20%22word%22%20%3C%26%3E%20it's",
      'p%40ss+%22word%22+%3C%26%3E+it%27s',
      'p@ss &quot;word&quot; &lt;&amp;&gt; it&#39;s',
    ]);
  });
});

describe('SecretRegistry', () => {
  it('masks every encoded form of a secret', () => {
    const registry = new SecretRegistry();
    expect(registry.register(SECRET)).toBe(true);
    for (const form of encodedForms(SECRET)) {
      expect(registry.mask(`before ${form} after`)).toBe(`before ${MASK} after`);
    }
  });

  it('masks a secret inside a longer value', () => {
    const registry = new SecretRegistry();
    registry.register('tok-1234-abcd');
    expect(registry.mask('Authorization: Bearer tok-1234-abcd')).toBe(
      `Authorization: Bearer ${MASK}`,
    );
  });

  it('rejects values shorter than 4 characters and leaves text alone', () => {
    const registry = new SecretRegistry();
    expect(registry.register('abc')).toBe(false);
    expect(registry.size).toBe(0);
    expect(registry.mask('abc abcd')).toBe('abc abcd');
    expect(registry.register('abcd')).toBe(true);
    expect(registry.mask('abc abcd')).toBe(`abc ${MASK}`);
  });

  it('replaces the longer of two overlapping secrets first', () => {
    const registry = new SecretRegistry();
    registry.register('abcd');
    registry.register('bcdefg');
    expect(registry.mask('xabcdefgx')).toBe(`xa${MASK}x`);
    expect(registry.mask('abcd')).toBe(MASK);
  });

  it('accepts values at run time and can be cleared', () => {
    const registry = new SecretRegistry();
    expect(registry.mask('session=s3ss10n')).toBe('session=s3ss10n');
    registry.register('s3ss10n');
    expect(registry.mask('session=s3ss10n')).toBe(`session=${MASK}`);
    registry.clear();
    expect(registry.mask('session=s3ss10n')).toBe('session=s3ss10n');
  });
});
