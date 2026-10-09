// Protocol test: with a secret set in the engine's environment, a message that
// would contain the secret reaches the client masked.
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { EngineProcess } from '../testing/engine-process.js';

const SECRET = 'hunter2secret';
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
});
