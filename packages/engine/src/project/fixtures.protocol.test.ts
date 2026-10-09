// Protocol tests for checks F4, F5 and F6: start the engine as a client would,
// open examples/demo-app and validate each invalid fixture. The diagnostics
// returned must equal the fixture's "# expect:" comments, no more and no fewer.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { afterEach, describe, expect, it } from 'vitest';
import { EngineProcess } from '../testing/engine-process.js';

const DEMO_APP = fileURLToPath(new URL('../../../../examples/demo-app/', import.meta.url));

/** Reads the "# expect: [file:]line:column Code" comments of a fixture. */
function expectations(fixture: string): string[] {
  const text = readFileSync(`${DEMO_APP}/${fixture}`, 'utf8');
  return [...text.matchAll(/^# expect: (?:(\S+):)?(\d+):(\d+) (\w+)$/gm)]
    .map(
      ([, file, line, column, code]) =>
        `${file ?? fixture}:${line ?? ''}:${column ?? ''} ${code ?? ''}`,
    )
    .sort();
}

interface Diagnostic {
  file: string;
  line: number;
  column: number;
  code: string;
  message: string;
}

const engines: EngineProcess[] = [];

afterEach(() => {
  for (const engine of engines.splice(0)) {
    if (engine.child.exitCode === null) engine.child.kill();
  }
});

async function openDemoApp(): Promise<EngineProcess> {
  const engine = new EngineProcess();
  engines.push(engine);
  engine.initialize(0);
  await engine.next();
  const opened = await engine.request(1, 'openProject', { root: DEMO_APP });
  expect(opened).toMatchObject({
    id: 1,
    result: { environments: ['local'], diagnostics: [] },
  });
  return engine;
}

async function validate(engine: EngineProcess, fixture: string): Promise<Diagnostic[]> {
  const response = await engine.request(2, 'validate', { files: [fixture] });
  const result = response.result as { diagnostics: Diagnostic[] } | undefined;
  if (result === undefined) throw new Error(`validate failed: ${JSON.stringify(response)}`);
  return result.diagnostics;
}

const brief = (diagnostics: Diagnostic[]): string[] =>
  diagnostics.map((d) => `${d.file}:${String(d.line)}:${String(d.column)} ${d.code}`).sort();

describe('validate through the protocol', () => {
  it.each([
    ['F4', 'fixtures/invalid/schema-errors.test.yaml'],
    ['F5', 'fixtures/invalid/flow-cycle.test.yaml'],
    ['F6', 'fixtures/invalid/yaml-syntax.test.yaml'],
  ])('%s: %s produces exactly its expected diagnostics', async (_check, fixture) => {
    const expected = expectations(fixture);
    expect(expected.length).toBeGreaterThan(0);
    const engine = await openDemoApp();
    expect(brief(await validate(engine, fixture))).toEqual(expected);
  });

  it('F4: gives the documented messages and hints', async () => {
    const engine = await openDemoApp();
    const diagnostics = await validate(engine, 'fixtures/invalid/schema-errors.test.yaml');
    const byCode = new Map(diagnostics.map((d) => [d.code, d]));
    expect(byCode.get('UnknownAction')).toMatchObject({
      message: '"exepct.text" is not an action.',
      hint: 'Did you mean "expect.text"?',
    });
    expect(byCode.get('MissingParameter')?.message).toBe('"fill" needs "value".');
    expect(byCode.get('UnknownKey')).toMatchObject({ hint: 'Did you mean "button"?' });
  });

  it('F5: names the chain of files in the cycle', async () => {
    const engine = await openDemoApp();
    const [cycle] = await validate(engine, 'fixtures/invalid/flow-cycle.test.yaml');
    expect(cycle?.message).toBe(
      'Flow calls itself: cycle-a.flow.yaml → cycle-b.flow.yaml → cycle-a.flow.yaml.',
    );
  });

  it('validates an unsaved buffer, with project-relative paths and forward slashes', async () => {
    const engine = await openDemoApp();
    const response = await engine.request(3, 'validate', {
      content: {
        file: 'tests/new.test.yaml',
        text: 'version: 1\nname: New\nsteps:\n  - clik: go\n',
      },
    });
    expect(response).toMatchObject({
      result: {
        diagnostics: [{ file: 'tests/new.test.yaml', line: 4, column: 5, code: 'UnknownAction' }],
      },
    });
  });

  it('answers ProjectNotOpen before openProject, and ProjectInvalid for a folder without config', async () => {
    const engine = new EngineProcess();
    engines.push(engine);
    engine.initialize(0);
    await engine.next();
    expect(await engine.request(1, 'validate', { files: ['a.test.yaml'] })).toMatchObject({
      error: { code: -32003, data: { name: 'ProjectNotOpen' } },
    });
    expect(await engine.request(2, 'openProject', { root: `${DEMO_APP}/fixtures` })).toMatchObject({
      error: { code: -32004, data: { name: 'ProjectInvalid' } },
    });
  });
});
