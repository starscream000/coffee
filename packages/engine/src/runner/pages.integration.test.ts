// Integration tests of plan branch 10, part 1, through the demo app harness:
// named and automatic pages (S7), the environment's settings (S17), a user
// action that ignores ctx.signal (F8), and cancelRun, after which no browser
// process may remain. Every message is checked against the JSON Schema files.
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { browserProcessIds, waitUntilGone } from '../testing/processes.js';
import { checkMessage } from '../testing/protocol-schemas.js';
import { collectRun, eventsOf, type CollectedRun, type RunEvent } from '../testing/run.js';

let app: DemoApp;
let nextId = 500;

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

function outcomes(events: readonly RunEvent[]): string[] {
  return events
    .filter((event) => ['stepPassed', 'stepFailed', 'stepSkipped'].includes(event.method))
    .map((event) => `${String(event.params.stepId)} ${event.method.slice(4).toLowerCase()}`);
}

describe('pages', () => {
  it('S7: opens names a new tab; an unnamed tab gets an automatic name and a warning', async () => {
    const collected = await run({ files: ['tests/multi-page.test.yaml'] });
    expect(outcomes(collected.events)).toEqual([
      'steps.0 passed',
      'steps.1 passed',
      'steps.2 passed',
      'steps.3 passed',
      'steps.4 passed',
    ]);
    expect(eventsOf(collected.events, 'pageOpened')).toEqual([
      expect.objectContaining({ stepId: 'steps.1', page: 'receipt', automatic: false }),
      expect.objectContaining({ stepId: 'steps.3', page: 'tab-3', automatic: true }),
    ]);
    expect(eventsOf(collected.events, 'stepStarted')[2]).toMatchObject({
      stepId: 'steps.2',
      page: 'receipt',
    });
    expect(eventsOf(collected.events, 'log')).toEqual([
      expect.objectContaining({
        level: 'warn',
        code: 'UnnamedPage',
        stepId: 'steps.3',
        location: { file: 'tests/multi-page.test.yaml', line: 11, column: 5 },
        message:
          'A new page opened during steps.3 and was named "tab-3". Add `opens: <name>` to this step to use the new page.',
      }),
    ]);
  });

  it('a step with opens fails with PageNotOpened when no page opens', async () => {
    writeFileSync(
      join(app.root, 'tests', 'no-tab.test.yaml'),
      'version: 1\nname: No tab\nsteps:\n  - goto: /tabs\n  - click: tabs.heading\n    opens: nothing\n    timeout: 1s\n',
    );
    const collected = await run({ files: ['tests/no-tab.test.yaml'] });
    expect(eventsOf(collected.events, 'stepFailed')[0]).toMatchObject({
      stepId: 'steps.1',
      error: { code: 'PageNotOpened' },
    });
  });
});

describe('settings (S17)', () => {
  it('uses viewport 1280×720, locale en-US and timezone UTC by default', async () => {
    const collected = await run({ files: ['tests/settings.test.yaml'] });
    expect(eventsOf(collected.events, 'runStarted')[0]).toMatchObject({
      env: 'local',
      settings: { viewport: { width: 1280, height: 720 }, locale: 'en-US', timezone: 'UTC' },
    });
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
  });

  it('uses the locale and timezone of an environment that overrides them', async () => {
    const collected = await run({ files: ['tests/settings.test.yaml'], env: 'europe' });
    expect(eventsOf(collected.events, 'runStarted')[0]).toMatchObject({
      env: 'europe',
      settings: { locale: 'de-DE', timezone: 'Europe/Berlin' },
    });
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
  });
});

