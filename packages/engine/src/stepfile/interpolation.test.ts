// Unit tests for finding ${…} references.
import { describe, expect, it } from 'vitest';
import { referencesIn, referencesInValue } from './interpolation.js';

describe('referencesIn', () => {
  it('finds every reference with its namespace and path', () => {
    expect(referencesIn('guest+${row.sku}@x.com/${vars.order.items.0.name}', ['a'])).toEqual([
      { expression: 'row.sku', namespace: 'row', path: ['sku'], at: ['a'] },
      {
        expression: 'vars.order.items.0.name',
        namespace: 'vars',
        path: ['order', 'items', '0', 'name'],
        at: ['a'],
      },
    ]);
  });

  it('skips $${…}, which is a literal', () => {
    expect(referencesIn('cost: $${price} and ${vars.x}', [])).toMatchObject([
      { expression: 'vars.x' },
    ]);
  });

  it('reports a reference without a path', () => {
    expect(referencesIn('${vars}', [])).toEqual([
      { expression: 'vars', namespace: 'vars', path: [], at: [] },
    ]);
  });
});

describe('referencesInValue', () => {
  it('walks lists and mappings and records each value path; keys are not searched', () => {
    const refs = referencesInValue({ a: ['${env.x}', { b: '${secrets.Y}' }], '${not.a.key}': 1 }, [
      'root',
    ]);
    expect(refs.map((ref) => [ref.expression, ref.at])).toEqual([
      ['env.x', ['root', 'a', 0]],
      ['secrets.Y', ['root', 'a', 1, 'b']],
    ]);
  });
});
