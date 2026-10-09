// Unit tests for validating one file: step normalisation, the mapping from a
// schema problem to a line and column, and reporting every problem at once.
import type { Diagnostic } from '@cfe/protocol';
import { describe, expect, it } from 'vitest';
import { BUILTIN_SPECS_BY_NAME } from '../actions/builtin-specs.js';
import type { FileKind } from '../schema/files.js';
import { validateFile } from './validate-file.js';

function check(text: string, kind: FileKind = 'test') {
  return validateFile(`tests/x.${kind}.yaml`, text, kind, BUILTIN_SPECS_BY_NAME);
}

function brief(diagnostics: readonly Diagnostic[]): string[] {
  return diagnostics.map((d) => `${String(d.line)}:${String(d.column)} ${d.code}`);
}

const lines = (...l: string[]) => `${l.join('\n')}\n`;

describe('step normalisation', () => {
  it('brings bare, shorthand and long forms into the canonical long form', () => {
    const result = check(
      lines(
        'version: 1',
        'name: Forms',
        'steps:',
        '  - back',
        '  - reload:',
        '  - goto: /products',
        '  - fill: { target: email, value: a@b.c }',
        '  - click: submit',
        '    page: main',
        '    timeout: 20s',
        '    name: Submit the form',
        '  - set: { total: 3 }',
      ),
    );
    expect(result.diagnostics).toEqual([]);
    expect(result.steps.map(({ action, params, form }) => ({ action, params, form }))).toEqual([
      { action: 'back', params: {}, form: 'bare' },
      { action: 'reload', params: {}, form: 'bare' },
      { action: 'goto', params: { url: '/products' }, form: 'shorthand' },
      { action: 'fill', params: { target: 'email', value: 'a@b.c' }, form: 'long' },
      { action: 'click', params: { target: 'submit' }, form: 'shorthand' },
      { action: 'set', params: { total: 3 }, form: 'long' },
    ]);
    expect(result.steps[4]).toMatchObject({
      page: 'main',
      timeout: '20s',
      name: 'Submit the form',
      path: ['steps', 4],
    });
  });

  it('reports an unknown action with a "did you mean" hint at the right place', () => {
    const result = check(
      lines(
        'version: 1',
        'name: T',
        'steps:',
        '  - goto: /',
        '  - exepct.text: { target: a, equals: "1" }',
      ),
    );
    expect(brief(result.diagnostics)).toEqual(['5:5 UnknownAction']);
    expect(result.diagnostics[0]?.message).toBe('"exepct.text" is not an action.');
    expect(result.diagnostics[0]?.hint).toBe('Did you mean "expect.text"?');
  });

  it('reports a missing parameter at the action, naming the action and the parameter', () => {
    const result = check(
      lines('version: 1', 'name: T', 'steps:', '  - fill:', '      target: email'),
    );
    expect(brief(result.diagnostics)).toEqual(['5:7 MissingParameter']);
    expect(result.diagnostics[0]?.message).toBe('"fill" needs "value".');
  });

  it('reports unknown parameters with a hint, and invalid values', () => {
    const result = check(
      lines(
        'version: 1',
        'name: T',
        'steps:',
        '  - click: { target: go, buton: left }',
        '  - wait.element: { target: x, state: gone }',
      ),
    );
    expect(brief(result.diagnostics)).toEqual(['4:26 UnknownKey', '5:39 InvalidValue']);
    expect(result.diagnostics[0]?.hint).toBe('Did you mean "button"?');
    expect(result.diagnostics[1]?.message).toContain('"visible", "hidden", "attached", "detached"');
  });

  it('reports a step with two actions, no action, or an unknown step key', () => {
    const result = check(
      lines(
        'version: 1',
        'name: T',
        'steps:',
        '  - goto: /a',
        '    back:',
        '  - page: main',
        '  - click: go',
        '    pgae: main',
      ),
    );
    expect(brief(result.diagnostics)).toEqual([
      '5:5 TwoActions',
      '6:5 MissingAction',
      '8:5 UnknownKey',
    ]);
    expect(result.diagnostics[2]?.hint).toBe('Did you mean "page"?');
  });

  it('reports shorthand for an action that has none, and bad common keys', () => {
    const result = check(
      lines(
        'version: 1',
        'name: T',
        'steps:',
        '  - fill: email',
        '  - click: go',
        '    timeout: soon',
      ),
    );
    expect(brief(result.diagnostics)).toEqual(['4:11 NoShorthand', '6:14 InvalidValue']);
    expect(result.diagnostics[1]?.message).toContain('duration');
  });

  it('maps problems inside an inline target to the right candidate', () => {
    const result = check(
      lines(
        'version: 1',
        'name: T',
        'steps:',
        '  - click:',
        '      target:',
        '        - role: button',
        '          name: Go',
        '        - css: a',
        '          testId: b',
      ),
    );
    expect(brief(result.diagnostics)).toEqual(['8:11 InvalidCandidate']);
  });

  it('reports frame together with within', () => {
    const result = check(
      lines(
        'version: 1',
        'name: T',
        'targets:',
        '  card:',
        '    frame: pay',
        '    within: form',
        '    candidates: [{ label: Card }]',
        'steps: [back]',
      ),
    );
    expect(brief(result.diagnostics)).toEqual(['5:5 FrameWithWithin']);
  });

  it('accepts ${…} where a number or boolean is expected', () => {
    const result = check(
      lines(
        'version: 1',
        'name: T',
        'steps:',
        '  - expect.count: { target: rows, equals: "${row.n}" }',
      ),
    );
    expect(result.diagnostics).toEqual([]);
  });
});

