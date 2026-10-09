// Unit tests for the run folder (ADR 0015): folder names, and the files built
// from a run's events.
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { RunFolder, testFolderName } from './run-folder.js';

const roots: string[] = [];
afterEach(() => {
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true });
});

describe('testFolderName', () => {
  it('is the order, the file name without extension and the row, in safe characters', () => {
    expect(testFolderName(3, 'tests/checkout/guest-checkout.test.yaml', 1)).toBe(
      '03-guest-checkout-row1',
    );
    expect(testFolderName(12, 'tests/Ünïcode Name!.test.yaml')).toBe('12-n-code-name');
    expect(testFolderName(1, `tests/${'x'.repeat(80)}.test.yaml`)).toHaveLength(40);
  });
});

describe('RunFolder', () => {
  it('writes events.ndjson as sent, and run.json and test.json from the events', () => {
    const dir = mkdtempSync(join(tmpdir(), 'cfe-run-folder-'));
    roots.push(dir);
    const folder = new RunFolder(dir, { engineVersion: '0.0.0', protocolVersion: '0.1.0' });
    const lines = [
      {
        method: 'runStarted',
        params: {
          runId: 'r',
          seq: 1,
          env: 'local',
          browser: 'chromium',
          settings: {},
          startedAt: 't',
          tests: [{ testId: 'tests/a.test.yaml#0', file: 'tests/a.test.yaml', name: 'A' }],
        },
      },
      {
        method: 'testStarted',
        params: { runId: 'r', seq: 2, testId: 'tests/a.test.yaml#0', startedAt: 't' },
      },
      {
        method: 'stepStarted',
        params: {
          runId: 'r',
          seq: 3,
          testId: 'tests/a.test.yaml#0',
          stepId: 'steps.0',
          section: 'steps',
          action: 'goto',
          params: { url: '/' },
          page: 'main',
          title: 'goto /',
          location: { file: 'tests/a.test.yaml', line: 4, column: 5 },
        },
      },
      {
        method: 'stepPassed',
        params: {
          runId: 'r',
          seq: 4,
          testId: 'tests/a.test.yaml#0',
          stepId: 'steps.0',
          durationMs: 5,
          locators: [],
          snapshot: { state: 'skipped' },
        },
      },
      {
        method: 'testFinished',
        params: {
          runId: 'r',
          seq: 5,
          testId: 'tests/a.test.yaml#0',
          status: 'passed',
          durationMs: 9,
        },
      },
      {
        method: 'runFinished',
        params: {
          runId: 'r',
          seq: 6,
          status: 'passed',
          durationMs: 10,
          totals: { passed: 1, failed: 0, cancelled: 0, skipped: 0 },
        },
      },
    ].map((message) => JSON.stringify({ jsonrpc: '2.0', ...message }));
    for (const line of lines) folder.record(line);

    expect(readFileSync(join(dir, 'events.ndjson'), 'utf8')).toBe(`${lines.join('\n')}\n`);
    const run = JSON.parse(readFileSync(join(dir, 'run.json'), 'utf8')) as Record<string, unknown>;
    expect(run).toMatchObject({
      runId: 'r',
      engineVersion: '0.0.0',
      protocolVersion: '0.1.0',
      status: 'passed',
      totals: { passed: 1 },
      tests: [{ testId: 'tests/a.test.yaml#0', status: 'passed', folder: 'tests/01-a' }],
    });
    const test = JSON.parse(
      readFileSync(join(dir, 'tests', '01-a', 'test.json'), 'utf8'),
    ) as Record<string, unknown>;
    expect(test).toMatchObject({
      testId: 'tests/a.test.yaml#0',
      status: 'passed',
      durationMs: 9,
      steps: [{ stepId: 'steps.0', action: 'goto', status: 'passed', durationMs: 5 }],
    });
    expect(existsSync(join(dir, 'tests', '01-a', 'steps'))).toBe(false);
  });
});
