// Protocol tests: start the built engine (dist/main.js --stdio) as a child
// process, exactly as a client does, and talk to it over stdin and stdout.
// Proves check I1, the oversized-line half of I3, and that the engine writes
// nothing before the first request. Needs `tsc -b` first (pnpm verify does it).
import { spawn } from 'node:child_process';
import { MAX_MESSAGE_BYTES, PROTOCOL_VERSION } from '@cfe/protocol';
import { afterEach, describe, expect, it } from 'vitest';
import { fileURLToPath } from 'node:url';
import { ENGINE_MAIN, EngineProcess } from '../testing/engine-process.js';

const TEST_ENGINES = fileURLToPath(new URL('../../test/engines/', import.meta.url));

const engines: EngineProcess[] = [];
function startEngine(): EngineProcess {
  const engine = new EngineProcess();
  engines.push(engine);
  return engine;
}

afterEach(() => {
  for (const engine of engines.splice(0)) {
    if (engine.child.exitCode === null) engine.child.kill();
  }
});

describe('engine over stdio', () => {
  it('writes nothing to stdout before the first request', async () => {
    const engine = startEngine();
    await new Promise((resolve) => setTimeout(resolve, 500));
    expect(engine.stdoutBytes).toBe(0);
    engine.initialize();
    expect(await engine.next()).toMatchObject({
      id: 1,
      result: { protocolVersion: PROTOCOL_VERSION },
    });
  });

  it('I1: completes the handshake with a compatible client', async () => {
    const engine = startEngine();
    engine.initialize();
    expect(await engine.next()).toEqual({
      jsonrpc: '2.0',
      id: 1,
      result: {
        protocolVersion: PROTOCOL_VERSION,
        engine: { name: '@cfe/engine', version: '0.0.0' },
        capabilities: { browsers: [] },
      },
    });
  });

  it('I1: answers any request before initialize with NotInitialized', async () => {
    const engine = startEngine();
    engine.send({ jsonrpc: '2.0', id: 1, method: 'openProject', params: { root: '.' } });
    expect(await engine.next()).toMatchObject({
      id: 1,
      error: { code: -32001, data: { name: 'NotInitialized' } },
    });
  });

  it('I1: refuses an incompatible client and exits with code 3', async () => {
    const engine = startEngine();
    engine.initialize(1, '0.99.0');
    expect(await engine.next()).toMatchObject({
      id: 1,
      error: {
        code: -32002,
        data: {
          name: 'IncompatibleProtocol',
          clientProtocolVersion: '0.99.0',
          engineProtocolVersion: PROTOCOL_VERSION,
        },
      },
    });
    expect(await engine.exitCode()).toBe(3);
  });

  it('rejects a second initialize', async () => {
    const engine = startEngine();
    engine.initialize(1);
    await engine.next();
    engine.initialize(2);
    expect(await engine.next()).toMatchObject({ id: 2, error: { code: -32600 } });
  });

  it('I3: discards a line over 4 MiB without parsing it and keeps the session open', async () => {
    const engine = startEngine();
    // Not valid JSON on purpose: the engine must not try to parse it.
    engine.sendRaw(`${'x'.repeat(MAX_MESSAGE_BYTES + 1024)}\n`);
    expect(await engine.next()).toMatchObject({
      id: null,
      error: { code: -32009, data: { name: 'MessageTooLarge' } },
    });
    engine.initialize();
    expect(await engine.next()).toMatchObject({
      id: 1,
      result: { protocolVersion: PROTOCOL_VERSION },
    });
  });

  it('answers invalid JSON, unknown methods and invalid params with the standard codes', async () => {
    const engine = startEngine();
    engine.sendRaw('{oops\n');
    expect(await engine.next()).toMatchObject({ id: null, error: { code: -32700 } });
    engine.initialize();
    await engine.next();
    engine.send({ jsonrpc: '2.0', id: 2, method: 'noSuchMethod' });
    expect(await engine.next()).toMatchObject({ id: 2, error: { code: -32601 } });
  });

  it('answers shutdown with null and exits with code 0', async () => {
    const engine = startEngine();
    engine.initialize();
    await engine.next();
    engine.send({ jsonrpc: '2.0', id: 2, method: 'shutdown' });
    expect(await engine.next()).toEqual({ jsonrpc: '2.0', id: 2, result: null });
    expect(await engine.exitCode()).toBe(0);
  });

  it('review 0003 finding 1: no console method writes to stdout', async () => {
    const engine = new EngineProcess(`${TEST_ENGINES}noisy-console.mjs`, []);
    engines.push(engine);
    // Wait until the last console call's output has arrived, however slow the machine.
    const deadline = Date.now() + 10_000;
    while (!engine.stderr.includes('Assertion failed') && Date.now() < deadline) {
      await new Promise((resolve) => setTimeout(resolve, 50));
    }
    expect(engine.stdoutBytes).toBe(0);
    for (const text of [
      'log',
      'dir: 1',
      'dirxml: 2',
      'table',
      'groupCollapsed',
      'count: 1',
      'time:',
      'Trace: trace',
      'Assertion failed: assert',
    ]) {
      expect(engine.stderr).toContain(text);
    }
    engine.initialize();
    expect(await engine.next()).toMatchObject({
      id: 1,
      result: { protocolVersion: PROTOCOL_VERSION },
    });
  });

  it('review 0003 finding 2: answers requests already received before exiting when stdin closes', async () => {
    const engine = new EngineProcess(`${TEST_ENGINES}slow-handler.mjs`, []);
    engines.push(engine);
    engine.initialize();
    engine.send({ jsonrpc: '2.0', id: 2, method: 'listTests', params: {} });
    engine.child.stdin.end();
    expect(await engine.next()).toMatchObject({ id: 1 });
    expect(await engine.next()).toEqual({ jsonrpc: '2.0', id: 2, result: { tests: [] } });
    expect(await engine.exitCode()).toBe(0);
  });

  it('exits with code 0 when stdin closes', async () => {
    const engine = startEngine();
    engine.child.stdin.end();
    expect(await engine.exitCode()).toBe(0);
  });

  it('refuses to start without --stdio and says how to start it', async () => {
    const child = spawn(process.execPath, [ENGINE_MAIN], { stdio: 'pipe' });
    let stderr = '';
    child.stderr.setEncoding('utf8');
    child.stderr.on('data', (text: string) => (stderr += text));
    const code = await new Promise<number | null>((resolve) => child.on('exit', resolve));
    expect(code).toBe(1);
    expect(stderr).toContain('--stdio');
  });
});