describe('file-level checks', () => {
  it('requires version 1 and stops there otherwise', () => {
    expect(brief(check(lines('name: T', 'steps: [back]')).diagnostics)).toEqual(['1:1 MissingKey']);
    expect(brief(check(lines('version: 2', 'name: T', 'nonsense: 1')).diagnostics)).toEqual([
      '1:10 UnsupportedVersion',
    ]);
  });

  it('requires a mapping', () => {
    expect(brief(check('- a\n- b\n').diagnostics)).toEqual(['1:1 InvalidFile']);
  });

  it('returns YAML syntax errors without trying the schema', () => {
    const result = check('version: 1\nsteps: [back\n');
    expect(result.diagnostics.every((d) => d.code === 'YamlSyntax')).toBe(true);
    expect(result.parsed).toBeUndefined();
  });

  it('reports file problems and step problems together', () => {
    const result = check(lines('version: 1', 'stpes: []', 'steps:', '  - clik: go'));
    expect(brief(result.diagnostics)).toEqual([
      '1:1 MissingKey',
      '2:1 UnknownKey',
      '4:5 UnknownAction',
    ]);
    expect(result.diagnostics[1]?.hint).toBe('Did you mean "steps"?');
  });

  it('returns typed data for a valid file of each kind', () => {
    expect(check(lines('version: 1', 'name: T', 'steps: [back]')).parsed?.kind).toBe('test');
    expect(check(lines('version: 1', 'name: F', 'steps: [back]'), 'flow').parsed?.kind).toBe(
      'flow',
    );
    expect(
      check(lines('version: 1', 'targets: { go: [{ css: "#go" }] }'), 'targets').parsed?.kind,
    ).toBe('targets');
    expect(
      check(
        lines('version: 1', 'environments: { local: { baseUrl: "http://localhost:1" } }'),
        'config',
      ).parsed?.kind,
    ).toBe('config');
  });

  it('rejects before, after and data in a flow', () => {
    const result = check(
      lines('version: 1', 'name: F', 'data: [{ a: 1 }]', 'steps: [back]'),
      'flow',
    );
    expect(brief(result.diagnostics)).toEqual(['3:1 UnknownKey']);
  });
});

describe('review 0003 fixes', () => {
  it('finding 5: regular expressions must compile, and extract needs one group', () => {
    const result = check(
      lines(
        'version: 1',
        'name: T',
        'steps:',
        "  - expect.text: { target: a, matches: '(' }",
        "  - wait.url: '/[/'",
        "  - extract: { target: a, as: n, pattern: '(a)(b)' }",
        "  - extract: { target: a, as: n, pattern: 'Order ([0-9]+)' }",
        "  - expect.url: { matches: '${vars.pattern}' }",
        "  - wait.url: '**/orders/*'",
      ),
    );
    expect(brief(result.diagnostics)).toEqual([
      '4:40 InvalidRegex',
      '5:15 InvalidRegex',
      '6:43 InvalidRegex',
    ]);
    expect(result.diagnostics[2]?.message).toContain('exactly one capturing group');
  });

  it('finding 6: a duration given as a number says it must be a duration', () => {
    const result = check(
      lines('version: 1', 'name: T', 'steps:', '  - click: go', '    timeout: 10'),
    );
    expect(result.diagnostics[0]?.message).toBe(
      '"timeout" must be a duration such as 500ms, 10s, 2m or 12h.',
    );
  });

  it('finding 6: a long-form target written straight after the action gets the target: hint', () => {
    const result = check(
      lines('version: 1', 'name: T', 'steps:', '  - click: { candidates: [{ css: a }] }'),
    );
    expect(brief(result.diagnostics)).toEqual(['4:12 MissingParameter', '4:14 UnknownKey']);
    expect(result.diagnostics[1]?.hint).toBe(
      'An inline target goes under "target:", for example: click: { target: { candidates: [ … ] } }',
    );
  });
});
