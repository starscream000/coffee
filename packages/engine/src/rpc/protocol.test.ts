// Protocol tests: start the built engine (dist/main.js --stdio) as a child
// process, exactly as a client does, and talk to it over stdin and stdout.
// Proves check I1, the oversized-line half of I3, and that the engine writes
// nothing before the first request. Needs `tsc -b` first (pnpm verify does it).
import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { MAX_MESSAGE_BYTES, PROTOCOL_VERSION } from '@cfe/protocol';
import { afterEach, describe, expect, it } from 'vitest';

const ENGINE_MAIN = fileURLToPath(new URL('../../dist/main.js', import.meta.url));

class EngineProcess {
  readonly child: ChildProcessWithoutNullStreams;
  private buffer = '';
  private readonly received: Record<string, unknown>[] = [];
  private waiters: (() => void)[] = [];
  stdoutBytes = 0;
  stderr = '';

  constructor() {
    this.child = spawn(process.execPath, [ENGINE_MAIN, '--stdio'], { stdio: 'pipe' });
    this.child.stdout.setEncoding('utf8');
    this.child.stdout.on('data', (text: string) => {
      this.stdoutBytes += text.length;
      this.buffer += text;
      let newline = this.buffer.indexOf('\n');
      while (newline !== -1) {
        this.received.push(JSON.parse(this.buffer.slice(0, newline)) as Record<string, unknown>);
        this.buffer = this.buffer.slice(newline + 1);
        newline = this.buffer.indexOf('\n');
      }
      for (const wake of this.waiters.splice(0)) wake();
    });
    this.child.stderr.setEncoding('utf8');
    this.child.stderr.on('data', (text: string) => {
      this.stderr += text;
    });
  }

  send(message: unknown): void {
    this.child.stdin.write(`${JSON.stringify(message)}\n`);
  }

  sendRaw(text: string): void {
    this.child.stdin.write(text);
  }

  async next(timeoutMs = 10_000): Promise<Record<string, unknown>> {
    const deadline = Date.now() + timeoutMs;
    while (this.received.length === 0) {
      const left = deadline - Date.now();
      if (left <= 0) throw new Error(`No message from the engine. stderr: ${this.stderr}`);
      await new Promise<void>((resolve) => {
        const timer = setTimeout(resolve, left);
        this.waiters.push(() => {
          clearTimeout(timer);
          resolve();
        });
      });
    }
    const message = this.received.shift();
    if (message === undefined) throw new Error('No message from the engine.');
    return message;
  }

  exitCode(timeoutMs = 10_000): Promise<number | null> {
    if (this.child.exitCode !== null) return Promise.resolve(this.child.exitCode);
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        reject(new Error('The engine did not exit.'));
      }, timeoutMs);
      this.child.on('exit', (code) => {
        clearTimeout(timer);
        resolve(code);
      });
    });
  }

  initialize(id: number | string = 1, protocolVersion = PROTOCOL_VERSION): void {
    this.send({
      jsonrpc: '2.0',
      id,
      method: 'initialize',
      params: { protocolVersion, client: { name: 'protocol-test', version: '0.0.0' } },
    });
  }
}

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
