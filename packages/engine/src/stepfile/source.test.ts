// Unit tests for YAML parsing with positions.
import { describe, expect, it } from 'vitest';
import { parseSource } from './source.js';

const TEXT = [
  'version: 1', // 1
  'name: Example', // 2
  'steps:', // 3
  '  - goto: /home', // 4
  '  - fill:', // 5
  '      target: email', // 6
  '      value: x', // 7
  '  - back', // 8
  '',
].join('\n');

describe('parseSource', () => {
  const source = parseSource('tests/a.test.yaml', TEXT);

  it('returns the data as plain values', () => {
    expect(source.syntaxErrors).toEqual([]);
    expect(source.data).toEqual({
      version: 1,
      name: 'Example',
      steps: [{ goto: '/home' }, { fill: { target: 'email', value: 'x' } }, 'back'],
    });
  });

  it('finds the position of values and keys', () => {
    expect(source.positionOf(['name'])).toEqual({ line: 2, column: 7 });
    expect(source.positionOf(['name'], true)).toEqual({ line: 2, column: 1 });
    expect(source.positionOf(['steps', 1])).toEqual({ line: 5, column: 5 });
    expect(source.positionOf(['steps', 1, 'fill'])).toEqual({ line: 6, column: 7 });
    expect(source.positionOf(['steps', 1, 'fill', 'value'])).toEqual({ line: 7, column: 14 });
    expect(source.positionOf(['steps', 2])).toEqual({ line: 8, column: 5 });
  });

  it('falls back to the deepest existing node for a missing path', () => {
    expect(source.positionOf(['steps', 1, 'fill', 'missing'])).toEqual({ line: 6, column: 7 });
    expect(source.positionOf(['nothing', 'here'])).toEqual({ line: 1, column: 1 });
  });

  it('points at the key of an action written without a value', () => {
    const bare = parseSource('t.test.yaml', 'steps:\n  - back:\n');
    expect(bare.positionOf(['steps', 0, 'back'])).toEqual({ line: 2, column: 5 });
  });

  it('reports YAML syntax errors with line and column instead of throwing', () => {
    const broken = parseSource('t.test.yaml', 'version: 1\nsteps: [a, b\nname: x\n');
    expect(broken.data).toBeUndefined();
    expect(broken.syntaxErrors.length).toBeGreaterThan(0);
    for (const error of broken.syntaxErrors) {
      expect(error).toMatchObject({ file: 't.test.yaml', severity: 'error', code: 'YamlSyntax' });
      expect(error.line).toBeGreaterThanOrEqual(2);
    }
  });

  it('reports a duplicate key as a syntax error', () => {
    const duplicate = parseSource('t.test.yaml', 'name: a\nname: b\n');
    expect(duplicate.syntaxErrors).toMatchObject([{ line: 2, column: 1, code: 'YamlSyntax' }]);
  });
});
