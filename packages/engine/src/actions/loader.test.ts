// Unit tests for loading user actions: bundling, the content-hash cache,
// compile and load errors, and the single SDK copy. Needs `tsc -b` first,
// because bundles import the engine's built SDK.
import { cpSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { BUILTIN_SPECS } from './builtin-specs.js';
import { checkSdkVersion, loadUserActions } from './loader.js';
import { ActionRegistry } from './registry.js';

const roots: string[] = [];
afterEach(() => {
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true });
});

function makeProject(files: Record<string, string>): string {
  const root = mkdtempSync(join(tmpdir(), 'cfe-actions-'));
  roots.push(root);
  write(root, files);
  return root;
}

function write(root: string, files: Record<string, string>): void {
  for (const [file, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, file)), { recursive: true });
    writeFileSync(join(root, file), text);
  }
}

const ACTION = (name: string, extra = '') => `
import { defineAction, z } from '@cfe/engine/sdk';
${extra}
export default defineAction({
  name: '${name}',
  description: 'Test action.',
  params: z.strictObject({ value: z.string() }),
  run: () => Promise.resolve(),
});
`;

async function load(root: string) {
  const registry = new ActionRegistry(BUILTIN_SPECS);
  const report = await loadUserActions(root, ['actions/**/*.ts'], registry);
  return { registry, report };
}

describe('loadUserActions', () => {
  it('bundles and loads a user action into the registry', async () => {
    const root = makeProject({ 'actions/shop.ts': ACTION('shop.addToCart') });
    const { registry, report } = await load(root);
    expect(report.diagnostics).toEqual([]);
    expect(report.files).toEqual(['actions/shop.ts']);
    expect(registry.specs.get('shop.addToCart')?.description).toBe('Test action.');
    expect(registry.list().at(-1)?.source).toMatchObject({
      kind: 'file',
      file: 'actions/shop.ts',
      line: 5,
    });
  });

  it('does not rebuild an unchanged file, and rebuilds a changed one or one whose import changed', async () => {
    const root = makeProject({
      'actions/shop.ts': ACTION(
        'shop.addToCart',
        "import { label } from '../lib/helper.js'; void label;",
      ),
      'lib/helper.ts': "export const label = 'one';\n",
    });
    expect((await load(root)).report.built).toEqual(['actions/shop.ts']);
    expect((await load(root)).report.built).toEqual([]);
    write(root, { 'lib/helper.ts': "export const label = 'two';\n" });
    expect((await load(root)).report.built).toEqual(['actions/shop.ts']);
    write(root, { 'actions/shop.ts': ACTION('shop.addToCart2') });
    const { registry, report } = await load(root);
    expect(report.built).toEqual(['actions/shop.ts']);
    expect(registry.specs.has('shop.addToCart2')).toBe(true);
  });

  it('reports a syntax error with file and line, and still loads the other files', async () => {
    const root = makeProject({
      'actions/broken.ts': "import { z } from '@cfe/engine/sdk';\nconst x = ;\n",
      'actions/good.ts': ACTION('good.one'),
    });
    const { registry, report } = await load(root);
    expect(report.diagnostics).toMatchObject([
      { file: 'actions/broken.ts', line: 2, code: 'ActionCompileError' },
    ]);
    expect(registry.specs.has('good.one')).toBe(true);
    expect(registry.failedFiles).toEqual(['actions/broken.ts']);
  });

  it('reports a file that throws while loading', async () => {
    const root = makeProject({ 'actions/throws.ts': "throw new Error('boom at load');\n" });
    const { report } = await load(root);
    expect(report.diagnostics).toMatchObject([
      { file: 'actions/throws.ts', code: 'ActionLoadError' },
    ]);
    expect(report.diagnostics[0]?.message).toContain('boom at load');
  });

  it("uses the engine's SDK even when the project has its own copy installed", async () => {
    const root = makeProject({
      'node_modules/@cfe/engine/package.json': JSON.stringify({
        name: '@cfe/engine',
        version: '9.9.9',
        type: 'module',
        exports: { './sdk': './sdk.js' },
      }),
      'node_modules/@cfe/engine/sdk.js':
        "export function defineAction() { throw new Error('wrong SDK copy'); }\n",
      'actions/shop.ts': ACTION(
        'shop.check',
        "import { AssertionError } from '@cfe/engine/sdk';\nif (new AssertionError('x').sdkError !== 'AssertionError') throw new Error('not the engine SDK');",
      ),
    });
    const { registry, report } = await load(root);
    expect(report.diagnostics).toEqual([]);
    expect(registry.specs.has('shop.check')).toBe(true);
    expect(checkSdkVersion(root)).toMatchObject({
      severity: 'warning',
      code: 'SdkVersionMismatch',
    });
    expect(checkSdkVersion(root)?.message).toContain('9.9.9');
  });
});

