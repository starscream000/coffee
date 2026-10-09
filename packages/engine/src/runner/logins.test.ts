// Unit tests for the clean-up of saved logins (ADR 0018): what is stale, what
// stays, and a file that cannot be deleted.
import { mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import type { ConfigFile } from '../schema/files.js';
import { pruneLogins } from './logins.js';

const roots: string[] = [];
afterEach(() => {
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true });
});

const NOW = Date.parse('2026-10-10T12:00:00Z');
const config = {
  version: 1,
  environments: { local: { baseUrl: 'http://localhost' } },
  logins: {
    customer: { flow: 'flows/login.flow.yaml' },
    admin: { flow: 'flows/login.flow.yaml', maxAge: '2h' },
  },
} as unknown as ConfigFile;

function folder(files: Record<string, string>): string {
  const dir = mkdtempSync(join(tmpdir(), 'cfe-logins-'));
  roots.push(dir);
  for (const [name, text] of Object.entries(files)) writeFileSync(join(dir, name), text);
  return dir;
}

const meta = (login: string, env: string, hoursAgo: number): string =>
  JSON.stringify({
    createdAt: new Date(NOW - hoursAgo * 3_600_000).toISOString(),
    env,
    login,
    engineVersion: '0.0.0',
  });

describe('pruneLogins', () => {
  it('deletes expired, orphaned and unreadable states and keeps the key and current states', () => {
    const dir = folder({
      '.key': 'k',
      'a.json': '{}',
      'a.meta.json': meta('customer', 'local', 11), // within 12h
      'b.json': '{}',
      'b.meta.json': meta('admin', 'local', 3), // older than 2h
      'c.json': '{}',
      'c.meta.json': meta('ghost', 'local', 1), // login removed
      'd.json': '{}',
      'd.meta.json': meta('customer', 'staging', 1), // environment removed
      'e.json': '{}', // no metadata
      'f.json': '{}',
      'f.meta.json': 'not json', // unreadable metadata
      'g.meta.json': meta('customer', 'local', 1), // metadata without a state
    });
    const result = pruneLogins(dir, config, NOW);
    expect(result.failed).toEqual([]);
    expect(readdirSync(dir).sort()).toEqual(['.key', 'a.json', 'a.meta.json']);
  });

  it('reports a file it cannot delete and goes on', () => {
    const dir = folder({ 'x.json': '{}', 'y.json': '{}' });
    const result = pruneLogins(dir, config, NOW, (path) => {
      if (path.endsWith('x.json')) throw new Error('EBUSY');
      rmSync(path);
    });
    expect(result).toEqual({ removed: ['y.json'], failed: [{ file: 'x.json', reason: 'EBUSY' }] });
  });

  it('does nothing when there is no logins folder', () => {
    expect(pruneLogins(join(tmpdir(), 'cfe-no-such-folder'), config, NOW)).toEqual({
      removed: [],
      failed: [],
    });
  });
});
