// Unit tests for loading secrets from the environment and a .env file.
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { SecretRegistry } from './mask.js';
import { SecretStore, parseDotEnv } from './secrets.js';

const roots: string[] = [];
afterEach(() => {
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true });
});

function project(dotEnv?: string): string {
  const root = mkdtempSync(join(tmpdir(), 'cfe-secrets-'));
  roots.push(root);
  if (dotEnv !== undefined) writeFileSync(join(root, '.env'), dotEnv);
  return root;
}

describe('parseDotEnv', () => {
  it('reads NAME=value lines with quotes, export and comments', () => {
    const values = parseDotEnv(
      [
        '# a comment',
        'PLAIN=one two # trailing',
        'export EXPORTED=yes',
        'DOUBLE="a # b\\nc"',
        "SINGLE='x y'",
        'not a line',
        '',
      ].join('\n'),
    );
    expect(Object.fromEntries(values)).toEqual({
      PLAIN: 'one two',
      EXPORTED: 'yes',
      DOUBLE: 'a # b\nc',
      SINGLE: 'x y',
    });
  });
});

describe('SecretStore', () => {
  it('reads the process environment first, then .env, and registers each value for masking', () => {
    const registry = new SecretRegistry();
    const store = SecretStore.load(
      ['FROM_ENV', 'FROM_FILE', 'BOTH'],
      project('FROM_FILE=file-value\nBOTH=file-loses\n'),
      { FROM_ENV: 'env-value', BOTH: 'env-wins' },
      registry,
    );
    expect(store.get('FROM_ENV')).toBe('env-value');
    expect(store.get('FROM_FILE')).toBe('file-value');
    expect(store.get('BOTH')).toBe('env-wins');
    expect(registry.mask('env-value file-value env-wins')).toBe('••• ••• •••');
  });

  it('notes a missing secret and one shorter than 4 characters, and names the variable to set', () => {
    const registry = new SecretRegistry();
    const store = SecretStore.load(['MISSING', 'SHORT'], project(), { SHORT: 'abc' }, registry);
    expect(store.problem('MISSING')).toBe('missing');
    expect(store.problem('SHORT')).toBe('tooShort');
    expect(registry.size).toBe(0);
    expect(() => store.get('MISSING')).toThrow(
      'The secret "MISSING" has no value. Set the environment variable MISSING, or add MISSING=… to the .env file at the project root.',
    );
    expect(() => store.get('SHORT')).toThrow('shorter than 4 characters');
    expect(() => store.get('UNDECLARED')).toThrow('not declared as a secret');
  });
});
