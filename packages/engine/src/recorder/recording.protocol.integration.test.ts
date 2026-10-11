// Integration tests of recording over the protocol (instruction 0010, tasks 5
// to 8): a real engine process records in a real browser, headless for the
// tests, which the test drives with real mouse and key input through the
// browser's remote debugging port. Every message is checked against the
// protocol's JSON Schema files; the typed password must appear in none.
import { readFileSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { join } from 'node:path';
import { chromium, type Browser, type Locator, type Page } from 'playwright';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { checkMessage } from '../testing/protocol-schemas.js';
import { RECORDING_DEBUG_PORT_VARIABLE, RECORDING_HEADLESS_VARIABLE } from './manager.js';

const PASSWORD = 'protocol-test-password-77';

type Message = Record<string, unknown>;

/** A free TCP port for the recording browser's remote debugging. */
function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createServer();
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      const port = typeof address === 'object' && address !== null ? address.port : 0;
      server.close(() => {
        resolve(port);
      });
    });
  });
}

let app: DemoApp;
let port: number;
let nextId = 2000;
/** Every message the engine sent, in order. */
const transcript: Message[] = [];

beforeAll(async () => {
  port = await freePort();
  app = await startDemoApp({
    env: {
      DEMO_PASSWORD: PASSWORD,
      [RECORDING_HEADLESS_VARIABLE]: '1',
      [RECORDING_DEBUG_PORT_VARIABLE]: String(port),
    },
  });
});
afterAll(async () => {
  await app.close();
});

/** The next message, kept in the transcript and checked against its schema. */
async function next(method?: string, timeoutMs = 20_000): Promise<Message> {
  const message = await app.engine.next(timeoutMs);
  transcript.push(message);
  expect(checkMessage(message, method)).toEqual([]);
  return message;
}

/** Reads messages until one fits; returns every message read. */
async function until(fits: (message: Message) => boolean, method?: string): Promise<Message[]> {
  const read: Message[] = [];
  for (;;) {
    const message = await next(method);
    read.push(message);
    if (fits(message)) return read;
  }
}

async function request(method: string, params: unknown): Promise<Message> {
  const id = nextId++;
  app.engine.send({ jsonrpc: '2.0', id, method, params });
  const read = await until((message) => message.id === id, method);
  return read.at(-1) ?? {};
}

function isEvent(name: string): (message: Message) => boolean {
  return (message) => message.method === name;
}

function paramsOf(message: Message | undefined): Record<string, unknown> {
  return (message?.params ?? {}) as Record<string, unknown>;
}

/** The recording browser's first page, through its remote debugging port. */
async function recordingPage(): Promise<{ browser: Browser; page: Page }> {
  const browser = await chromium.connectOverCDP(`http://127.0.0.1:${String(port)}`);
  const page = browser.contexts()[0]?.pages()[0];
  if (page === undefined) throw new Error('The recording browser has no page.');
  return { browser, page };
}

/** Moves the mouse to the middle of an element and clicks there, as a person does. */
async function clickAt(page: Page, element: Locator): Promise<void> {
  await element.waitFor();
  const box = await element.boundingBox();
  if (box === null) throw new Error('The element has no box.');
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 3 });
  await page.mouse.down();
  await page.mouse.up();
}

