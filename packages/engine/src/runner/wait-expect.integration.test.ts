// Integration tests of the waits and checks (plan branch 12) through the demo
// app harness: samples S1, S2 (with expect.value), S3 and S16, readable
// failures of expect.visible and expect.count, check I3 (an assertion whose
// actual value is 10 MB) and check I4 (cancelRun during a 30-second
// wait.element).
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { MAX_MESSAGE_BYTES, TRUNCATED_FIELD_BYTES } from '@cfe/protocol';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { checkMessage } from '../testing/protocol-schemas.js';
import { collectRun, eventsOf, type CollectedRun, type RunEvent } from '../testing/run.js';

let app: DemoApp;
let nextId = 1000;

beforeAll(async () => {
  app = await startDemoApp();
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

function testFile(name: string, lines: string[]): string {
  const file = `tests/${name}.test.yaml`;
  writeFileSync(join(app.root, file), [...lines, ''].join('\n'));
  return file;
}

function failures(collected: CollectedRun): Record<string, unknown>[] {
  return eventsOf(collected.events, 'stepFailed').map(
    (event) => event.error as Record<string, unknown>,
  );
}

describe('samples', () => {
  it.each([
    ['S1', 'tests/navigation.test.yaml'],
    ['S2', 'tests/forms.test.yaml'],
    ['S3', 'tests/interactions.test.yaml'],
  ])('%s passes', async (_check, file) => {
    const collected = await run({ files: [file] });
    expect(failures(collected)).toEqual([]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
  });

  it('S16: deletes the row named after each of three data rows, through within', async () => {
    const collected = await run({ files: ['tests/within.test.yaml'] });
    expect(failures(collected)).toEqual([]);
    expect(eventsOf(collected.events, 'testFinished').map((event) => event.status)).toEqual([
      'passed',
      'passed',
      'passed',
    ]);
    const clicks = eventsOf(collected.events, 'stepPassed').filter(
      (event) => (event.locators as unknown[]).length > 0 && event.stepId === 'steps.1',
    );
    expect(
      clicks.map(
        (event) => (event.locators as { within?: { candidate: unknown } }[])[0]?.within?.candidate,
      ),
    ).toEqual([
      { role: 'row', name: 'Desk lamp', exact: false },
      { role: 'row', name: 'Office chair', exact: false },
      { role: 'row', name: 'Notebook', exact: false },
    ]);
  });
});

describe('readable failures', () => {
  it('expect.visible false and expect.count say what they found', async () => {
    const file = testFile('checks-fail', [
      'version: 1',
      'name: Checks that fail',
      'steps:',
      '  - goto: /interactions',
      '  - expect.visible: { target: interactions.help, visible: false }',
      '    timeout: 1s',
    ]);
    const [visible] = failures(await run({ files: [file] }));
    expect(visible).toMatchObject({
      code: 'AssertionFailed',
      expected: 'hidden',
      actual: 'visible',
      message: '"interactions.help" is visible: candidate 0 matches 1 visible element(s).',
    });

    const count = testFile('count-fail', [
      'version: 1',
      'name: Count that fails',
      'steps:',
      '  - goto: /interactions',
      '  - expect.count: { target: fruit.items, equals: 5 }',
      '    timeout: 1s',
    ]);
    const counted = await run({ files: [count] });
    expect(failures(counted)[0]).toMatchObject({
      code: 'AssertionFailed',
      expected: '5',
      actual: 3,
      message: '"fruit.items" matches 3 element(s), expected 5.',
    });
    expect(eventsOf(counted.events, 'stepFailed')[0]?.locators).toEqual([
      expect.objectContaining({ param: 'target', target: 'fruit.items', candidateIndex: 0 }),
    ]);
  });
});

describe('I3: message size', () => {
  it('an assertion whose actual value is 10 MB gives a stepFailed under 4 MiB, truncated and marked', async () => {
    // A line over the limit from the client is answered and the session goes on.
    app.engine.sendRaw(`${'x'.repeat(5 * 1024 * 1024)}\n`);
    expect(await app.engine.next()).toMatchObject({
      id: null,
      error: { data: { name: 'MessageTooLarge' } },
    });
    // Only double quotes inside, so the URL fits in a single-quoted YAML scalar.
    const page =
      'data:text/html,<p id="big"></p><script>document.getElementById("big").textContent="x".repeat(10000000)</script>';
    const file = testFile('big-text', [
      'version: 1',
      'name: A huge text',
      'steps:',
      `  - goto: '${page}'`,
      "  - expect.text: { target: [{ css: '#big' }], equals: small }",
      '    timeout: 3s',
    ]);
    const collected = await run({ files: [file] });
    const failed = eventsOf(collected.events, 'stepFailed')[0];
    expect(failed).toBeDefined();
    const line = JSON.stringify({ jsonrpc: '2.0', method: 'stepFailed', params: failed });
    expect(Buffer.byteLength(line)).toBeLessThan(MAX_MESSAGE_BYTES);
    const error = failed?.error as { code: string; actual: string; message: string };
    expect(error.code).toBe('AssertionFailed');
    expect(Buffer.byteLength(error.actual)).toBeLessThanOrEqual(TRUNCATED_FIELD_BYTES);
    expect(error.actual).toMatch(/… \[truncated \d+ characters\]$/);
    expect(error.message).toMatch(/… \[truncated \d+ characters\]$/);
  });
});

describe('I4: cancellation', () => {
  it('cancelRun during a 30-second wait.element ends the step within 2 seconds', async () => {
    const file = testFile('long-wait', [
      'version: 1',
      'name: Waits for an element that never comes',
      'steps:',
      '  - goto: /interactions',
      "  - wait.element: { target: [{ css: '#never' }] }",
      '    timeout: 30s',
      'after:',
      '  - goto: /todos',
    ]);
    const runId = nextId++;
    app.engine.send({ jsonrpc: '2.0', id: runId, method: 'startRun', params: { files: [file] } });
    const events: RunEvent[] = [];
    let cancelledAt = 0;
    let stepEndedAt = 0;
    for (;;) {
      const message = await app.engine.next(30_000);
      expect(checkMessage(message, message.id === runId ? 'startRun' : 'cancelRun')).toEqual([]);
      if (message.id !== undefined) continue;
      const event = {
        method: String(message.method),
        params: message.params as Record<string, unknown>,
      };
      events.push(event);
      if (event.method === 'stepStarted' && event.params.action === 'wait.element') {
        await new Promise((resolve) => setTimeout(resolve, 500));
        cancelledAt = performance.now();
        app.engine.send({
          jsonrpc: '2.0',
          id: nextId++,
          method: 'cancelRun',
          params: { runId: event.params.runId },
        });
      }
      if (event.method === 'stepFailed') stepEndedAt = performance.now();
      if (event.method === 'runFinished') break;
    }
    expect(stepEndedAt - cancelledAt).toBeLessThan(2_000);
    expect(eventsOf(events, 'stepFailed')[0]).toMatchObject({ error: { code: 'Cancelled' } });
    expect(eventsOf(events, 'stepPassed').map((event) => event.stepId)).toContain('after.0');
    expect(eventsOf(events, 'testFinished')[0]).toMatchObject({ status: 'cancelled' });
  });
});
