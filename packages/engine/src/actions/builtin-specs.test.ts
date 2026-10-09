// Unit tests for the built-in action specs: the list of names is fixed, so an
// action cannot be dropped by accident, and every spec is well formed.
import { describe, expect, it } from 'vitest';
import { z } from 'zod';
import { paramKeys } from './action-spec.js';
import { BUILTIN_SPECS, BUILTIN_SPECS_BY_NAME } from './builtin-specs.js';

describe('BUILTIN_SPECS', () => {
  it('holds exactly the built-in actions of docs/actions.md', () => {
    expect(BUILTIN_SPECS.map((spec) => spec.name)).toEqual([
      'goto',
      'back',
      'reload',
      'click',
      'fill',
      'select',
      'check',
      'hover',
      'press',
      'upload',
      'drag',
      'wait.element',
      'wait.url',
      'wait.response',
      'expect.visible',
      'expect.text',
      'expect.value',
      'expect.url',
      'expect.count',
      'expect.response',
      'set',
      'extract',
      'api',
      'mock',
      'call',
    ]);
    expect(BUILTIN_SPECS_BY_NAME.size).toBe(BUILTIN_SPECS.length);
  });

  it.each(BUILTIN_SPECS.map((spec) => [spec.name, spec] as const))(
    '%s is well formed',
    (_name, spec) => {
      expect(spec.description).toMatch(/^[A-Z].*\.$/);
      if (spec.shorthand !== undefined) {
        expect(paramKeys(spec)).toContain(spec.shorthand);
      }
      // Every spec must convert to JSON Schema for listActions.
      expect(() => z.toJSONSchema(spec.params)).not.toThrow();
    },
  );

  it('uses the documented shorthands', () => {
    const shorthands = Object.fromEntries(
      BUILTIN_SPECS.filter((spec) => spec.shorthand !== undefined).map((spec) => [
        spec.name,
        spec.shorthand,
      ]),
    );
    expect(shorthands).toEqual({
      goto: 'url',
      click: 'target',
      check: 'target',
      hover: 'target',
      press: 'key',
      'wait.element': 'target',
      'wait.url': 'url',
      'wait.response': 'url',
      'expect.visible': 'target',
      'expect.url': 'equals',
      call: 'flow',
    });
  });
});
