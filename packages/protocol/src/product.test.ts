// Tests that the product identity is well formed and that files which cannot
// import it (.gitignore, package.json files) still agree with it, so renaming
// the product is one change plus whatever these tests point at.
import { readdirSync, readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { PRODUCT } from './product.js';

const repoRoot = new URL('../../../', import.meta.url);

function readRepoFile(relativePath: string): string {
  return readFileSync(new URL(relativePath, repoRoot), 'utf8');
}

interface Manifest {
  name?: string;
  bin?: Record<string, string>;
}

function readManifest(relativePath: string): Manifest {
  return JSON.parse(readRepoFile(relativePath)) as Manifest;
}

describe('PRODUCT', () => {
  it('has well-formed names', () => {
    expect(PRODUCT.displayName).not.toBe('');
    expect(PRODUCT.command).toMatch(/^[a-z][a-z0-9-]*$/);
    expect(PRODUCT.dataDir).toMatch(/^\.[a-z][a-z0-9-]*$/);
    expect(PRODUCT.npmScope).toMatch(/^@[a-z][a-z0-9-]*$/);
  });

  it('derives the config file and environment prefix from the command', () => {
    expect(PRODUCT.configFile).toBe(`${PRODUCT.command}.config.yaml`);
    expect(PRODUCT.envPrefix).toMatch(/^[A-Z][A-Z0-9_]*_$/);
  });

  it('is the command name in the CLI package.json', () => {
    const manifest = readManifest('packages/cli/package.json');
    expect(Object.keys(manifest.bin ?? {})).toEqual([PRODUCT.command]);
  });

  it('is the npm scope of every workspace package', () => {
    for (const dir of readdirSync(new URL('packages/', repoRoot))) {
      const manifest = readManifest(`packages/${dir}/package.json`);
      expect(manifest.name, `packages/${dir}/package.json`).toMatch(
        new RegExp(`^${PRODUCT.npmScope}/`),
      );
    }
  });

  it('has its data folder ignored by Git', () => {
    const lines = readRepoFile('.gitignore').split(/\r?\n/);
    expect(lines).toContain(`${PRODUCT.dataDir}/`);
  });
});
