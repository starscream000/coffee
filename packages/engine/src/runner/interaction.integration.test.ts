// Integration tests of the interaction actions (plan branch 11) through the
// demo app harness: sample S2 (fill, select, check, press, upload, in
// shorthand and long forms), and hover, drag, clearing an upload and an
// option that does not exist, in step files written to the harness's copy.
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { collectRun, eventsOf, type CollectedRun } from '../testing/run.js';

let app: DemoApp;
let nextId = 900;

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

/** Writes a test file into the harness's copy and returns its path. */
function testFile(name: string, steps: string[]): string {
  const file = `tests/${name}.test.yaml`;
  writeFileSync(
    join(app.root, file),
    ['version: 1', `name: ${name}`, 'steps:', ...steps, ''].join('\n'),
  );
  return file;
}

function failures(collected: CollectedRun): unknown[] {
  return eventsOf(collected.events, 'stepFailed').map((event) => event.error);
}

describe('interaction actions', () => {
  it('S2: fills in the order form with every interaction action', async () => {
    const collected = await run('tests/forms.test.yaml');
    expect(failures(collected)).toEqual([]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
    const actions = eventsOf(collected.events, 'stepStarted').map((event) => event.action);
    for (const action of ['fill', 'select', 'check', 'upload', 'press', 'click', 'expect.text']) {
      expect(actions).toContain(action);
    }
  });

  it('hover shows a tooltip, and drag moves an item in a sortable list', async () => {
    const file = testFile('hover-and-drag', [
      '  - goto: /interactions',
      '  - hover: interactions.help',
      '  - expect.text: { target: interactions.tooltip, equals: Opens the help pages }',
      '  - drag: { from: fruit.cherry, to: fruit.apple }',
      "  - expect.text: { target: fruit.order, equals: 'Cherry, Apple, Banana' }",
    ]);
    const collected = await run(file);
    expect(failures(collected)).toEqual([]);
    expect(eventsOf(collected.events, 'stepPassed')[3]).toMatchObject({
      locators: [
        { param: 'from', target: 'fruit.cherry', candidateIndex: 0 },
        { param: 'to', target: 'fruit.apple', candidateIndex: 0 },
      ],
    });
  });

  it('upload with an empty list clears the files', async () => {
    const file = testFile('upload-clear', [
      '  - goto: /form',
      '  - upload: { target: form.attachment, files: data/notes.txt }',
      '  - upload: { target: form.attachment, files: [] }',
      '  - click: form.send',
      "  - expect.text: { target: form.summary, contains: 'no files' }",
    ]);
    expect(failures(await run(file))).toEqual([]);
  });

  it('a select option that does not exist fails the step with a readable error', async () => {
    const file = testFile('select-missing', [
      '  - goto: /form',
      '  - select: { target: form.country, option: Atlantis }',
      '    timeout: 1s',
    ]);
    const [error] = failures(await run(file));
    expect(error).toMatchObject({
      code: 'OptionNotFound',
      message:
        '"form.country" has no option "Atlantis". Its options: "" (Choose…), "de" (Germany), "fr" (France).',
    });
  });
});
