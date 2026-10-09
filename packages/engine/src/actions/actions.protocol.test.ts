// Protocol tests for user actions: checks F9 (naming rules through
// openProject), I12 (one SDK copy, SdkVersionMismatch) and I13 (stdout stays
// clean while action files load), plus listActions and validate with user
// actions. They start the engine as a child process, as a client does.
import { cpSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, describe, expect, it } from 'vitest';
import { EngineProcess } from '../testing/engine-process.js';

const EXAMPLES = fileURLToPath(new URL('../../../../examples/', import.meta.url));
const TEST_PROJECTS = fileURLToPath(new URL('../../test/projects/', import.meta.url));

interface Diagnostic {
  file: string;
  line: number;
  column: number;
  severity: string;
  code: string;
  message: string;
}

const engines: EngineProcess[] = [];
const roots: string[] = [];
afterEach(() => {
  for (const engine of engines.splice(0)) {
    if (engine.child.exitCode === null) engine.child.kill();
  }
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true });
});

async function open(root: string): Promise<{ engine: EngineProcess; diagnostics: Diagnostic[] }> {
  const engine = new EngineProcess();
  engines.push(engine);
  engine.initialize(0);
  await engine.next();
  const response = await engine.request(1, 'openProject', { root });
  const result = response.result as { diagnostics: Diagnostic[] } | undefined;
  if (result === undefined) throw new Error(`openProject failed: ${JSON.stringify(response)}`);
  return { engine, diagnostics: result.diagnostics };
}

/** A copy of a project in a temporary folder, so its cache does not touch the repository. */
function copyProject(source: string): string {
  const root = mkdtempSync(join(tmpdir(), 'cfe-protocol-'));
  roots.push(root);
  cpSync(source, root, { recursive: true, filter: (path) => !path.includes('.cfe') });
  return root;
}

