// Integration tests of plan branch 9 through the demo app harness: data rows
// (S6), user actions inside runs (S13), skipped tests (S15), the run folder
// (I8), keepRuns (I9) and listTests. Every message, responses included, is
// checked against the protocol's JSON Schema files (I10).
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { checkMessage } from '../testing/protocol-schemas.js';
import { collectRun, eventsOf, type CollectedRun } from '../testing/run.js';
import { RUN_ID_PATTERN } from './keep-runs.js';

let app: DemoApp;
let nextId = 300;

beforeAll(async () => {
  app = await startDemoApp({ env: { DEMO_PASSWORD: 'demo-password-1234' } });
});
afterAll(async () => {
  await app.close();
});

async function run(params: Record<string, unknown>): Promise<CollectedRun> {
  const collected = await collectRun(app.engine, nextId++, params);
  expect(collected.invalid).toEqual([]);
  expect(collected.response).toHaveProperty('result');
  return collected;
}

async function request(method: string, params?: unknown): Promise<Record<string, unknown>> {
  const response = await app.engine.request(nextId++, method, params);
  expect(checkMessage(response, method)).toEqual([]);
  return response;
}

function runsDir(): string {
  return join(app.root, '.cfe', 'runs');
}

describe('rows, skips and user actions', () => {
  it('S6: one test instance per data row, inline and from a CSV file', async () => {
    const collected = await run({
      files: ['tests/data-rows.test.yaml', 'tests/data-rows-csv.test.yaml'],
    });
    expect(eventsOf(collected.events, 'runStarted')[0]?.tests).toEqual([
      {
        testId: 'tests/data-rows.test.yaml#0',
        file: 'tests/data-rows.test.yaml',
        name: 'Add Buy milk',
        row: 0,
      },
      {
        testId: 'tests/data-rows.test.yaml#1',
        file: 'tests/data-rows.test.yaml',
        name: 'Add Walk the dog',
        row: 1,
      },
      {
        testId: 'tests/data-rows-csv.test.yaml#0',
        file: 'tests/data-rows-csv.test.yaml',
        name: 'Add desk-lamp',
        row: 0,
      },
      {
        testId: 'tests/data-rows-csv.test.yaml#1',
        file: 'tests/data-rows-csv.test.yaml',
        name: 'Add office-chair',
        row: 1,
      },
      {
        testId: 'tests/data-rows-csv.test.yaml#2',
        file: 'tests/data-rows-csv.test.yaml',
        name: 'Add notebook',
        row: 2,
      },
    ]);
    expect(eventsOf(collected.events, 'testFinished').map((event) => event.status)).toEqual([
      'passed',
      'passed',
      'passed',
      'passed',
      'passed',
    ]);
    // Each row has its own before, steps and after; ${row.…} is filled in per row.
    const fills = eventsOf(collected.events, 'stepStarted').filter(
      (event) => event.action === 'fill',
    );
    expect(fills.map((event) => event.params)).toEqual([
      { target: 'todos.new', value: '${row.title}' },
      { target: 'todos.new', value: '${row.title}' },
      { target: 'todos.new', value: '${row.title} (${row.price} €)' },
      { target: 'todos.new', value: '${row.title} (${row.price} €)' },
      { target: 'todos.new', value: '${row.title} (${row.price} €)' },
    ]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({
      status: 'passed',
      totals: { passed: 5, failed: 0, cancelled: 0, skipped: 0 },
    });
  });

  it('S13: a step that calls a user action runs it with the same ctx', async () => {
    const collected = await run({ files: ['tests/user-action.test.yaml'] });
    expect(eventsOf(collected.events, 'log').map((event) => event.message)).toEqual([
      'Adding "Buy milk"',
      'Adding "Walk the dog" as done',
    ]);
    expect(eventsOf(collected.events, 'stepPassed')[1]).toMatchObject({
      stepId: 'steps.1',
      locators: [
        { param: 'target', target: 'todos.new', candidateIndex: 0 },
        { param: 'target', target: 'todos.add', candidateIndex: 0 },
      ],
    });
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
  });

  it('S15: a skipped test with 2 rows sends 2 testSkipped and counts 2 skipped', async () => {
    const collected = await run({ files: ['tests/skipped.test.yaml'] });
    expect(collected.events.map((event) => event.method)).toEqual([
      'runStarted',
      'testSkipped',
      'testSkipped',
      'runFinished',
    ]);
    expect(eventsOf(collected.events, 'testSkipped')).toEqual([
      expect.objectContaining({
        testId: 'tests/skipped.test.yaml#0',
        reason: 'The payment page is not part of the demo app yet',
      }),
      expect.objectContaining({
        testId: 'tests/skipped.test.yaml#1',
        reason: 'The payment page is not part of the demo app yet',
      }),
    ]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({
      status: 'passed',
      totals: { passed: 0, failed: 0, cancelled: 0, skipped: 2 },
    });
  });
});

describe('the run folder (I8)', () => {
  it('has the layout of ADR 0015, and events.ndjson holds exactly the events sent', async () => {
    const collected = await run({
      files: ['tests/data-rows.test.yaml', 'fixtures/failing/assertion.test.yaml'],
    });
    const { runId, resultsDir } = collected.response.result as {
      runId: string;
      resultsDir: string;
    };
    expect(readdirSync(resultsDir).sort()).toEqual(['events.ndjson', 'run.json', 'tests']);
    expect(readdirSync(join(resultsDir, 'tests'))).toEqual([
      '01-data-rows-row0',
      '02-data-rows-row1',
      '03-assertion',
    ]);

    const lines = readFileSync(join(resultsDir, 'events.ndjson'), 'utf8').trimEnd().split('\n');
    expect(lines.map((line) => JSON.parse(line) as unknown)).toEqual(
      collected.events.map((event) => ({
        jsonrpc: '2.0',
        method: event.method,
        params: event.params,
      })),
    );

    const runJson = JSON.parse(readFileSync(join(resultsDir, 'run.json'), 'utf8')) as Record<
      string,
      unknown
    >;
    expect(runJson).toMatchObject({
      runId,
      env: 'local',
      browser: 'chromium',
      status: 'failed',
      totals: { passed: 2, failed: 1, cancelled: 0, skipped: 0 },
      tests: [
        {
          testId: 'tests/data-rows.test.yaml#0',
          status: 'passed',
          folder: 'tests/01-data-rows-row0',
        },
        {
          testId: 'tests/data-rows.test.yaml#1',
          status: 'passed',
          folder: 'tests/02-data-rows-row1',
        },
        {
          testId: 'fixtures/failing/assertion.test.yaml#0',
          status: 'failed',
          folder: 'tests/03-assertion',
        },
      ],
    });
    const testJson = JSON.parse(
      readFileSync(join(resultsDir, 'tests', '03-assertion', 'test.json'), 'utf8'),
    ) as { status: string; steps: { stepId: string; status: string; error?: { code: string } }[] };
    expect(testJson.status).toBe('failed');
    expect(testJson.steps.map((step) => `${step.stepId} ${step.status}`)).toEqual([
      'steps.0 passed',
      'steps.1 passed',
      'steps.2 passed',
      'steps.3 failed',
      'steps.4 skipped',
      'after.0 passed',
      'after.1 passed',
    ]);
    expect(testJson.steps[3]?.error?.code).toBe('AssertionFailed');

    // No event carries file contents: not the step files, nor the run folder's files.
    const stepFile = readFileSync(
      join(app.root, 'fixtures', 'failing', 'assertion.test.yaml'),
      'utf8',
    );
    for (const line of lines) {
      expect(line).not.toContain('version: 1');
      expect(line).not.toContain(stepFile.slice(0, 40));
      expect(line).not.toContain('"engineVersion"');
    }
  });
});

describe('keepRuns (I9)', () => {
  it('keeps the newest runs, leaves other folders alone, and keeps everything with 0', async () => {
    const config = join(app.root, 'cfe.config.yaml');
    const original = readFileSync(config, 'utf8');
    const withKeep = (keep: number): string =>
      original.replace('defaults:\n', `defaults:\n  keepRuns: ${String(keep)}\n`);
    expect(withKeep(3)).not.toBe(original);
    mkdirSync(join(runsDir(), 'notes'), { recursive: true });
    writeFileSync(join(runsDir(), 'notes', 'keep-me.txt'), 'not a run');

    writeFileSync(config, withKeep(3));
    expect(await request('openProject', { root: app.root })).toMatchObject({
      result: { diagnostics: [] },
    });
    const runIds: string[] = [];
    for (let index = 0; index < 5; index++) {
      // Run ids sort by time to the second; keep the runs a second apart.
      await new Promise((resolve) => setTimeout(resolve, 1_050));
      const collected = await run({ files: ['tests/skipped.test.yaml'] });
      runIds.push((collected.response.result as { runId: string }).runId);
    }
    const runs = (): string[] =>
      readdirSync(runsDir())
        .filter((name) => RUN_ID_PATTERN.test(name))
        .sort();
    expect(runs()).toEqual(runIds.slice(-3));
    expect(existsSync(join(runsDir(), 'notes', 'keep-me.txt'))).toBe(true);

    writeFileSync(config, withKeep(0));
    expect(await request('openProject', { root: app.root })).toMatchObject({
      result: { diagnostics: [] },
    });
    await run({ files: ['tests/skipped.test.yaml'] });
    await run({ files: ['tests/skipped.test.yaml'] });
    expect(runs()).toHaveLength(5);

    writeFileSync(config, original);
    await request('openProject', { root: app.root });
  });
});

describe('listTests', () => {
  it('lists every test with its name as written, its tags and its rows', async () => {
    const response = await request('listTests', {});
    const tests = (
      response.result as { tests: { file: string; name: string; tags: string[]; rows: number }[] }
    ).tests;
    expect(tests.find((test) => test.file === 'tests/data-rows-csv.test.yaml')).toEqual({
      file: 'tests/data-rows-csv.test.yaml',
      name: 'Add ${row.sku}',
      tags: [],
      rows: 3,
    });
    expect(tests.find((test) => test.file === 'tests/data-rows.test.yaml')?.rows).toBe(2);
    expect(tests.find((test) => test.file === 'tests/skipped.test.yaml')?.rows).toBe(2);
    expect(tests.find((test) => test.file === 'tests/frames.test.yaml')?.rows).toBe(1);
    expect(tests.some((test) => test.file.startsWith('fixtures/'))).toBe(false);
  });

  it('filters by tag', async () => {
    const response = await request('listTests', { tags: ['smoke'] });
    expect(
      (response.result as { tests: { file: string }[] }).tests.map((test) => test.file),
    ).toEqual(['tests/after-always.test.yaml', 'tests/user-action.test.yaml']);
  });
});
