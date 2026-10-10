// Unit test: every built-in action of docs/actions.md has a `run`, so no step
// that calls a built-in fails with NotImplemented (instruction 0008).
import { describe, expect, it } from 'vitest';
import { BUILTIN_SPECS } from '../builtin-specs.js';
import { BUILTIN_ACTIONS } from './index.js';

describe('BUILTIN_ACTIONS', () => {
  it('gives every built-in spec a run, in the order of the specs', () => {
    expect(BUILTIN_ACTIONS.map((action) => action.name)).toEqual(
      BUILTIN_SPECS.map((spec) => spec.name),
    );
    expect(BUILTIN_ACTIONS.filter((action) => !('run' in action)).map((a) => a.name)).toEqual([]);
  });
});
