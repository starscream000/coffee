// Unit tests for the target and file schemas.
import { describe, expect, it } from 'vitest';
import { ConfigFileSchema, FlowFileSchema, TestFileSchema, fileKindOf } from './files.js';
import { keysAt } from './shape.js';
import { CandidateSchema, TargetSchema } from './targets.js';

const ok = (schema: { safeParse: (v: unknown) => { success: boolean } }, value: unknown) =>
  schema.safeParse(value).success;

describe('CandidateSchema', () => {
  it.each([
    [{ role: 'button', name: 'Save' }],
    [{ role: 'button' }],
    [{ label: 'Email', exact: false }],
    [{ placeholder: 'Search' }],
    [{ text: 'Done', nth: 0 }],
    [{ testId: 'checkout' }],
    [{ css: '#cart .btn' }],
    [{ text: '${row.product}' }],
  ])('accepts %j', (candidate) => {
    expect(ok(CandidateSchema, candidate)).toBe(true);
  });

  it.each([
    [{}, 'no kind'],
    [{ css: 'a', testId: 'b' }, 'two kinds'],
    [{ label: 'x', name: 'y' }, 'name without role'],
    [{ css: 'a', exact: true }, 'exact on css'],
    [{ css: 'a', nth: -1 }, 'negative nth'],
    [{ xpath: '//a' }, 'unknown kind'],
  ])('rejects %j (%s)', (candidate, reason) => {
    expect(ok(CandidateSchema, candidate), reason).toBe(false);
  });
});

describe('TargetSchema', () => {
  it('accepts a name, the short form and the long form', () => {
    expect(ok(TargetSchema, 'cart.count')).toBe(true);
    expect(ok(TargetSchema, [{ role: 'button', name: 'Go' }, { css: '#go' }])).toBe(true);
    expect(ok(TargetSchema, { candidates: [{ label: 'Card' }], frame: 'paymentFrame' })).toBe(true);
    expect(
      ok(TargetSchema, {
        candidates: [{ role: 'button', name: 'Delete' }],
        within: { candidates: [{ role: 'row', name: '${row.product}' }] },
      }),
    ).toBe(true);
  });

  it('rejects frame together with within, empty candidates and bad names', () => {
    expect(ok(TargetSchema, { candidates: [{ css: 'a' }], frame: 'f', within: 'w' })).toBe(false);
    expect(ok(TargetSchema, [])).toBe(false);
    expect(ok(TargetSchema, { candidates: [] })).toBe(false);
    expect(ok(TargetSchema, '1bad')).toBe(false);
  });
});

describe('file schemas', () => {
  it('accepts a minimal test, and a test using every key', () => {
    expect(ok(TestFileSchema, { version: 1, name: 'T', steps: ['back'] })).toBe(true);
    expect(
      ok(TestFileSchema, {
        version: 1,
        name: 'T',
        description: 'd',
        tags: ['smoke'],
        skip: 'broken until SHOP-1',
        login: 'customer',
        freshLogin: true,
        pages: { main: { login: 'customer' }, backoffice: { login: 'admin' } },
        data: './data/rows.csv',
        vars: { email: 'a@b.c' },
        targets: { go: [{ css: '#go' }] },
        before: ['back'],
        steps: ['back'],
        after: ['back'],
      }),
    ).toBe(true);
  });

  it('rejects an empty skip reason, an empty step list and unknown keys', () => {
    expect(ok(TestFileSchema, { version: 1, name: 'T', steps: ['back'], skip: '  ' })).toBe(false);
    expect(ok(TestFileSchema, { version: 1, name: 'T', steps: [] })).toBe(false);
    expect(ok(TestFileSchema, { version: 1, name: 'T', steps: ['back'], stpes: [] })).toBe(false);
  });

  it('checks flow parameter defaults against their type', () => {
    const flow = (def: unknown) => ({
      version: 1,
      name: 'F',
      params: { express: { type: 'boolean', default: def } },
      steps: ['back'],
    });
    expect(ok(FlowFileSchema, flow(false))).toBe(true);
    expect(ok(FlowFileSchema, flow('no'))).toBe(false);
  });

  it('requires a known default environment and at least one environment', () => {
    const base = { version: 1, environments: { local: { baseUrl: 'http://localhost:5173' } } };
    expect(ok(ConfigFileSchema, base)).toBe(true);
    expect(ok(ConfigFileSchema, { ...base, defaults: { environment: 'staging' } })).toBe(false);
    expect(ok(ConfigFileSchema, { ...base, environments: {} })).toBe(false);
    expect(ok(ConfigFileSchema, { ...base, defaults: { snapshots: 'sometimes' } })).toBe(false);
  });
});

describe('fileKindOf', () => {
  it.each([
    ['tests/a.test.yaml', 'test'],
    ['flows\\login.flow.yaml', 'flow'],
    ['targets/shop.targets.yaml', 'targets'],
    ['cfe.config.yaml', 'config'],
    ['notes.yaml', undefined],
  ])('%s is %s', (path, kind) => {
    expect(fileKindOf(path, 'cfe.config.yaml')).toBe(kind);
  });
});

describe('keysAt', () => {
  it('finds allowed keys inside records, arrays and unions', () => {
    expect(keysAt(TestFileSchema, ['pages', 'admin'])).toEqual(['login']);
    expect(keysAt(TestFileSchema, ['targets', 'go', 0])).toContain('testId');
    expect(keysAt(TestFileSchema, ['targets', 'go'])).toEqual(['candidates', 'frame', 'within']);
    expect(keysAt(TestFileSchema, [])).toContain('steps');
    expect(keysAt(TestFileSchema, ['vars', 'x'])).toBeUndefined();
  });
});
