// Integration tests of the data and HTTP actions (plan branch 13) through the
// demo app harness: samples S4, S9 and S19. For S9 and S19 the parts of A9
// that later go through the command line are checked here through the events
// and the run folder: neither the secret (in any encoded form) nor the token
// appears in them.
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { PRODUCT } from '@cfe/protocol';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { collectRun, eventsOf, type CollectedRun } from '../testing/run.js';

/** A secret with characters that JSON, URL and HTML encode differently. */
const SECRET = 'Sesame & <open> "now" 42';

let app: DemoApp;
let nextId = 1500;

beforeAll(async () => {
  app = await startDemoApp({ env: { DEMO_PASSWORD: SECRET } });
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

/** Every file under a folder, as text. */
function filesUnder(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? filesUnder(path) : [readFileSync(path, 'latin1')];
  });
}

/** Everything the engine wrote out: the run's events and every file of the data folder. */
function output(collected: CollectedRun): string {
  return [JSON.stringify(collected.events), ...filesUnder(join(app.root, PRODUCT.dataDir))].join(
    '\n',
  );
}

function htmlEncoded(text: string): string {
  return text.replace(/[&<>"]/g, (char) => `&#${String(char.charCodeAt(0))};`);
}

describe('S4: network', () => {
  it('mocks, waits for the response the click caused, checks responses and calls the API', async () => {
    const collected = await run('tests/network.test.yaml');
    expect(failures(collected)).toEqual([]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
  });

  it('expect.response says what it got', async () => {
    const file = testFile('response-fail', [
      'version: 1',
      'name: A response that is not as expected',
      'steps:',
      '  - goto: /orders',
      '  - click: orders.load',
      '  - expect.response: { url: /api/orders, json: [{ item: Grinder }] }',
      '    timeout: 1s',
    ]);
    expect(failures(await run(file))[0]).toMatchObject({
      code: 'AssertionFailed',
      message: 'The response to "/api/orders" is not as expected.',
      expected: { json: [{ item: 'Grinder' }] },
      actual: {
        status: 200,
        json: [
          { id: 1, item: 'Desk lamp' },
          { id: 2, item: 'Notebook' },
        ],
      },
    });
  });

  it('api fails on an unexpected status', async () => {
    const file = testFile('api-fail', [
      'version: 1',
      'name: An API call that is refused',
      'steps:',
      '  - api: { url: /api/protected }',
    ]);
    expect(failures(await run(file))[0]).toMatchObject({
      code: 'AssertionFailed',
      message: 'GET /api/protected answered with status 401, expected a 2xx or 3xx status.',
    });
  });
});

describe('S9: secrets', () => {
  it('the secret is used in a fill, a header and a page, and written out nowhere', async () => {
    const collected = await run('tests/secrets.test.yaml');
    expect(failures(collected)).toEqual([]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
    const written = output(collected);
    for (const form of [
      SECRET,
      JSON.stringify(SECRET).slice(1, -1),
      encodeURIComponent(SECRET),
      htmlEncoded(SECRET),
    ]) {
      expect(written).not.toContain(form);
    }
  });
});

describe('S19: a token from a response header', () => {
  async function lastToken(): Promise<string> {
    const answer = (await (await fetch(`${app.url}/api/token/last`)).json()) as {
      token: string | null;
    };
    expect(answer.token).toEqual(expect.any(String));
    return answer.token ?? '';
  }

  it('sends the token on and writes it out nowhere', async () => {
    const collected = await run('tests/header-token.test.yaml');
    expect(failures(collected)).toEqual([]);
    expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'passed' });
    expect(output(collected)).not.toContain(await lastToken());
  });

  it('masks the token in a failure that names it', async () => {
    const file = testFile('token-fail', [
      'version: 1',
      'name: A failure that names the token',
      'steps:',
      '  - goto: /token',
      '  - click: token.get',
      '  - wait.response: { url: /api/token, as: issued }',
      "  - expect.text: { target: token.status, equals: '${vars.issued.headers.authorization}' }",
      '    timeout: 1s',
    ]);
    const collected = await run(file);
    expect(failures(collected)[0]).toMatchObject({ code: 'AssertionFailed', expected: '•••' });
    expect(output(collected)).not.toContain(await lastToken());
  });

  it('warns, naming the header, when a value is too short to mask', async () => {
    const file = testFile('short-token', [
      'version: 1',
      'name: A token too short to mask',
      'steps:',
      "  - mock: { url: /api/token, headers: { Authorization: 'Bearer ab' }, json: {} }",
      '  - goto: /token',
      '  - click: token.get',
      '  - wait.response: { url: /api/token, as: issued }',
    ]);
    const collected = await run(file);
    expect(failures(collected)).toEqual([]);
    expect(eventsOf(collected.events, 'log')).toContainEqual(
      expect.objectContaining({
        level: 'warn',
        message:
          'A value in the authorization header is shorter than 4 characters, so it is not masked.',
      }),
    );
  });
});
