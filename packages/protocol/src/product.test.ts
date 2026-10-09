// Tests that the product identity is well formed and that files which cannot
// import it (.gitignore, the CLI package.json) still agree with it, so renaming
// the product is one change plus whatever these tests point at.
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { PRODUCT, PRODUCT_ID } from './product.js';

const repoRoot = new URL('../../../', import.meta.url);

function readRepoFile(relativePath: string): string {
  return readFileSync(new URL(relativePath, repoRoot), 'utf8');
}

describe('PRODUCT', () => {
  it('has a valid identifier', () => {
    expect(PRODUCT_ID).toMatch(/^[a-z][a-z0-9-]*$/);
  });

  it('derives every name from the identifier', () => {
    expect(PRODUCT).toMatchObject({
      command: PRODUCT_ID,
      configFile: `${PRODUCT_ID}.config.yaml`,
      dataDir: `.${PRODUCT_ID}`,
    });
    expect(PRODUCT.envPrefix).toMatch(/^[A-Z][A-Z0-9_]*_$/);
  });

  it('is the command name in the CLI package.json', () => {
    const manifest = JSON.parse(readRepoFile('packages/cli/package.json')) as {
      bin?: Record<string, string>;
    };
    expect(Object.keys(manifest.bin ?? {})).toEqual([PRODUCT.command]);
  });

  it('has its data folder ignored by Git', () => {
    const lines = readRepoFile('.gitignore').split(/\r?\n/);
    expect(lines).toContain(`${PRODUCT.dataDir}/`);
  });
});