const FIXTURE_PACKAGES = new URL('../../test/fixtures/packages/', import.meta.url);

function installFixturePackage(root: string, name: string): void {
  cpSync(new URL(`${name}/`, FIXTURE_PACKAGES), join(root, 'node_modules', name), {
    recursive: true,
  });
}

describe('review 0004 fixes', () => {
  it('finding 2: an action importing a CommonJS package that requires Node built-ins loads', async () => {
    const root = makeProject({
      'actions/shop.ts': ACTION(
        'shop.cjs',
        "import pkg from 'cjs-uses-builtins';\nif (pkg.joined !== 'a/b' || !pkg.sameModule) throw new Error('wrong values');",
      ),
    });
    installFixturePackage(root, 'cjs-uses-builtins');
    const { registry, report } = await load(root);
    expect(report.diagnostics).toEqual([]);
    expect(registry.specs.has('shop.cjs')).toBe(true);
  });

  it('finding 2: the load-error hint fits the cause', async () => {
    const root = makeProject({
      'actions/own.ts': "throw new Error('own top-level code');\n",
      'actions/dep.ts': ACTION('shop.dep', "import 'throws-on-load';"),
    });
    installFixturePackage(root, 'throws-on-load');
    const { report } = await load(root);
    const byFile = new Map(report.diagnostics.map((d) => [d.file, d]));
    expect(byFile.get('actions/own.ts')?.hint).toContain('move work into run()');
    expect(byFile.get('actions/dep.ts')?.hint).toContain('the package "throws-on-load"');
    expect(byFile.get('actions/dep.ts')?.hint).not.toContain('move work into run()');
  });

  it('finding 3: a changed helper is used by a second load in the same process', async () => {
    const root = makeProject({
      'actions/shop.ts': `
import { defineAction, z } from '@cfe/engine/sdk';
import { label } from '../lib/helper.js';
export default defineAction({
  name: 'shop.helper',
  description: label,
  params: z.strictObject({}),
  run: () => Promise.resolve(),
});
`,
      'lib/helper.ts': "export const label = 'First version.';\n",
    });
    expect((await load(root)).registry.specs.get('shop.helper')?.description).toBe(
      'First version.',
    );
    write(root, { 'lib/helper.ts': "export const label = 'Second version.';\n" });
    expect((await load(root)).registry.specs.get('shop.helper')?.description).toBe(
      'Second version.',
    );
  });

  it('finding 7: bundles no current action file maps to are deleted, and one that cannot be is skipped', async () => {
    const root = makeProject({ 'actions/shop.ts': ACTION('shop.one') });
    await load(root);
    const cacheDir = join(root, '.cfe', 'cache', 'actions');
    const before = readdirSync(cacheDir);
    expect(before.length).toBe(3);
    mkdirSync(join(cacheDir, 'stale0123456789abcdef0123456789ab.mjs'));
    write(root, { 'actions/shop.ts': ACTION('shop.two') });
    const { report } = await load(root);
    expect([...report.removed].sort()).toEqual([...before].sort());
    const after = readdirSync(cacheDir);
    expect(after).toContain('stale0123456789abcdef0123456789ab.mjs');
    expect(after.filter((name) => !name.startsWith('stale'))).toHaveLength(3);
  });
});
