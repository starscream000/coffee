// The demo web server that integration tests run step files against: Node's
// own http module, no framework, no dependency. Node runs this file directly
// (type stripping), so there is no build step:
//
//   node examples/demo-app/server/server.ts [--port 4310]
//
// With `--port 0` the system picks a free port. Once listening, it prints one
// line, `Demo app listening on http://127.0.0.1:<port>`, which the test
// harness reads. State (the to-do list and the reset count) lives in memory.

import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import type { AddressInfo } from 'node:net';
import {
  checkoutFramePage,
  fallbackPage,
  framesPage,
  helpPage,
  homePage,
  paymentFramePage,
  receiptPage,
  settingsPage,
  slowRenderPage,
  tabsPage,
  todosPage,
} from './pages.ts';

/** One to-do item. */
interface Todo {
  title: string;
  done: boolean;
}

/** Everything the server remembers; `POST /api/reset` empties it. */
interface State {
  todos: Todo[];
  /** How many times `POST /api/reset` was called, so tests can prove `after` steps ran. */
  resets: number;
}

const state: State = { todos: [], resets: 0 };

const PAGES: Readonly<Record<string, string>> = {
  '/': homePage,
  '/todos': todosPage,
  '/fallback': fallbackPage,
  '/slow-render': slowRenderPage,
  '/frames': framesPage,
  '/frames/checkout': checkoutFramePage,
  '/frames/payment': paymentFramePage,
  '/tabs': tabsPage,
  '/receipt': receiptPage,
  '/help': helpPage,
  '/settings': settingsPage,
};

function send(response: ServerResponse, status: number, type: string, body: string): void {
  response.writeHead(status, {
    'content-type': `${type}; charset=utf-8`,
    'cache-control': 'no-store',
  });
  response.end(body);
}

function sendJson(response: ServerResponse, status: number, value: unknown): void {
  send(response, status, 'application/json', JSON.stringify(value));
}

async function readJson(request: IncomingMessage): Promise<unknown> {
  const chunks: Buffer[] = [];
  for await (const chunk of request) chunks.push(chunk as Buffer);
  const text = Buffer.concat(chunks).toString('utf8');
  return text === '' ? undefined : (JSON.parse(text) as unknown);
}

function isTodoInput(value: unknown): value is { title: string; done?: boolean } {
  if (typeof value !== 'object' || value === null) return false;
  const { title, done } = value as Record<string, unknown>;
  return (
    typeof title === 'string' && title !== '' && (done === undefined || typeof done === 'boolean')
  );
}

async function handle(request: IncomingMessage, response: ServerResponse): Promise<void> {
  const path = new URL(request.url ?? '/', 'http://localhost').pathname;
  const method = request.method ?? 'GET';
  const page = PAGES[path];
  if (method === 'GET' && page !== undefined) {
    send(response, 200, 'text/html', page);
  } else if (path === '/api/todos' && method === 'GET') {
    sendJson(response, 200, state.todos);
  } else if (path === '/api/todos' && method === 'POST') {
    const input = await readJson(request);
    if (!isTodoInput(input)) {
      sendJson(response, 400, { error: 'Send { "title": "<text>", "done": <boolean> }.' });
      return;
    }
    state.todos.push({ title: input.title, done: input.done ?? false });
    sendJson(response, 201, state.todos);
  } else if (path === '/api/reset' && method === 'POST') {
    state.todos = [];
    state.resets += 1;
    sendJson(response, 200, state);
  } else if (path === '/api/state' && method === 'GET') {
    sendJson(response, 200, state);
  } else {
    send(response, 404, 'text/plain', `Nothing at ${method} ${path}.`);
  }
}

/**
 * Starts the demo server.
 *
 * @param port - The port to listen on; 0 lets the system pick a free one.
 * @returns The server's base URL, such as `http://127.0.0.1:4310`.
 */
export function startServer(port: number): Promise<string> {
  const server = createServer((request, response) => {
    handle(request, response).catch((error: unknown) => {
      sendJson(response, 500, { error: error instanceof Error ? error.message : String(error) });
    });
  });
  return new Promise((resolve) => {
    server.listen(port, '127.0.0.1', () => {
      const { port: chosen } = server.address() as AddressInfo;
      resolve(`http://127.0.0.1:${String(chosen)}`);
    });
  });
}

const portFlag = process.argv.indexOf('--port');
const port = portFlag === -1 ? 4310 : Number(process.argv[portFlag + 1]);
if (!Number.isInteger(port) || port < 0 || port > 65_535) {
  console.error('Usage: node examples/demo-app/server/server.ts [--port <0-65535>]');
  process.exit(2);
}
const url = await startServer(port);
console.log(`Demo app listening on ${url}`);
