// Integration tests of step errors a tester can read (instruction 0006, task
// 14), of a built-in without an implementation (task 15), and of the stdio
// guard (task 8; review 0005, finding 6) at load time and inside `run`. The
// test writes its own action file and test files into the harness's copy of
// the demo project and opens it again.
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { collectRun, eventsOf } from '../testing/run.js';

const SECRET = 'demo-password-1234';

const ACTIONS = `// Actions for the runner's error tests.
import { defineAction, z } from '@cfe/engine/sdk';

process.stdout.write(\`load stdout \${String(process.env.DEMO_PASSWORD)}\\n\`);
process.stderr.write(\`load stderr \${String(process.env.DEMO_PASSWORD)}\\n\`);

export default [
  defineAction({
    name: 'test.throwPlain',
    description: 'Throws an error that is not an SDK error.',
    params: z.strictObject({}),
    run: async () => {
      throw new TypeError('plain failure');
    },
  }),
  defineAction({
    name: 'test.closePage',
    description: 'Closes the page, then looks for a button on it.',
    params: z.strictObject({}),
    run: async (ctx) => {
      await ctx.page.close();
      await ctx.locate([{ css: 'button' }]);
    },
  }),
  defineAction({
    name: 'test.ignoreSignal',
    description: 'Waits 5 seconds and ignores ctx.signal.',
    params: z.strictObject({}),
    run: () => new Promise((resolve) => setTimeout(resolve, 5_000)),
  }),
  defineAction({
    name: 'test.writeStdio',
    description: 'Writes the secret to stdout and stderr and logs it.',
    params: z.strictObject({}),
    run: async (ctx) => {
      process.stdout.write(\`run stdout \${ctx.secrets.get('DEMO_PASSWORD')}\\n\`);
      process.stderr.write(\`run stderr \${ctx.secrets.get('DEMO_PASSWORD')}\\n\`);
      ctx.log.info(\`logged \${ctx.secrets.get('DEMO_PASSWORD')}\`);
    },
  }),
];
`;

const TESTS: Record<string, string> = {
  'not-implemented': '  - goto: /todos\n  - hover: todos.add\n',
  'plain-error': '  - test.throwPlain\n',
  'page-closed': '  - goto: /todos\n  - test.closePage\n',
  'invalid-selector':
    "  - goto: /todos\n  - click:\n      target:\n        - css: 'div[['\n        - testId: todo-count\n",
  timeout: '  - test.ignoreSignal:\n    timeout: 500ms\n',
  stdio: '  - test.writeStdio\n',
};

let app: DemoApp;
let nextId = 200;

beforeAll(async () => {
  app = await startDemoApp({ env: { DEMO_PASSWORD: SECRET } });
  mkdirSync(join(app.root, 'tests', 'errors'), { recursive: true });
  writeFileSync(join(app.root, 'actions', 'errors.ts'), ACTIONS);
  for (const [name, steps] of Object.entries(TESTS)) {
    writeFileSync(
      join(app.root, 'tests', 'errors', `${name}.test.yaml`),
      `version: 1\nname: ${name}\nsteps:\n${steps}`,
    );
  }
  const reopened = await app.engine.request(nextId++, 'openProject', { root: app.root });
  expect(reopened).toMatchObject({ result: { diagnostics: [] } });
});
afterAll(async () => {
  await app.close();
});

async function failureOf(name: string): Promise<Record<string, unknown>> {
  const collected = await collectRun(app.engine, nextId++, {
    files: [`tests/errors/${name}.test.yaml`],
  });
  expect(collected.invalid).toEqual([]);
  const [failed] = eventsOf(collected.events, 'stepFailed');
  expect(eventsOf(collected.events, 'runFinished')[0]).toMatchObject({ status: 'failed' });
  return (failed?.error ?? {}) as Record<string, unknown>;
}

describe('step errors a tester can read', () => {
  it('a built-in action without an implementation fails its step with NotImplemented', async () => {
    expect(await failureOf('not-implemented')).toMatchObject({
      code: 'NotImplemented',
      message: 'The built-in action "hover" cannot run yet in this engine version.',
      location: { file: 'tests/errors/not-implemented.test.yaml', line: 5, column: 5 },
    });
    // The engine is still there.
    expect(await app.engine.request(nextId++, 'listActions')).toHaveProperty('result');
  });

  it('an action that throws something that is not an SDK error is an ActionError', async () => {
    expect(await failureOf('plain-error')).toMatchObject({
      code: 'ActionError',
      message: 'plain failure',
      location: { file: 'tests/errors/plain-error.test.yaml', line: 4, column: 5 },
    });
  });

  it('a page closed during a step is PageClosed (review 0005, finding 7)', async () => {
    expect(await failureOf('page-closed')).toMatchObject({
      code: 'PageClosed',
      message: 'The page was closed while the step was running.',
    });
  });

  it('a candidate Playwright rejects is InvalidSelector, at once', async () => {
    const error = await failureOf('invalid-selector');
    expect(error).toMatchObject({ code: 'InvalidSelector' });
    expect(error.message).toMatch(/^The candidate css="div\[\[" of the inline target target/);
  });

  it('a step that outlives its timeout is ActionTimeout', async () => {
    expect(await failureOf('timeout')).toMatchObject({
      code: 'ActionTimeout',
      message: 'The step did not finish within its timeout of 500ms.',
    });
  });
});

describe('stdout and stderr belong to the engine', () => {
  it('sends writes to stdout to stderr, masked, at load time and inside run', async () => {
    const collected = await collectRun(app.engine, nextId++, {
      files: ['tests/errors/stdio.test.yaml'],
    });
    expect(collected.invalid).toEqual([]);
    expect(eventsOf(collected.events, 'log')).toEqual([
      expect.objectContaining({ level: 'info', message: 'logged •••' }),
    ]);
    const stderr = await app.engine.waitForStderr('run stderr');
    for (const line of ['load stdout •••', 'load stderr •••', 'run stdout •••', 'run stderr •••']) {
      expect(stderr).toContain(line);
    }
    expect(stderr).not.toContain(SECRET);
    expect(app.engine.notJson).toEqual([]);
  });
});
