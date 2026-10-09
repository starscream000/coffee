// Unit tests for the project module and the cross-file checks, each against a
// small temporary project.
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import type { Diagnostic } from '@cfe/protocol';
import { afterEach, describe, expect, it } from 'vitest';
import { BUILTIN_SPECS } from '../actions/builtin-specs.js';
import { RpcError } from '../rpc/rpc-error.js';
import { Project } from './project.js';

const CONFIG = [
  'version: 1',
  "tests: ['tests/**/*.test.yaml']",
  "flows: ['flows/**/*.flow.yaml']",
  "targets: ['targets/**/*.targets.yaml']",
  'environments:',
  '  local:',
  '    baseUrl: http://localhost:1',
  '    values: { apiUrl: http://localhost:1/api }',
  '  staging:',
  '    baseUrl: https://staging.example.com',
  '    values: { apiUrl: https://s/api, onlyStaging: x }',
  'secrets: [API_TOKEN]',
  'logins:',
  '  customer: { flow: flows/login.flow.yaml }',
  '',
].join('\n');

const roots: string[] = [];
afterEach(() => {
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true });
});

async function project(files: Record<string, string>, config = CONFIG): Promise<Project> {
  const root = mkdtempSync(join(tmpdir(), 'cfe-project-'));
  roots.push(root);
  for (const [file, text] of Object.entries({ 'cfe.config.yaml': config, ...files })) {
    mkdirSync(dirname(join(root, file)), { recursive: true });
    writeFileSync(join(root, file), text);
  }
  return Project.open(root, BUILTIN_SPECS, { environment: { API_TOKEN: 'token-1234' } });
}

const lines = (...l: string[]) => `${l.join('\n')}\n`;
const brief = (diagnostics: readonly Diagnostic[]) =>
  diagnostics.map((d) => `${d.file}:${String(d.line)}:${String(d.column)} ${d.code}`);
const test = (...steps: string[]) => lines('version: 1', 'name: T', 'steps:', ...steps);
const login = lines('version: 1', 'name: Login', 'steps: [back]');

describe('Project.open', () => {
  it('summarises the config, with forward slashes in paths', async () => {
    const p = await project({ 'flows/login.flow.yaml': login });
    const summary = p.summary();
    expect(summary).toMatchObject({
      environments: ['local', 'staging'],
      defaultEnvironment: 'local',
      logins: ['customer'],
      diagnostics: [],
    });
    expect(summary.configFile.endsWith('/cfe.config.yaml')).toBe(true);
    expect(summary.root).not.toContain('\\');
  });

  it('reports config problems as diagnostics, not as a failure', async () => {
    const p = await project(
      {},
      lines(
        'version: 1',
        'environments: { local: { baseUrl: nope } }',
        'secrets: [ok]',
        'extra: 1',
      ),
    );
    expect(brief(p.summary().diagnostics)).toEqual([
      'cfe.config.yaml:2:35 InvalidValue',
      'cfe.config.yaml:4:1 UnknownKey',
    ]);
  });

  it('throws ProjectInvalid when there is no config', async () => {
    const root = mkdtempSync(join(tmpdir(), 'cfe-empty-'));
    roots.push(root);
    await expect(Project.open(root, BUILTIN_SPECS)).rejects.toThrow(RpcError);
    await expect(Project.open(join(root, 'missing'), BUILTIN_SPECS)).rejects.toThrow(
      /does not exist/,
    );
  });

  it('finds files with the config globs, ignoring other folders', async () => {
    const p = await project({
      'tests/a.test.yaml': test('- back'),
      'other/b.test.yaml': test('- back'),
      'flows/login.flow.yaml': login,
    });
    expect(p.files.tests).toEqual(['tests/a.test.yaml']);
    expect(p.files.flows).toEqual(['flows/login.flow.yaml']);
  });
});

describe('validate: files', () => {
  it('reports missing files, unknown kinds and files outside the project', async () => {
    const p = await project({ 'notes.yaml': 'x: 1\n' });
    expect(
      brief(p.validate({ files: ['tests/none.test.yaml', 'notes.yaml', '../outside.test.yaml'] })),
    ).toEqual([
      '../outside.test.yaml:1:1 FileOutsideProject',
      'notes.yaml:1:1 UnknownFileKind',
      'tests/none.test.yaml:1:1 FileNotFound',
    ]);
  });
});

