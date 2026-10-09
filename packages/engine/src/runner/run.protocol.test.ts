// Protocol tests of startRun's checks that need no browser (docs/protocol.md,
// "startRun"): nothing runs when a selected file has errors, and an unknown
// environment, an unknown browser or a missing Chromium are refused with a
// message that says what to do. The engine runs with an empty Playwright
// browser folder, so it finds no browser even where one is installed.
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { copyDemoProject } from '../testing/demo-app.js';
import { EngineProcess } from '../testing/engine-process.js';
import { checkMessage } from '../testing/protocol-schemas.js';
import { INSTALL_COMMAND } from './browser.js';

let engine: EngineProcess;
let root: string;
const noBrowsers = mkdtempSync(join(tmpdir(), 'cfe-no-browsers-'));

beforeAll(async () => {
  root = copyDemoProject('http://127.0.0.1:9');
  engine = new EngineProcess(undefined, undefined, {
    PLAYWRIGHT_BROWSERS_PATH: noBrowsers,
    DEMO_PASSWORD: 'demo-password-1234',
  });
  engine.initialize(0);
  await engine.next();
});
afterAll(() => {
  if (engine.child.exitCode === null) engine.child.kill();
  rmSync(root, { recursive: true, force: true });
  rmSync(noBrowsers, { recursive: true, force: true });
});

let nextId = 1;
async function startRun(params: Record<string, unknown>): Promise<Record<string, unknown>> {
  const response = await engine.request(nextId++, 'startRun', params);
  expect(checkMessage(response, 'startRun')).toEqual([]);
  return response;
}

describe('startRun without a browser', () => {
  it('reports capabilities.browsers as an empty list', async () => {
    const other = new EngineProcess(undefined, undefined, { PLAYWRIGHT_BROWSERS_PATH: noBrowsers });
    other.initialize(0);
    expect(await other.next()).toMatchObject({ result: { capabilities: { browsers: [] } } });
    other.child.kill();
  });

  it('needs an open project', async () => {
    expect(await startRun({ files: ['tests/user-action.test.yaml'] })).toMatchObject({
      error: { code: -32003, data: { name: 'ProjectNotOpen' } },
    });
    expect(await engine.request(nextId++, 'openProject', { root })).toMatchObject({
      result: { diagnostics: [] },
    });
  });

  it('runs nothing when a selected file has errors, and lists them', async () => {
    const response = await startRun({
      files: ['tests/user-action.test.yaml', 'fixtures/invalid/schema-errors.test.yaml'],
    });
    expect(response).toMatchObject({
      error: { code: -32005, data: { name: 'StepFilesInvalid' } },
    });
    const diagnostics = (response.error as { data: { diagnostics: { file: string }[] } }).data
      .diagnostics;
    expect(diagnostics.length).toBeGreaterThan(3);
    expect(new Set(diagnostics.map((diagnostic) => diagnostic.file))).toEqual(
      new Set(['fixtures/invalid/schema-errors.test.yaml']),
    );
    // No run started: the next message is the answer to the next request.
    expect(await engine.request(nextId++, 'listActions')).toHaveProperty('result');
  });

  it('only runs test files', async () => {
    expect(await startRun({ files: ['cfe.config.yaml'] })).toMatchObject({
      error: {
        data: {
          name: 'StepFilesInvalid',
          diagnostics: [{ file: 'cfe.config.yaml', code: 'NotATestFile' }],
        },
      },
    });
  });

  it('refuses an unknown environment', async () => {
    expect(
      await startRun({ files: ['tests/user-action.test.yaml'], env: 'nowhere' }),
    ).toMatchObject({
      error: {
        code: -32602,
        message: 'There is no environment "nowhere". The config has: local.',
      },
    });
  });

  it('refuses when Chromium is not installed, with the command that installs it', async () => {
    const response = await startRun({ files: ['tests/user-action.test.yaml'] });
    expect(response).toMatchObject({ error: { code: -32602 } });
    expect((response.error as { message: string }).message).toBe(
      `Chromium is not installed for this engine. Install it with: ${INSTALL_COMMAND}`,
    );
    expect(INSTALL_COMMAND).toMatch(/^npx playwright@\d+\.\d+\.\d+ install chromium$/);
  });

  it('refuses a browser it does not know', async () => {
    expect(
      await startRun({ files: ['tests/user-action.test.yaml'], options: { browser: 'firefox' } }),
    ).toMatchObject({
      error: {
        code: -32602,
        message: expect.stringContaining('cannot run the browser "firefox"') as string,
      },
    });
  });
});
