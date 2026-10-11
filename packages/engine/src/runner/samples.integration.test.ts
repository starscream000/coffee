// Integration tests of the runner (plan branch 8): the samples S10, S11, S12
// and S14 and the fixtures F1, F2, F3 and F7 run through the demo app harness
// in Chromium. Each test checks the events a client receives, every one of
// them against the protocol's JSON Schema files, and the demo app's state.
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { collectRun, eventsOf, type CollectedRun } from '../testing/run.js';

let app: DemoApp;
let nextId = 100;

beforeAll(async () => {
  app = await startDemoApp({ env: { DEMO_PASSWORD: 'demo-password-1234' } });
});
afterAll(async () => {
  await app.close();
});

async function run(files: string[], extra: Record<string, unknown> = {}): Promise<CollectedRun> {
  const collected = await collectRun(app.engine, nextId++, { files, ...extra });
  expect(collected.invalid).toEqual([]);
  expect(collected.response).toHaveProperty('result');
  // seq rises by one from 1, with no gaps.
  expect(collected.events.map((event) => event.params.seq)).toEqual(
    collected.events.map((_event, index) => index + 1),
  );
  return collected;
}

async function resets(): Promise<number> {
  const response = await fetch(`${app.url}/api/state`);
  return ((await response.json()) as { resets: number }).resets;
}

/** The step results of a run as `stepId status`, in order. */
function outcomes(collected: CollectedRun): string[] {
  return collected.events
    .filter((event) => ['stepPassed', 'stepFailed', 'stepSkipped'].includes(event.method))
    .map((event) => `${String(event.params.stepId)} ${event.method.slice(4).toLowerCase()}`);
}

