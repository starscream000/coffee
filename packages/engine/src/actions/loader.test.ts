// Unit tests for loading user actions: bundling, the content-hash cache,
// compile and load errors, and the single SDK copy. Needs `tsc -b` first,
// because bundles import the engine's built SDK.
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
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