describe('cross-file checks: targets', () => {
  it('finds targets in the file, then in shared files, and reports unknown ones', async () => {
    const p = await project({
      'targets/shop.targets.yaml': lines(
        'version: 1',
        'targets:',
        '  cart.count: [{ testId: cart-count }]',
      ),
      'tests/a.test.yaml': lines(
        'version: 1',
        'name: T',
        'targets:',
        '  go: [{ css: "#go" }]',
        'steps:',
        '  - click: go',
        '  - click: cart.count',
        '  - click: cart.cuont',
      ),
    });
    const diagnostics = p.validate({ files: ['tests/a.test.yaml'] });
    expect(brief(diagnostics)).toEqual(['tests/a.test.yaml:8:12 UnknownTarget']);
    expect(diagnostics[0]?.hint).toBe('Did you mean "cart.count"?');
  });

  it('reports a test target that reuses a shared name, and duplicates across shared files', async () => {
    const p = await project({
      'targets/a.targets.yaml': lines('version: 1', 'targets:', '  go: [{ css: a }]'),
      'targets/b.targets.yaml': lines('version: 1', 'targets:', '  go: [{ css: b }]'),
      'tests/a.test.yaml': lines(
        'version: 1',
        'name: T',
        'targets:',
        '  go: [{ css: c }]',
        'steps: [back]',
      ),
    });
    expect(brief(p.validate({ files: ['tests/a.test.yaml'] }))).toEqual([
      'tests/a.test.yaml:4:3 DuplicateTarget',
    ]);
    expect(brief(p.summary().diagnostics)).toEqual(['targets/b.targets.yaml:3:3 DuplicateTarget']);
  });

  it('checks frame and within references and reports cycles with the chain', async () => {
    const p = await project({
      'tests/a.test.yaml': lines(
        'version: 1',
        'name: T',
        'targets:',
        '  row: { within: table, candidates: [{ role: row }] }',
        '  table: { within: row, candidates: [{ role: table }] }',
        '  card: { frame: nope, candidates: [{ label: Card }] }',
        'steps: [back]',
      ),
    });
    const diagnostics = p.validate({ files: ['tests/a.test.yaml'] });
    expect(brief(diagnostics)).toEqual([
      'tests/a.test.yaml:5:20 TargetCycle',
      'tests/a.test.yaml:6:18 UnknownTarget',
    ]);
    expect(diagnostics[0]?.message).toBe(
      'Targets refer to each other in a circle: row → table → row.',
    );
  });
});

describe('cross-file checks: flows', () => {
  const flow = lines(
    'version: 1',
    'name: Checkout',
    'params:',
    '  email: { type: string }',
    '  express: { type: boolean, default: false }',
    'steps: [back]',
  );

  it('checks the flow path and its parameters', async () => {
    const p = await project({
      'flows/checkout.flow.yaml': flow,
      'tests/a.test.yaml': test(
        '  - call: flows/chekout.flow.yaml',
        '  - call: { flow: flows/checkout.flow.yaml, with: { email: a@b.c, express: "yes", colour: red } }',
        '  - call: flows/checkout.flow.yaml',
        "  - call: { flow: flows/checkout.flow.yaml, with: { email: '${vars.email}', express: '${vars.fast}' } }",
      ),
    });
    const diagnostics = p.validate({ files: ['tests/a.test.yaml'] });
    expect(brief(diagnostics)).toEqual([
      'tests/a.test.yaml:4:11 FlowNotFound',
      'tests/a.test.yaml:5:76 InvalidParameterType',
      'tests/a.test.yaml:5:83 UnknownParameter',
      'tests/a.test.yaml:6:11 MissingParameter',
    ]);
    expect(diagnostics[0]?.hint).toBe('Did you mean "flows/checkout.flow.yaml"?');
  });

  it("includes the called flow's own problems once, even when it is called twice", async () => {
    const p = await project({
      'flows/broken.flow.yaml': lines('version: 1', 'name: Broken', 'steps:', '  - clik: x'),
      'tests/a.test.yaml': test(
        '  - call: flows/broken.flow.yaml',
        '  - call: flows/broken.flow.yaml',
      ),
    });
    expect(brief(p.validate({ files: ['tests/a.test.yaml'] }))).toEqual([
      'flows/broken.flow.yaml:4:5 UnknownAction',
    ]);
  });
});

