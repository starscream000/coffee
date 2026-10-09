// Test helper: starts the built engine (dist/main.js --stdio) as a child
// process, exactly as a client does, and exchanges protocol messages with it.
// Used by the protocol tests; not part of the engine build.

import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { PROTOCOL_VERSION } from '@cfe/protocol';

/** Absolute path of the built engine entry point. */
export const ENGINE_MAIN = fileURLToPath(new URL('../../dist/main.js', import.meta.url));

/** A running engine process and its message stream. */
export class EngineProcess {
  /** The child process. */
  readonly child: ChildProcessWithoutNullStreams;
  /** Characters received on stdout so far. */
  stdoutBytes = 0;
  /** Everything received on stderr so far. */
  stderr = '';
  /** Lines on stdout that were not JSON: must stay empty. */
  readonly notJson: string[] = [];
  private buffer = '';
  private readonly received: Record<string, unknown>[] = [];
  private waiters: (() => void)[] = [];

  /**
   * Starts `node dist/main.js --stdio`, or another entry script.
   *
   * @param entry - Script to run; defaults to the engine's main.
   * @param args - Arguments after the script.
   * @param env - Extra environment variables for the engine process.
   */
  constructor(
    entry: string = ENGINE_MAIN,
    args: readonly string[] = ['--stdio'],
    env: Readonly<Record<string, string>> = {},
  ) {
    this.child = spawn(process.execPath, [entry, ...args], {
      stdio: 'pipe',
      env: { ...process.env, ...env },
    });
    this.child.stdout.setEncoding('utf8');
    this.child.stdout.on('data', (text: string) => {
      this.stdoutBytes += text.length;
      this.buffer += text;
      let newline = this.buffer.indexOf('\n');
      while (newline !== -1) {
        const line = this.buffer.slice(0, newline);
        try {
          this.received.push(JSON.parse(line) as Record<string, unknown>);
        } catch {
          this.notJson.push(line);
        }
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

  /**
   * Sends one message as a JSON line.
   *
   * @param message - Any JSON value.
   */
  send(message: unknown): void {
    this.child.stdin.write(`${JSON.stringify(message)}\n`);
  }

  /**
   * Sends raw text, for malformed or oversized input.
   *
   * @param text - Exactly what to write to stdin.
   */
  sendRaw(text: string): void {
    this.child.stdin.write(text);
  }

  /**
   * Waits for the next message from the engine.
   *
   * @param timeoutMs - How long to wait.
   * @returns The parsed message.
   * @throws Error when nothing arrives in time.
   */
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

  /**
   * Sends a request and waits for the next message, normally its response.
   *
   * @param id - Request id.
   * @param method - Request method.
   * @param params - Request parameters.
   * @returns The next message.
   */
  async request(id: number, method: string, params?: unknown): Promise<Record<string, unknown>> {
    this.send({ jsonrpc: '2.0', id, method, ...(params === undefined ? {} : { params }) });
    return this.next();
  }

  /**
   * Waits until stderr contains some text. stdout and stderr are separate
   * pipes, so a line written to stderr before a response can arrive after it.
   *
   * @param text - Text to wait for.
   * @param timeoutMs - How long to wait.
   * @returns Everything received on stderr so far.
   * @throws Error when the text does not arrive in time.
   */
  async waitForStderr(text: string, timeoutMs = 10_000): Promise<string> {
    const deadline = Date.now() + timeoutMs;
    while (!this.stderr.includes(text)) {
      if (Date.now() > deadline) {
        throw new Error(`"${text}" never reached stderr. stderr: ${this.stderr}`);
      }
      await new Promise((resolve) => setTimeout(resolve, 10));
    }
    return this.stderr;
  }

  /**
   * Waits for the process to exit.
   *
   * @param timeoutMs - How long to wait.
   * @returns The exit code.
   */
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

  /**
   * Sends `initialize`.
   *
   * @param id - Request id.
   * @param protocolVersion - Version the fake client claims to speak.
   */
  initialize(id: number | string = 1, protocolVersion = PROTOCOL_VERSION): void {
    this.send({
      jsonrpc: '2.0',
      id,
      method: 'initialize',
      params: { protocolVersion, client: { name: 'protocol-test', version: '0.0.0' } },
    });
  }
}
