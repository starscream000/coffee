// Test helper: the demo app harness. Starts the demo web server
// (examples/demo-app/server) on a free port, copies the demo project to a
// temporary folder with the server's URL as the environment's base URL, starts
// the engine as a child process and opens the copy. `close` stops both and
// deletes the copy; call it in `afterAll`/`finally` so it also runs when a test
// fails. Used by the integration tests; not part of the engine build.

import { spawn, type ChildProcess } from 'node:child_process';
import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PRODUCT, type Diagnostic } from '@cfe/protocol';
import { EngineProcess } from './engine-process.js';

/** The demo project in the repository. */
export const DEMO_PROJECT = fileURLToPath(
  new URL('../../../../examples/demo-app/', import.meta.url),
);

/** The demo web server's entry point. */
export const DEMO_SERVER = join(DEMO_PROJECT, 'server', 'server.ts');

/** The base URL the demo project's config names; the harness replaces it with the server's. */
export const CONFIG_BASE_URL = 'http://localhost:4310';

/** How long the server may take to print its URL. */
const START_TIMEOUT_MS = 10_000;

/** A running demo server and an engine with the demo project open. */
export interface DemoApp {
  /** The server's base URL, such as `http://127.0.0.1:53124`. */
  readonly url: string;
  /** The temporary copy of the demo project that the engine opened. */
  readonly root: string;
  /** The engine, initialised, with the project open. */
  readonly engine: EngineProcess;
  /** The diagnostics `openProject` returned. */
  readonly diagnostics: readonly Diagnostic[];
  /** Stops the engine and the server and deletes the copy. Safe to call twice. */
  close(): Promise<void>;
}

/** Options for {@link startDemoApp}. */
export interface DemoAppOptions {
  /** Extra environment variables for the engine, such as declared secrets. */
  readonly env?: Readonly<Record<string, string>>;
}

/** Starts the demo server and resolves with its URL once it is listening. */
function startServer(): Promise<{ server: ChildProcess; url: string }> {
  const server = spawn(process.execPath, [DEMO_SERVER, '--port', '0'], {
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let output = '';
  return new Promise((resolve, reject) => {
    const fail = (reason: string): void => {
      clearTimeout(timer);
      server.kill();
      reject(new Error(`The demo server did not start: ${reason}. Output: ${output}`));
    };
    const timer = setTimeout(() => {
      fail(`no URL within ${String(START_TIMEOUT_MS)} ms`);
    }, START_TIMEOUT_MS);
    server.stdout.setEncoding('utf8').on('data', (text: string) => {
      output += text;
      const url = /listening on (http:\/\/\S+)/.exec(output)?.[1];
      if (url !== undefined) {
        clearTimeout(timer);
        resolve({ server, url });
      }
    });
    server.stderr.setEncoding('utf8').on('data', (text: string) => {
      output += text;
    });
    server.on('exit', (code) => {
      fail(`it exited with code ${String(code)}`);
    });
  });
}

/** Stops a child process and waits until it has exited. */
function stop(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null || child.signalCode !== null) return Promise.resolve();
  return new Promise((resolve) => {
    child.once('exit', () => {
      resolve();
    });
    child.kill();
  });
}

/**
 * Copies the demo project to a temporary folder and points its environments at
 * `url` by replacing {@link CONFIG_BASE_URL} in the config file.
 *
 * @param url - The running server's base URL.
 * @returns The copy's folder.
 * @throws Error when the config no longer names {@link CONFIG_BASE_URL}.
 */
export function copyDemoProject(url: string): string {
  const root = mkdtempSync(join(tmpdir(), 'cfe-demo-'));
  cpSync(DEMO_PROJECT, root, {
    recursive: true,
    // Not the original's engine data folder or installed packages.
    filter: (path) =>
      !relative(DEMO_PROJECT, path)
        .split(sep)
        .some((part) => part === PRODUCT.dataDir || part === 'node_modules'),
  });
  const configFile = join(root, PRODUCT.configFile);
  const config = readFileSync(configFile, 'utf8');
  if (!config.includes(CONFIG_BASE_URL)) {
    rmSync(root, { recursive: true, force: true });
    throw new Error(`The demo config no longer names ${CONFIG_BASE_URL}; update the harness.`);
  }
  writeFileSync(configFile, config.split(CONFIG_BASE_URL).join(url));
  return root;
}

/**
 * Starts the demo server and an engine with the demo project open.
 *
 * @param options - Extra environment variables for the engine.
 * @returns The running app; call `close` when done.
 * @throws Error when the server does not start or the project cannot be opened;
 *   everything started so far is stopped first.
 *
 * @example
 * ```ts
 * const app = await startDemoApp();
 * try {
 *   await page.goto(`${app.url}/todos`);
 * } finally {
 *   await app.close();
 * }
 * ```
 */
export async function startDemoApp(options: DemoAppOptions = {}): Promise<DemoApp> {
  const { server, url } = await startServer();
  let root: string | undefined;
  let engine: EngineProcess | undefined;
  let closed = false;
  const close = async (): Promise<void> => {
    if (closed) return;
    closed = true;
    if (engine !== undefined) await stop(engine.child);
    await stop(server);
    if (root !== undefined) rmSync(root, { recursive: true, force: true });
  };
  try {
    root = copyDemoProject(url);
    engine = new EngineProcess(undefined, undefined, options.env);
    engine.initialize(0);
    await engine.next();
    const response = await engine.request(1, 'openProject', { root });
    const result = response.result as { diagnostics: Diagnostic[] } | undefined;
    if (result === undefined) {
      throw new Error(`openProject failed: ${JSON.stringify(response)}`);
    }
    return { url, root, engine, diagnostics: result.diagnostics, close };
  } catch (error) {
    await close();
    throw error;
  }
}