describe('recording over the protocol', () => {
  it('a whole session: steps, a changed fill, a notice, stop, verify', async () => {
    const started = await request('startRecording', {
      file: 'tests/recorded/p1.test.yaml',
      startUrl: '/todos',
    });
    const { recordingId } = started.result as { recordingId: string };
    expect(started.result).toEqual({ recordingId, file: 'tests/recorded/p1.test.yaml' });
    const opening = await until(
      (message) => message.method === 'stepRecorded' && paramsOf(message).index === 0,
    );
    expect(opening.map((message) => message.method)).toEqual(['recordingStarted', 'stepRecorded']);

    const { browser, page } = await recordingPage();
    await clickAt(page, page.getByLabel('New to-do'));
    await page.keyboard.type('Buy milk');
    await page.keyboard.press('Enter');
    await page.getByText('Buy milk').waitFor();
    await clickAt(page, page.getByLabel('New to-do'));
    await page.keyboard.type('Walk');
    // A click on the background ends the typing: a fill, and a notice.
    await page.mouse.click(5, 700);
    await clickAt(page, page.getByLabel('New to-do'));
    await page.keyboard.type(' the dog');
    await clickAt(page, page.getByRole('button', { name: 'Add' }));
    await page.getByText('Walk the dog').waitFor();
    const recorded = await until(
      (message) => message.method === 'stepRecorded' && paramsOf(message).index === 4,
    );
    await browser.close();

    const stopped = await request('stopRecording', { recordingId });
    expect(stopped.result).toEqual({ file: 'tests/recorded/p1.test.yaml', steps: 5 });
    const verify = await request('verifyRecording', { recordingId });
    const { runId } = verify.result as { runId: string };
    const run = await until(isEvent('recordingVerified'));

    const shape = (message: Message): unknown => {
      const params = paramsOf(message);
      return [message.method, params.index ?? params.kind ?? params.reason ?? params.status];
    };
    expect([...recorded].map(shape)).toEqual([
      ['stepRecorded', 1],
      ['stepRecorded', 2],
      ['stepRecorded', 3],
      ['recordingNotice', 'background'],
      ['stepChanged', 3],
      ['stepRecorded', 4],
    ]);
    expect(paramsOf(recorded.find(isEvent('stepChanged')))).toEqual({
      recordingId,
      index: 3,
      step: { action: 'fill', params: { target: 'todos.newTodo', value: 'Walk the dog' } },
    });
    expect(paramsOf(recorded.at(0))).toEqual({
      recordingId,
      index: 1,
      step: { action: 'fill', params: { target: 'todos.newTodo', value: 'Buy milk' } },
      targets: {
        'todos.newTodo': [
          { role: 'textbox', name: 'New to-do' },
          { label: 'New to-do' },
          { css: '#new-todo' },
        ],
      },
    });
    expect(transcript.find(isEvent('recordingStopped'))?.params).toEqual({
      recordingId,
      file: 'tests/recorded/p1.test.yaml',
      reason: 'stopped',
      steps: 5,
    });
    expect(paramsOf(run.find(isEvent('runFinished')))).toMatchObject({ runId, status: 'passed' });
    expect(paramsOf(run.at(-1))).toEqual({ recordingId, runId, status: 'passed' });
    console.log(transcript.map((message) => JSON.stringify(message)).join('\n'));

    // A failed verify: the page no longer fits the recording.
    const file = join(app.root, 'tests/recorded/p1.test.yaml');
    // Every candidate of the Add button now finds nothing.
    writeFileSync(
      file,
      readFileSync(file, 'utf8')
        .replace('name: Add\n', 'name: Add it\n')
        .replace('text: Add\n', 'text: Add it\n')
        .replace("css: '#add-form > button'", "css: '#add-form > .gone'"),
    );
    await request('verifyRecording', { recordingId });
    const failed = await until(isEvent('recordingVerified'));
    expect(paramsOf(failed.find(isEvent('stepFailed')))).toMatchObject({
      stepId: 'steps.4',
      error: { code: 'TargetNotFound' },
    });
    expect(paramsOf(failed.at(-1))).toMatchObject({ recordingId, status: 'failed' });
  });

  it('refuses what a recording in progress forbids, and unknown or existing things', async () => {
    const started = await request('startRecording', {
      file: 'tests/recorded/p2.test.yaml',
      startUrl: '/todos',
    });
    const { recordingId } = started.result as { recordingId: string };
    const errorName = (message: Message): unknown =>
      (message.error as { data?: { name?: string } } | undefined)?.data?.name;

    expect(
      errorName(await request('startRecording', { file: 'tests/recorded/p3.test.yaml' })),
    ).toBe('RecordingInProgress');
    expect(errorName(await request('startRun', { files: ['tests/forms.test.yaml'] }))).toBe(
      'RecordingInProgress',
    );
    expect(errorName(await request('openProject', { root: app.root }))).toBe('RecordingInProgress');
    expect(errorName(await request('verifyRecording', { recordingId }))).toBe(
      'RecordingInProgress',
    );
    await request('stopRecording', { recordingId });

    expect(errorName(await request('stopRecording', { recordingId: 'rec-none' }))).toBe(
      'RecordingNotFound',
    );
    expect(errorName(await request('verifyRecording', { recordingId: 'rec-none' }))).toBe(
      'RecordingNotFound',
    );
    expect(
      errorName(await request('startRecording', { file: 'tests/recorded/p2.test.yaml' })),
    ).toBe('FileExists');
    expect(errorName(await request('startRecording', { file: 'tests/recorded/p4.yaml' }))).toBe(
      'InvalidParams',
    );

    // A run in progress refuses a recording.
    writeFileSync(
      join(app.root, 'tests/recorded/long.test.yaml'),
      [
        'version: 1',
        'name: Waits',
        'steps:',
        '  - goto: /todos',
        "  - wait.element: { target: [{ css: '#never' }] }",
        '    timeout: 30s',
        '',
      ].join('\n'),
    );
    const run = await request('startRun', { files: ['tests/recorded/long.test.yaml'] });
    const { runId } = run.result as { runId: string };
    expect(
      errorName(await request('startRecording', { file: 'tests/recorded/p5.test.yaml' })),
    ).toBe('RunInProgress');
    await request('cancelRun', { runId });
    await until(isEvent('runFinished'));
  });

  it('a login: the password is in no message, and the file holds the secret', async () => {
    const started = await request('startRecording', {
      file: 'tests/recorded/p6.test.yaml',
      startUrl: '/login',
    });
    const { recordingId } = started.result as { recordingId: string };
    await until((message) => message.method === 'stepRecorded');
    const { browser, page } = await recordingPage();
    await clickAt(page, page.getByLabel('Username'));
    await page.keyboard.type('alice');
    await clickAt(page, page.getByLabel('Password'));
    await page.keyboard.type(PASSWORD);
    await clickAt(page, page.getByRole('button', { name: 'Sign in' }));
    await page.waitForURL('**/account');
    await until((message) => message.method === 'stepRecorded' && paramsOf(message).index === 3);
    await browser.close();
    await request('stopRecording', { recordingId });
    await request('verifyRecording', { recordingId });
    const verified = await until(isEvent('recordingVerified'));
    expect(paramsOf(verified.at(-1))).toMatchObject({ status: 'passed' });

    expect(readFileSync(join(app.root, 'tests/recorded/p6.test.yaml'), 'utf8')).toContain(
      "value: '${secrets.DEMO_PASSWORD}'",
    );
    const everything = [...transcript.map((message) => JSON.stringify(message)), app.engine.stderr];
    for (const text of everything) expect(text).not.toContain(PASSWORD);
    expect(app.engine.notJson).toEqual([]);
  });
});

describe('a recording when the client goes away', () => {
  it('stops, closes its browser and keeps the file', async () => {
    const ownPort = await freePort();
    const own = await startDemoApp({
      env: {
        [RECORDING_HEADLESS_VARIABLE]: '1',
        [RECORDING_DEBUG_PORT_VARIABLE]: String(ownPort),
      },
    });
    try {
      own.engine.send({
        jsonrpc: '2.0',
        id: 9,
        method: 'startRecording',
        params: { file: 'tests/recorded/gone.test.yaml', startUrl: '/todos' },
      });
      for (;;) {
        const message = await own.engine.next(20_000);
        if (message.method === 'stepRecorded') break;
      }
      const versionUrl = `http://127.0.0.1:${String(ownPort)}/json/version`;
      expect((await fetch(versionUrl)).ok).toBe(true);
      own.engine.child.stdin.end();
      expect(await own.engine.exitCode(20_000)).toBe(0);
      await expect(fetch(versionUrl)).rejects.toThrow();
      expect(readFileSync(join(own.root, 'tests/recorded/gone.test.yaml'), 'utf8')).toContain(
        '  - goto: /todos',
      );
    } finally {
      await own.close();
    }
  });
});
