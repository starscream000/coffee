// Integration tests: when the engine exits during a run, for any reason, no
// browser process is left behind (instruction 0006, task 9). Each case starts
// an engine whose temporary folder marks its browser's processes, starts a
// long run, makes the engine exit, and waits until no marked process remains.
import { rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { browserProcessCount, markedTemp, waitForNoBrowser } from '../testing/processes.js';

const ACTIONS = `// Actions for the cleanup tests.
import { defineAction, z } from '@cfe/engine/sdk';

export default [
  defineAction({
    name: 'test.waitForAbort',
    description: 'Waits until ctx.signal is aborted.',
    params: z.strictObject({}),
    run: (ctx) =>
      new Promise((resolve) => {
        ctx.signal.addEventListener('abort', resolve);
      }),
  }),
  // Fails the engine from outside any step, as an engine bug would.
  defineAction({
    name: 'test.crashLater',
    description: 'Throws outside its own promise, then waits.',
    params: z.strictObject({}),
    run: () => {
      setTimeout(() => {
        throw new Error('internal failure for the cleanup test');
      }, 3_000);
      return new Promise((resolve) => setTimeout(resolve, 30_000));
    },
  }),
];
`;

const TESTS = {
  long: [
    'version: 1',
    'name: Waits a long time',
    'steps:',
    '  - test.waitForAbort:',
    '    timeout: 60s',
  ],
  crash: [
    'version: 1',
    'name: Crashes the engine',
    'steps:',
    '  - test.crashLater:',
    '    timeout: 60s',
  ],
};

/**
 * Starts an engine with a marked browser, runs a long test until its step has
 * started, then calls `exit` and checks that every browser process ended.
 */
async function exitDuringRun(
  file: keyof typeof TESTS,
  exit: (app: DemoApp) => void,
): Promise<{ exitCode: number | null; methods: string[] }> {
  const mark = markedTemp();
  const app = await startDemoApp({ env: { ...mark.env, DEMO_PASSWORD: 'demo-password-1234' } });
  try {
    writeFileSync(join(app.root, 'actions', 'cleanup.ts'), ACTIONS);
    writeFileSync(join(app.root, 'tests', `${file}.test.yaml`), `${TESTS[file].join('\n')}\n`);
    expect(await app.engine.request(10, 'openProject', { root: app.root })).toMatchObject({
      result: { diagnostics: [] },
    });
    app.engine.send({
      jsonrpc: '2.0',
      id: 11,
      method: 'startRun',
      params: { files: [`tests/${file}.test.yaml`] },
    });
    const methods: string[] = [];
    for (;;) {
      // Shorter than the test's timeout, so a failure shows the engine's stderr.
      const message = await app.engine.next(20_000);
      if (typeof message.method === 'string') methods.push(message.method);
      if (message.method === 'stepStarted') break;
    }
    expect(browserProcessCount(mark.marker)).toBeGreaterThan(0);

    exit(app);
    const exitCode = await app.engine.exitCode(45_000);
    // Everything the engine sent before it exited.
    for (;;) {
      try {
        const message = await app.engine.next(100);
        if (typeof message.method === 'string') methods.push(message.method);
      } catch {
        break;
      }
    }
    expect(await waitForNoBrowser(mark.marker)).toBe(0);
    return { exitCode, methods };
  } finally {
    await app.close();
    rmSync(mark.folder, { recursive: true, force: true });
  }
}

describe(
  'no browser is left behind when the engine exits during a run',
  { timeout: 90_000 },
  () => {
    it('after shutdown: the run is cancelled and finished first, then the engine exits 0', async () => {
      const { exitCode, methods } = await exitDuringRun('long', (app) => {
        app.engine.send({ jsonrpc: '2.0', id: 12, method: 'shutdown' });
      });
      expect(exitCode).toBe(0);
      expect(methods.slice(-3)).toEqual(['stepFailed', 'testFinished', 'runFinished']);
    });

    it('when stdin closes', async () => {
      const { exitCode } = await exitDuringRun('long', (app) => {
        app.engine.child.stdin.end();
      });
      expect(exitCode).toBe(0);
    });

    it('after an unexpected internal error', async () => {
      const { exitCode } = await exitDuringRun('crash', () => undefined);
      expect(exitCode).toBe(1);
    });

    it('when the engine process is killed', async () => {
      const { exitCode } = await exitDuringRun('long', (app) => {
        app.engine.child.kill('SIGKILL');
      });
      expect(exitCode).not.toBe(0);
    });
  },
);
