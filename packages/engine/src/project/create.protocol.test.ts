// Protocol test for createProject (instruction 0010, task 9): a client creates
// a project in a new folder, the engine opens it with no diagnostics, and a
// folder that is not empty is refused with FolderNotEmpty. Every message is
// checked against the protocol's JSON Schema files.
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { EngineProcess } from '../testing/engine-process.js';
import { checkMessage } from '../testing/protocol-schemas.js';

const engines: EngineProcess[] = [];
const folders: string[] = [];

afterEach(() => {
  for (const engine of engines.splice(0)) {
    if (engine.child.exitCode === null) engine.child.kill();
  }
  for (const folder of folders.splice(0)) rmSync(folder, { recursive: true, force: true });
});

async function engine(): Promise<EngineProcess> {
  const started = new EngineProcess();
  engines.push(started);
  started.initialize(0);
  await started.next();
  return started;
}

describe('createProject', () => {
  it('creates and opens a project that has no diagnostics', async () => {
    const parent = mkdtempSync(join(tmpdir(), 'create-protocol-'));
    folders.push(parent);
    const root = join(parent, 'shop-tests');
    const client = await engine();
    const created = await client.request(1, 'createProject', {
      root,
      name: 'Shop tests',
      baseUrl: 'http://localhost:5173',
    });
    expect(checkMessage(created, 'createProject')).toEqual([]);
    expect(created.result).toMatchObject({
      environments: ['local'],
      defaultEnvironment: 'local',
      logins: [],
      diagnostics: [],
    });
    const tests = await client.request(2, 'listTests', {});
    expect(tests.result).toEqual({ tests: [] });
  });

  it('refuses a folder that is not empty', async () => {
    const root = mkdtempSync(join(tmpdir(), 'create-protocol-'));
    folders.push(root);
    writeFileSync(join(root, 'notes.txt'), 'mine\n');
    const client = await engine();
    const refused = await client.request(1, 'createProject', {
      root,
      name: 'x',
      baseUrl: 'http://localhost:5173',
    });
    expect(checkMessage(refused)).toEqual([]);
    expect(refused.error).toMatchObject({
      code: -32014,
      message: `"${root}" is not empty: it holds notes.txt. Choose an empty folder, or a new one.`,
      data: { name: 'FolderNotEmpty' },
    });
  });
});