describe('user actions through the protocol', () => {
  it('F9: openProject reports every action that breaks the naming rules, at its name', async () => {
    const { diagnostics } = await open(copyProject(join(EXAMPLES, 'demo-app-bad-actions')));
    expect(
      diagnostics.map((d) => `${d.file}:${String(d.line)}:${String(d.column)} ${d.code}`),
    ).toEqual([
      'actions/bad.ts:9:11 ActionNameNotNamespaced',
      'actions/bad.ts:15:11 ActionNamespaceReserved',
      'actions/bad.ts:21:11 ActionNameNotNamespaced',
    ]);
    expect(diagnostics[0]?.message).toBe(
      'Action "fillOtp" needs a namespace, for example "bad.fillOtp". Names without a dot are reserved for built-in actions.',
    );
    expect(diagnostics[1]?.message).toContain('reserved for built-in actions');
    expect(diagnostics[2]?.message).toContain('"click" is a built-in action');
  });

  it('F9: a step calling a rejected action says it did not load', async () => {
    const { engine } = await open(copyProject(join(EXAMPLES, 'demo-app-bad-actions')));
    const response = await engine.request(2, 'validate', {
      content: {
        file: 'tests/a.test.yaml',
        text: 'version: 1\nname: T\nsteps:\n  - fillOtp: { code: "1" }\n',
      },
    });
    expect(response).toMatchObject({
      result: { diagnostics: [{ line: 4, column: 5, code: 'ActionNotLoaded' }] },
    });
  });

  it("I12: an installed SDK of another version is not used; the engine's copy is, with a warning", async () => {
    const root = mkdtempSync(join(tmpdir(), 'cfe-i12-'));
    roots.push(root);
    const files: Record<string, string> = {
      'cfe.config.yaml':
        'version: 1\nenvironments:\n  local:\n    baseUrl: http://localhost:4310\n',
      'package.json': JSON.stringify({ name: 'user-project', private: true }),
      'node_modules/@cfe/engine/package.json': JSON.stringify({
        name: '@cfe/engine',
        version: '9.9.9',
        type: 'module',
        exports: { './sdk': './sdk.js' },
      }),
      'node_modules/@cfe/engine/sdk.js':
        "export function defineAction() { throw new Error('wrong SDK copy'); }\nexport class AssertionError extends Error { sdkError = 'Fake'; }\n",
      'actions/check.ts': [
        "import { AssertionError, defineAction, z } from '@cfe/engine/sdk';",
        "if (new AssertionError('x').sdkError !== 'AssertionError') throw new Error('not the engine SDK');",
        'export default defineAction({',
        "  name: 'mismatch.check',",
        "  description: 'Proves the engine SDK is used.',",
        '  params: z.strictObject({}),',
        '  run: () => Promise.resolve(),',
        '});',
      ].join('\n'),
    };
    for (const [file, text] of Object.entries(files)) {
      mkdirSync(join(root, file, '..'), { recursive: true });
      writeFileSync(join(root, file), text);
    }
    const { engine, diagnostics } = await open(root);
    expect(diagnostics).toMatchObject([
      { file: 'package.json', severity: 'warning', code: 'SdkVersionMismatch' },
    ]);
    expect(diagnostics[0]?.message).toContain('9.9.9');
    expect(diagnostics[0]?.message).toContain('the engine is 0.0.0');
    const listed = await engine.request(2, 'listActions');
    expect(JSON.stringify(listed)).toContain('"mismatch.check"');
  });

  it("imports of playwright in a user action use the engine's copy, not the project's", async () => {
    const root = mkdtempSync(join(tmpdir(), 'cfe-playwright-'));
    roots.push(root);
    const files: Record<string, string> = {
      'cfe.config.yaml':
        'version: 1\nenvironments:\n  local:\n    baseUrl: http://localhost:4310\n',
      'package.json': JSON.stringify({ name: 'user-project', private: true }),
      'node_modules/playwright/package.json': JSON.stringify({
        name: 'playwright',
        version: '0.0.1',
        type: 'module',
        exports: { '.': './index.js', './package.json': './package.json' },
      }),
      'node_modules/playwright/index.js':
        "throw new Error('the project copy of playwright was loaded');\n",
      'actions/browser.ts': [
        "import { defineAction, z } from '@cfe/engine/sdk';",
        "import { chromium, devices } from 'playwright';",
        "if (typeof chromium.launch !== 'function' || devices['iPhone 15'] === undefined) {",
        "  throw new Error('not the engine copy of playwright');",
        '}',
        'export default defineAction({',
        "  name: 'browser.check',",
        "  description: 'Proves the engine copy of Playwright is used.',",
        '  params: z.strictObject({}),',
        '  run: () => Promise.resolve(),',
        '});',
      ].join('\n'),
    };
    for (const [file, text] of Object.entries(files)) {
      mkdirSync(join(root, file, '..'), { recursive: true });
      writeFileSync(join(root, file), text);
    }
    const { engine, diagnostics } = await open(root);
    expect(diagnostics).toEqual([]);
    const listed = await engine.request(2, 'listActions');
    expect(JSON.stringify(listed)).toContain('"browser.check"');
  });

  it('I13: an action file that calls every console method leaves stdout to the protocol', async () => {
    const { engine, diagnostics } = await open(copyProject(join(TEST_PROJECTS, 'noisy-actions')));
    expect(diagnostics).toEqual([]);
    expect(engine.notJson).toEqual([]);
    for (const text of [
      'log',
      'dir: 1',
      'dirxml: 2',
      'table',
      'count: 1',
      'Assertion failed: assert',
    ]) {
      expect(await engine.waitForStderr(text)).toContain(text);
    }
    expect(await engine.request(2, 'listActions')).toMatchObject({ id: 2 });
    expect(engine.notJson).toEqual([]);
  });

  it('listActions returns built-in and user actions with JSON Schema parameters', async () => {
    const { engine } = await open(copyProject(join(EXAMPLES, 'demo-app')));
    const response = await engine.request(2, 'listActions');
    const actions = (response.result as { actions: Record<string, unknown>[] }).actions;
    expect(actions).toHaveLength(27);
    expect(actions.find((a) => a.name === 'fill')).toMatchObject({
      shorthand: null,
      source: { kind: 'builtin' },
    });
    expect(actions.find((a) => a.name === 'demo.addTodo')).toMatchObject({
      description: "Adds an item on the demo app's to-do page.",
      shorthand: 'title',
      source: { kind: 'file', file: 'actions/demo.ts' },
      paramsSchema: { type: 'object', properties: { title: { type: 'string' } } },
    });
  });

  it('validate checks steps that call a user action against its parameters', async () => {
    const { engine, diagnostics } = await open(copyProject(join(EXAMPLES, 'demo-app')));
    expect(diagnostics).toEqual([]);
    expect(
      await engine.request(2, 'validate', { files: ['tests/user-action.test.yaml'] }),
    ).toMatchObject({
      result: { diagnostics: [] },
    });
    const bad = await engine.request(3, 'validate', {
      content: {
        file: 'tests/b.test.yaml',
        text: 'version: 1\nname: T\nsteps:\n  - demo.addTodo: { done: true }\n',
      },
    });
    expect(bad).toMatchObject({
      result: {
        diagnostics: [
          {
            line: 4,
            column: 19,
            code: 'MissingParameter',
            message: '"demo.addTodo" needs "title".',
          },
        ],
      },
    });
  });
});

describe('review 0004 finding 2 through a real engine process', () => {
  it('loads an action that imports a CommonJS package requiring Node built-ins', async () => {
    const root = mkdtempSync(join(tmpdir(), 'cfe-cjs-'));
    roots.push(root);
    cpSync(
      fileURLToPath(new URL('../../test/fixtures/packages/cjs-uses-builtins/', import.meta.url)),
      join(root, 'node_modules', 'cjs-uses-builtins'),
      { recursive: true },
    );
    writeFileSync(
      join(root, 'cfe.config.yaml'),
      'version: 1\nenvironments:\n  local:\n    baseUrl: http://localhost:4310\n',
    );
    mkdirSync(join(root, 'actions'));
    writeFileSync(
      join(root, 'actions', 'shop.ts'),
      [
        "import { defineAction, z } from '@cfe/engine/sdk';",
        "import pkg from 'cjs-uses-builtins';",
        "if (pkg.joined !== 'a/b' || !pkg.sameModule) throw new Error('wrong values');",
        'export default defineAction({',
        "  name: 'shop.cjs',",
        "  description: 'Uses a CommonJS package.',",
        '  params: z.strictObject({}),',
        '  run: () => Promise.resolve(),',
        '});',
      ].join('\n'),
    );
    const { engine, diagnostics } = await open(root);
    expect(diagnostics).toEqual([]);
    expect(JSON.stringify(await engine.request(2, 'listActions'))).toContain('"shop.cjs"');
  });
});