describe('a user action that ignores ctx.signal (F8)', () => {
  it('fails with ActionTimeout, loses its page, and the after step gets a new page', async () => {
    const collected = await run({ files: ['fixtures/failing/stray-action.test.yaml'] });
    expect(outcomes(collected.events)).toEqual([
      'steps.0 passed',
      'steps.1 failed',
      'after.0 passed',
      'after.1 passed',
    ]);
    expect(eventsOf(collected.events, 'stepFailed')[0]).toMatchObject({
      error: {
        code: 'ActionTimeout',
        hint: 'The action did not stop when ctx.signal was aborted.',
      },
    });
    expect(eventsOf(collected.events, 'log').map((event) => event.code)).toEqual([
      'StrayActionCode',
      'PageReplaced',
    ]);
    // The stray action's own ctx.log, 5 seconds in, is never sent: wait for it.
    await new Promise((resolve) => setTimeout(resolve, 3_500));
    expect(await app.engine.request(nextId++, 'listActions')).toHaveProperty('result');
  });
});

describe('cancelRun', () => {
  it('cancels the current step, skips the rest, runs after steps, cancels later tests', async () => {
    writeFileSync(
      join(app.root, 'tests', 'long.test.yaml'),
      [
        'version: 1',
        'name: Waits for a count that never comes',
        'steps:',
        '  - goto: /todos',
        '  - expect.text: { target: todos.count, equals: 99 }',
        '    timeout: 60s',
        '  - goto: /todos',
        'after:',
        '  - goto: /todos',
        '  - expect.text: { target: todos.count, equals: 0 }',
        '',
      ].join('\n'),
    );
    const runId = nextId++;
    app.engine.send({
      jsonrpc: '2.0',
      id: runId,
      method: 'startRun',
      params: { files: ['tests/long.test.yaml', 'tests/frames.test.yaml'] },
    });
    const events: RunEvent[] = [];
    let browser: number[] = [];
    let cancelAnswer: Record<string, unknown> | undefined;
    const cancelId = nextId++;
    for (;;) {
      const message = await app.engine.next(30_000);
      expect(checkMessage(message, message.id === runId ? 'startRun' : 'cancelRun')).toEqual([]);
      if (message.id === cancelId) cancelAnswer = message;
      if (message.id !== undefined) continue;
      const event = {
        method: String(message.method),
        params: message.params as Record<string, unknown>,
      };
      events.push(event);
      if (
        event.method === 'stepStarted' &&
        event.params.stepId === 'steps.1' &&
        browser.length === 0
      ) {
        browser = browserProcessIds(app.engine.child.pid ?? -1);
        app.engine.send({
          jsonrpc: '2.0',
          id: cancelId,
          method: 'cancelRun',
          params: { runId: event.params.runId },
        });
      }
      if (event.method === 'runFinished') break;
    }
    expect(cancelAnswer).toMatchObject({ result: null });
    expect(outcomes(events)).toEqual([
      'steps.0 passed',
      'steps.1 failed',
      'steps.2 skipped',
      'after.0 passed',
      'after.1 passed',
    ]);
    expect(eventsOf(events, 'stepFailed')[0]).toMatchObject({ error: { code: 'Cancelled' } });
    expect(eventsOf(events, 'stepSkipped')[0]).toMatchObject({
      reason: 'cancelled',
      message: 'skipped: the run was cancelled',
    });
    expect(eventsOf(events, 'testFinished').map((event) => [event.testId, event.status])).toEqual([
      ['tests/long.test.yaml#0', 'cancelled'],
      ['tests/frames.test.yaml#0', 'cancelled'],
    ]);
    expect(eventsOf(events, 'runFinished')[0]).toMatchObject({
      status: 'cancelled',
      totals: { passed: 0, failed: 0, cancelled: 2, skipped: 0 },
    });
    // No browser process is left behind after a cancelled run.
    expect(browser.length).toBeGreaterThan(0);
    expect(await waitUntilGone(browser)).toEqual([]);
  });

  it('refuses a run id that is not in progress', async () => {
    const response = await app.engine.request(nextId++, 'cancelRun', { runId: 'nope' });
    expect(response).toMatchObject({ error: { code: -32602 } });
  });
});
