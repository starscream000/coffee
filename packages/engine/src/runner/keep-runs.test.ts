// Unit tests for keepRuns (ADR 0015): the oldest run folders go, other
// folders stay, 0 keeps everything, and a folder that cannot be deleted is
// reported without stopping the clean-up.
import { mkdirSync, mkdtempSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { pruneRuns } from './keep-runs.js';

const roots: string[] = [];
afterEach(() => {
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true });
});

function runsFolder(names: string[]): string {
  const root = mkdtempSync(join(tmpdir(), 'cfe-runs-'));
  roots.push(root);
  for (const name of names) mkdirSync(join(root, name));
  return root;
}

const RUNS = [
  '20261009-100000-000-aaaa',
  '20261009-100001-000-bbbb',
  '20261009-100002-000-cccc',
  '20261009-100003-000-dddd',
];

describe('pruneRuns', () => {
  it('deletes the oldest runs so that the new run makes keepRuns, and leaves other folders', () => {
    const dir = runsFolder([...RUNS, 'notes']);
    expect(pruneRuns(dir, 3)).toEqual({
      removed: ['20261009-100000-000-aaaa', '20261009-100001-000-bbbb'],
      failed: [],
    });
    expect(readdirSync(dir).sort()).toEqual([
      '20261009-100002-000-cccc',
      '20261009-100003-000-dddd',
      'notes',
    ]);
  });

  it('keeps every run with keepRuns 0, and copes with a missing folder', () => {
    const dir = runsFolder(RUNS);
    expect(pruneRuns(dir, 0)).toEqual({ removed: [], failed: [] });
    expect(readdirSync(dir)).toHaveLength(4);
    expect(pruneRuns(join(dir, 'nothing-here'), 3)).toEqual({ removed: [], failed: [] });
  });

  it('skips a folder that cannot be deleted and goes on with the others', () => {
    const dir = runsFolder(RUNS);
    const result = pruneRuns(dir, 2, (folder) => {
      if (folder.endsWith('aaaa')) throw new Error('EBUSY: resource busy or locked');
      rmSync(folder, { recursive: true });
    });
    expect(result).toEqual({
      removed: ['20261009-100001-000-bbbb', '20261009-100002-000-cccc'],
      failed: [{ runId: '20261009-100000-000-aaaa', reason: 'EBUSY: resource busy or locked' }],
    });
  });
});