describe('the runner on the demo app', () => {
  it('reports chromium in capabilities.browsers', async () => {
    const { EngineProcess } = await import('../testing/engine-process.js');
    const engine = new EngineProcess();
    engine.initialize(0);
    expect(await engine.next()).toMatchObject({
      result: { capabilities: { browsers: ['chromium'] } },
    });
    engine.child.kill();
  });

  it('answers startRun with the run id and folder before the first event', async () => {
    const collected = await run(['tests/slow-render.test.yaml']);
    const result = collected.response.result as { runId: string; resultsDir: string };
    expect(result.runId).toMatch(/^\d{8}-\d{6}-\d{3}-[0-9a-f]{4}$/);
    expect(result.resultsDir).toBe(`${app.root.replaceAll('\\', '/')}/.cfe/runs/${result.runId}`);
    expect(collected.events.map((event) => event.method)).toEqual([
      'runStarted',
      'testStarted',
      'stepStarted',
      'stepPassed',
      'stepStarted',
      'stepPassed',
      'stepStarted',
      'stepPassed',
      'testFinished',
      'runFinished',
    ]);
    expect(collected.events[0]?.params).toMatchObject({
      runId: result.runId,
      env: 'local',
      browser: 'chromium',
      settings: { viewport: { width: 1280, height: 720 }, locale: 'en-US', timezone: 'UTC' },
      tests: [
        {
          testId: 'tests/slow-render.test.yaml#0',
          file: 'tests/slow-render.test.yaml',
          name: 'The first candidate wins on a slow page',
        },
      ],
    });
  });

  it('S10: after steps run after a passing test, and the reset endpoint proves it', async () => {
    const before = await resets();
    const collected = await run(['tests/after-always.test.yaml']);
    expect(outcomes(collected)).toEqual([
      'steps.0 passed',
      'steps.1 passed',
      'steps.2 passed',
      'steps.3 passed',
      'after.0 passed',
      'after.1 passed',
      'after.2 passed',
    ]);
    expect(eventsOf(collected.events, 'stepStarted').at(-1)).toMatchObject({
      stepId: 'after.2',
      section: 'after',
      action: 'expect.text',
      params: { target: 'todos.count', equals: 0 },
      page: 'main',
      title: 'expect.text todos.count',
      location: { file: 'tests/after-always.test.yaml', line: 14, column: 5 },
    });
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({
      status: 'passed',
      totals: { passed: 1, failed: 0, cancelled: 0, skipped: 0 },
    });
    expect(await resets()).toBe(before + 1);
  });

  it('S11: the fallback is used after fallbackGrace, reported at index 1 with a warning', async () => {
    const collected = await run(['tests/locator-fallback.test.yaml']);
    const click = eventsOf(collected.events, 'stepPassed')[1];
    expect(click).toMatchObject({
      stepId: 'steps.1',
      locators: [
        {
          param: 'target',
          target: 'order.placeOrder',
          candidateIndex: 1,
          candidate: { testId: 'place-order' },
        },
      ],
    });
    expect(click?.durationMs).toBeGreaterThanOrEqual(1_000);
    expect(eventsOf(collected.events, 'log')).toEqual([
      expect.objectContaining({
        level: 'warn',
        code: 'LocatorFallback',
        testId: 'tests/locator-fallback.test.yaml#0',
        stepId: 'steps.1',
        location: { file: 'tests/locator-fallback.test.yaml', line: 7, column: 5 },
        data: { target: 'order.placeOrder', candidateIndex: 1 },
      }),
    ]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
  });

  it('S12: the first candidate wins over a placeholder that matches the fallback', async () => {
    const collected = await run(['tests/slow-render.test.yaml']);
    expect(eventsOf(collected.events, 'stepPassed')[1]).toMatchObject({
      locators: [{ target: 'cart.checkout', candidateIndex: 0 }],
    });
    expect(eventsOf(collected.events, 'log')).toEqual([]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
  });

  it('S14: a field inside a frame inside a frame, with every level reported', async () => {
    const collected = await run(['tests/frames.test.yaml']);
    expect(eventsOf(collected.events, 'stepPassed')[1]).toMatchObject({
      stepId: 'steps.1',
      locators: [
        {
          param: 'target',
          target: 'payment.cardNumber',
          candidateIndex: 0,
          candidate: { label: 'Card number' },
          frame: {
            param: 'frame',
            target: 'paymentFrame',
            candidateIndex: 0,
            frame: { param: 'frame', target: 'checkoutFrame', candidateIndex: 0 },
          },
        },
      ],
    });
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
  });

  it('F1: an assertion fails with expected and actual; the rest is skipped; after steps ran', async () => {
    const before = await resets();
    const collected = await run(['fixtures/failing/assertion.test.yaml']);
    expect(outcomes(collected)).toEqual([
      'steps.0 passed',
      'steps.1 passed',
      'steps.2 passed',
      'steps.3 failed',
      'steps.4 skipped',
      'after.0 passed',
      'after.1 passed',
      'after.2 passed',
    ]);
    expect(eventsOf(collected.events, 'stepFailed')[0]).toMatchObject({
      error: {
        code: 'AssertionFailed',
        message: 'Text of "todos.count" is "1", expected "2".',
        expected: '2',
        actual: '1',
        location: { file: 'fixtures/failing/assertion.test.yaml', line: 9, column: 5 },
      },
    });
    // No stepStarted was sent for it, so it says where it is (desktop request R0006).
    expect(eventsOf(collected.events, 'stepSkipped')[0]).toMatchObject({
      stepId: 'steps.4',
      reason: 'previousFailure',
      message: 'skipped: steps.3 failed',
      section: 'steps',
      action: 'click',
      title: 'click todos.add',
      location: { file: 'fixtures/failing/assertion.test.yaml', line: 11, column: 5 },
    });
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({
      status: 'failed',
      totals: { passed: 0, failed: 1, cancelled: 0, skipped: 0 },
    });
    expect(await resets()).toBe(before + 1);
  });

  it('F2: TargetNotFound lists every candidate with its match count', async () => {
    const collected = await run(['fixtures/failing/target-not-found.test.yaml']);
    const failed = eventsOf(collected.events, 'stepFailed')[0];
    expect(failed).toMatchObject({
      stepId: 'steps.1',
      error: {
        code: 'TargetNotFound',
        location: { file: 'fixtures/failing/target-not-found.test.yaml', line: 7, column: 5 },
        candidates: [
          { candidate: { role: 'button', name: 'Delete all' }, matches: 0 },
          { candidate: { testId: 'delete-all' }, matches: 0 },
          { candidate: { css: 'button' }, matches: 2 },
        ],
      },
      locators: [{ param: 'target', candidateIndex: null, candidate: null }],
    });
    expect((failed?.error as { message: string }).message).toContain(
      '2: css="button" → 2 elements',
    );
  });

  it('F3: a failing before step is a setup failure; steps are skipped; after steps ran', async () => {
    const before = await resets();
    const collected = await run(['fixtures/failing/before-fails.test.yaml']);
    expect(outcomes(collected)).toEqual([
      'before.0 passed',
      'before.1 failed',
      'steps.0 skipped',
      'steps.1 skipped',
      'after.0 passed',
      'after.1 passed',
      'after.2 passed',
    ]);
    expect(eventsOf(collected.events, 'stepStarted')[1]).toMatchObject({
      stepId: 'before.1',
      section: 'before',
    });
    expect(eventsOf(collected.events, 'stepSkipped')[0]).toMatchObject({
      reason: 'previousFailure',
      message: "skipped: the test's setup failed at before.1",
    });
    expect(eventsOf(collected.events, 'testFinished')[0]).toMatchObject({ status: 'failed' });
    expect(await resets()).toBe(before + 1);
  });

  it('F7: an after step that needs an unset variable is skipped, not failed', async () => {
    const collected = await run(['fixtures/failing/after-unset-var.test.yaml']);
    expect(outcomes(collected)).toEqual(['steps.0 passed', 'steps.1 failed', 'after.0 skipped']);
    expect(eventsOf(collected.events, 'stepSkipped')[0]).toEqual(
      expect.objectContaining({
        stepId: 'after.0',
        reason: 'variableNotSet',
        variable: 'orderNumber',
        message: 'skipped: orderNumber was never set',
      }),
    );
    expect(eventsOf(collected.events, 'stepFailed')).toHaveLength(1);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'failed' });
  });

  it('sends stepStarted.params in the long form with nothing interpolated', async () => {
    writeFileSync(
      join(app.root, 'tests', 'interpolated.test.yaml'),
      'version: 1\nname: Interpolated\nvars:\n  page: todos\nsteps:\n  - goto: /${vars.page}\n  - expect.text: { target: todos.count, contains: "${vars.missing}" }\n',
    );
    const collected = await run(['tests/interpolated.test.yaml']);
    expect(eventsOf(collected.events, 'stepStarted').map((event) => event.params)).toEqual([
      { url: '/${vars.page}' },
      { target: 'todos.count', contains: '${vars.missing}' },
    ]);
    expect(eventsOf(collected.events, 'stepFailed')[0]).toMatchObject({
      error: { code: 'VariableNotSet' },
    });
  });

  it('selects tests by tag', async () => {
    const collected = await collectRun(app.engine, nextId++, { tags: ['smoke'] });
    expect(collected.invalid).toEqual([]);
    const files = (
      eventsOf(collected.events, 'runStarted')[0]?.tests as { file: string }[] | undefined
    )?.map((test) => test.file);
    expect(files).toEqual(['tests/after-always.test.yaml', 'tests/user-action.test.yaml']);
  });

  it('runs one run at a time', async () => {
    app.engine.send({
      jsonrpc: '2.0',
      id: nextId++,
      method: 'startRun',
      params: { files: ['tests/slow-render.test.yaml'] },
    });
    const second = nextId++;
    app.engine.send({
      jsonrpc: '2.0',
      id: second,
      method: 'startRun',
      params: { files: ['tests/slow-render.test.yaml'] },
    });
    let refused: Record<string, unknown> | undefined;
    for (;;) {
      const message = await app.engine.next();
      if (message.id === second) refused = message;
      if (message.method === 'runFinished') break;
    }
    expect(refused).toMatchObject({ error: { code: -32006, data: { name: 'RunInProgress' } } });
  });
});
