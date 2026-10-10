// Integration tests of `call` (plan branch 13) through the demo app harness:
// sample S5 (parameters, outputs, a flow calling a flow) and check I2 (its
// event sequence matches the checked-in expectation in
// flows.events.expected.json; UPDATE_EXPECTED=1 rewrites it, to be reviewed
// before committing), and a failing flow.
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { collectRun, eventsOf, type CollectedRun, type RunEvent } from '../testing/run.js';

const EXPECTED = join(import.meta.dirname, 'flows.events.expected.json');

let app: DemoApp;
let nextId = 1700;

beforeAll(async () => {
  app = await startDemoApp();
});
afterAll(async () => {
  await app.close();
});

async function run(file: string): Promise<CollectedRun> {
  const collected = await collectRun(app.engine, nextId++, { files: [file] });
  expect(collected.invalid).toEqual([]);
  expect(collected.response).toHaveProperty('result');
  return collected;
}

/** The fields of an event that I2 compares; times and durations vary. */
function shape(event: RunEvent): Record<string, unknown> {
  const { params } = event;
  const keep = ['seq', 'testId', 'stepId', 'parentStepId', 'section', 'action', 'params', 'status'];
  return {
    method: event.method,
    ...Object.fromEntries(keep.filter((key) => key in params).map((key) => [key, params[key]])),
  };
}

describe('S5 and I2: flows', () => {
  it('runs flows with parameters and outputs, nested in the events as expected', async () => {
    const collected = await run('tests/flows.test.yaml');
    expect(eventsOf(collected.events, 'stepFailed')).toEqual([]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
    const actual = collected.events.map(shape);
    if (process.env.UPDATE_EXPECTED === '1') {
      writeFileSync(EXPECTED, `${JSON.stringify(actual, null, 2)}\n`);
    }
    expect(actual).toEqual(JSON.parse(readFileSync(EXPECTED, 'utf8')));
  });

  it('a failing step fails its flow and the call, and skips the rest of the flow', async () => {
    const file = 'tests/flow-fails.test.yaml';
    writeFileSync(
      join(app.root, file),
      [
        'version: 1',
        'name: A flow that fails',
        'steps:',
        '  - goto: /interactions',
        '  - call: { flow: flows/add-todo.flow.yaml, with: { title: Nowhere } }',
        '    timeout: 1s',
        '  - goto: /todos',
        '',
      ].join('\n'),
    );
    const collected = await run(file);
    expect(eventsOf(collected.events, 'stepFailed').map((event) => event.stepId)).toEqual([
      'steps.1/steps.0',
      'steps.1',
    ]);
    const [inner, outer] = eventsOf(collected.events, 'stepFailed').map(
      (event) => event.error as Record<string, unknown>,
    );
    expect(inner).toMatchObject({ location: { file: 'flows/add-todo.flow.yaml', line: 14 } });
    expect(outer).toMatchObject({
      code: 'FlowFailed',
      location: { file, line: 5, column: 5 },
    });
    expect(outer?.message).toMatch(
      /^The flow "flows\/add-todo\.flow\.yaml" failed at steps\.1\/steps\.0 \(flows\/add-todo\.flow\.yaml:14:5\): /,
    );
    expect(eventsOf(collected.events, 'stepSkipped').map((event) => event.stepId)).toEqual([
      'steps.1/steps.1',
      'steps.1/steps.2',
      'steps.1/steps.3',
      'steps.1/steps.4',
      'steps.2',
    ]);
  });
});