describe('cross-file checks: pages, logins and data', () => {
  it('checks page names, opening order and duplicates', async () => {
    const p = await project({
      'flows/login.flow.yaml': login,
      'tests/a.test.yaml': lines(
        'version: 1',
        'name: T',
        'pages: { admin: { login: customer }, other: { login: nobody } }',
        'steps:',
        '  - click: { target: [{ css: a }] }',
        '    page: receipt',
        '  - click: { target: [{ css: a }] }',
        '    opens: receipt',
        '  - back:',
        '    page: receipt',
        '  - back:',
        '    page: amdin',
        '  - back:',
        '    opens: admin',
      ),
    });
    const diagnostics = p.validate({ files: ['tests/a.test.yaml'] });
    expect(brief(diagnostics)).toEqual([
      'tests/a.test.yaml:3:54 UnknownLogin',
      'tests/a.test.yaml:6:11 PageNotYetOpened',
      'tests/a.test.yaml:12:11 UnknownPage',
      'tests/a.test.yaml:14:12 PageNameTaken',
    ]);
    expect(diagnostics[2]?.hint).toBe('Did you mean "admin"?');
  });

  it('checks the data file', async () => {
    const p = await project({
      'tests/rows.csv': 'a,b\n1,2\n',
      'tests/ok.test.yaml': lines('version: 1', 'name: T', 'data: ./rows.csv', 'steps: [back]'),
      'tests/missing.test.yaml': lines(
        'version: 1',
        'name: T',
        'data: ./none.csv',
        'steps: [back]',
      ),
      'tests/kind.test.yaml': lines('version: 1', 'name: T', 'data: ./rows.txt', 'steps: [back]'),
    });
    expect(
      brief(
        p.validate({
          files: ['tests/ok.test.yaml', 'tests/missing.test.yaml', 'tests/kind.test.yaml'],
        }),
      ),
    ).toEqual([
      'tests/kind.test.yaml:3:7 UnsupportedDataFile',
      'tests/missing.test.yaml:3:7 DataFileNotFound',
    ]);
  });
});

describe('cross-file checks: interpolation', () => {
  it('checks namespaces, secrets and environment values in a test', async () => {
    const p = await project({
      'tests/a.test.yaml': test(
        "  - goto: '${env.apiUrl}/x'",
        "  - goto: '${env.onlyStaging}'",
        "  - goto: '${secrets.API_TOKEN}${secrets.API_TOKN}'",
        "  - goto: '${row.sku}'",
        "  - goto: '${params.x}'",
        "  - goto: '${nope.x}$${literal}'",
        "  - goto: '${vars}'",
      ),
    });
    const diagnostics = p.validate({ files: ['tests/a.test.yaml'] });
    expect(brief(diagnostics)).toEqual([
      'tests/a.test.yaml:5:11 UnknownEnvValue',
      'tests/a.test.yaml:6:11 UndeclaredSecret',
      'tests/a.test.yaml:7:11 NamespaceNotAvailable',
      'tests/a.test.yaml:8:11 NamespaceNotAvailable',
      'tests/a.test.yaml:9:11 UnknownNamespace',
      'tests/a.test.yaml:10:11 InvalidInterpolation',
    ]);
    expect(diagnostics[0]?.message).toBe('"onlyStaging" is not a value of the environment local.');
    expect(diagnostics[1]?.hint).toBe('Did you mean "API_TOKEN"?');
  });

  it('allows row in tests with data, and params of the flow in flows', async () => {
    const p = await project({
      'tests/a.test.yaml': lines(
        'version: 1',
        'name: T ${row.sku}',
        'data: [{ sku: a }]',
        'steps:',
        "  - goto: '/p/${row.sku}'",
      ),
      'flows/f.flow.yaml': lines(
        'version: 1',
        'name: F',
        'params: { sku: { type: string } }',
        'steps:',
        "  - goto: '/p/${params.sku}/${params.skew}'",
      ),
    });
    expect(brief(p.validate({ files: ['tests/a.test.yaml', 'flows/f.flow.yaml'] }))).toEqual([
      'flows/f.flow.yaml:5:11 UnknownFlowParam',
    ]);
  });
});

