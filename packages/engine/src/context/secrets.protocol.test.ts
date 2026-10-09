// Protocol test: with a secret set in the engine's environment, a message that
// would contain the secret reaches the client masked.
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { protocolSchemas } from '@cfe/protocol';
import { afterEach, describe, expect, it } from 'vitest';
import { EngineProcess } from '../testing/engine-process.js';

const SECRET = 'hunter2secret';
const TEST_ENGINES = fileURLToPath(new URL('../../test/engines/', import.meta.url));
const engines: EngineProcess[] = [];
const roots: string[] = [];
afterEach(() => {
  for (const engine of engines.splice(0)) {
    if (engine.child.exitCode === null) engine.child.kill();
  }
  for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true });
});

describe('secret masking through the protocol', () => {
  it('masks a declared secret in a diagnostic message that would contain it', async () => {
    const root = mkdtempSync(join(tmpdir(), 'cfe-mask-'));
    roots.push(root);
    writeFileSync(
      join(root, 'cfe.config.yaml'),
      'version: 1\nenvironments:\n  local:\n    baseUrl: http://localhost:4310\nsecrets: [DEMO_PASSWORD]\n',
    );
    const engine = new EngineProcess(undefined, undefined, { DEMO_PASSWORD: SECRET });
    engines.push(engine);
    engine.initialize(0);
    await engine.next();
    expect(await engine.request(1, 'openProject', { root })).toMatchObject({
      result: { diagnostics: [] },
    });
    // An unknown action named like the secret: its message would quote the value.
    engine.child.stdin.write(
      `${JSON.stringify({
        jsonrpc: '2.0',
        id: 2,
        method: 'validate',
        params: {
          content: {
            file: 'tests/a.test.yaml',
            text: `version: 1\nname: T\nsteps:\n  - ${SECRET}\n`,
          },
        },
      })}\n`,
    );
    const response = await engine.next();
    expect(JSON.stringify(response)).not.toContain(SECRET);
    expect(response).toMatchObject({
      result: { diagnostics: [{ code: 'UnknownAction', message: '"•••" is not an action.' }] },
    });
  });

  // Review 0004, finding 1: secrets equal to protocol keys or to pieces of the
  // serialised text used to break the message. Each must leave a valid message.
  it.each(['jsonrpc', 'result', 'line', '2.0","'])(
    'a secret equal to %j leaves the message valid JSON that matches its schema',
    async (secret) => {
      const root = mkdtempSync(join(tmpdir(), 'cfe-mask-'));
      roots.push(root);
      writeFileSync(
        join(root, 'cfe.config.yaml'),
        'version: 1\nenvironments:\n  local:\n    baseUrl: http://localhost:4310\nsecrets: [DEMO_PASSWORD]\n',
      );
      const engine = new EngineProcess(undefined, undefined, { DEMO_PASSWORD: secret });
      engines.push(engine);
      engine.initialize(0);
      await engine.next();
      expect(await engine.request(1, 'openProject', { root })).toMatchObject({
        result: { diagnostics: [] },
      });
      const response = await engine.request(2, 'validate', {
        content: {
          file: 'tests/a.test.yaml',
          text: `version: 1\nname: T\nsteps:\n  - ${JSON.stringify(secret)}\n`,
        },
      });
      expect(engine.notJson).toEqual([]);
      const schemas = protocolSchemas();
      expect(schemas.get('envelope.success-response')?.safeParse(response).success).toBe(true);
      expect(schemas.get('request.validate.result')?.safeParse(response.result).success).toBe(true);
      expect(response).toMatchObject({
        jsonrpc: '2.0',
        id: 2,
        result: {
          diagnostics: [
            {
              file: 'tests/a.test.yaml',
              line: 4,
              severity: 'error',
              code: 'UnknownAction',
              message: '"•••" is not an action.',
            },
          ],
        },
      });
    },
  );

  // Review 0004, finding 4: stderr is an exit point too.
  it("masks a secret that an action file's top-level code logs to the console", async () => {
    const root = mkdtempSync(join(tmpdir(), 'cfe-mask-'));
    roots.push(root);
    writeFileSync(
      join(root, 'cfe.config.yaml'),
      'version: 1\nenvironments:\n  local:\n    baseUrl: http://localhost:4310\nsecrets: [DEMO_PASSWORD]\n',
    );
    mkdirSync(join(root, 'actions'));
    writeFileSync(
      join(root, 'actions', 'leak.ts'),
      [
        "import { defineAction, z } from '@cfe/engine/sdk';",
        "console.log('password is', process.env.DEMO_PASSWORD);",
        'console.error(`error with ${String(process.env.DEMO_PASSWORD)}`);',
        'export default defineAction({',
        "  name: 'leak.nothing',",
        "  description: 'Logs a secret while loading.',",
        '  params: z.strictObject({}),',
        '  run: () => Promise.resolve(),',
        '});',
      ].join('\n'),
    );
    const engine = new EngineProcess(undefined, undefined, { DEMO_PASSWORD: SECRET });
    engines.push(engine);
    engine.initialize(0);
    await engine.next();
    expect(await engine.request(1, 'openProject', { root })).toMatchObject({
      result: { diagnostics: [] },
    });
    const stderr = await engine.waitForStderr('error with');
    expect(stderr).toContain('password is •••');
    expect(stderr).toContain('error with •••');
    expect(stderr).not.toContain(SECRET);
  });

  it('masks a secret in the message of an internal error logged on stderr', async () => {
    const engine = new EngineProcess(join(TEST_ENGINES, 'secret-crash.mjs'), []);
    engines.push(engine);
    expect(await engine.exitCode()).toBe(1);
    const stderr = await engine.waitForStderr('internal failure');
    expect(stderr).toContain('Unexpected internal error: Error: internal failure with ••• inside');
    expect(stderr).not.toContain(SECRET);
  });
});