describe('review 0003 fixes', () => {
  it('finding 3: validate on a folder is a diagnostic, not an internal error', async () => {
    const p = await project({ 'tests/a.test.yaml': test('  - back') });
    expect(brief(p.validate({ files: ['tests', '..'] }))).toEqual([
      '..:1:1 FileOutsideProject',
      'tests:1:1 NotAFile',
    ]);
  });

  it('finding 4: an unclosed ${ is reported at its value', async () => {
    const p = await project({
      'tests/a.test.yaml': test("  - fill: { target: [{ css: a }], value: '${vars.email' }"),
    });
    const diagnostics = p.validate({ files: ['tests/a.test.yaml'] });
    expect(brief(diagnostics)).toEqual(['tests/a.test.yaml:4:42 UnclosedInterpolation']);
  });

  it('finding 7: validate re-reads shared targets files', async () => {
    const p = await project({ 'tests/a.test.yaml': test('  - click: go') });
    expect(brief(p.validate({ files: ['tests/a.test.yaml'] }))).toEqual([
      'tests/a.test.yaml:4:12 UnknownTarget',
    ]);
    mkdirSync(join(p.root, 'targets'));
    writeFileSync(
      join(p.root, 'targets/shop.targets.yaml'),
      lines('version: 1', 'targets:', '  go: [{ css: "#go" }]'),
      { flag: 'w' },
    );
    expect(p.validate({ files: ['tests/a.test.yaml'] })).toEqual([]);
  });

  it('finding 8: an environment value may not be named name or baseUrl', async () => {
    const p = await project(
      {},
      lines(
        'version: 1',
        'environments:',
        '  local:',
        '    baseUrl: http://localhost:1',
        '    values: { name: x, baseUrl: y, ok: z }',
      ),
    );
    const diagnostics = p.summary().diagnostics;
    expect(brief(diagnostics)).toEqual([
      'cfe.config.yaml:5:15 ReservedEnvValue',
      'cfe.config.yaml:5:24 ReservedEnvValue',
    ]);
    expect(diagnostics[0]?.message).toContain('is built in');
  });
});

describe('secrets in validate', () => {
  it('reports a declared secret without a value, and one that is too short, where they are used', async () => {
    const root = mkdtempSync(join(tmpdir(), 'cfe-project-'));
    roots.push(root);
    writeFileSync(
      join(root, 'cfe.config.yaml'),
      CONFIG.replace('secrets: [API_TOKEN]', 'secrets: [API_TOKEN, SHORT, FROM_FILE]'),
    );
    writeFileSync(join(root, '.env'), 'SHORT=abc\nFROM_FILE=file-value\n');
    mkdirSync(join(root, 'tests'));
    writeFileSync(
      join(root, 'tests/a.test.yaml'),
      test(
        "  - goto: '/${secrets.API_TOKEN}'",
        "  - goto: '/${secrets.SHORT}'",
        "  - goto: '/${secrets.FROM_FILE}'",
      ),
    );
    const p = await Project.open(root, BUILTIN_SPECS, { environment: {} });
    const diagnostics = p.validate({ files: ['tests/a.test.yaml'] });
    expect(brief(diagnostics)).toEqual([
      'tests/a.test.yaml:4:11 SecretNotSet',
      'tests/a.test.yaml:5:11 SecretTooShort',
    ]);
    expect(diagnostics[0]?.message).toBe(
      'The secret "API_TOKEN" has no value. Set the environment variable API_TOKEN, or add API_TOKEN=… to the .env file at the project root.',
    );
  });
});
