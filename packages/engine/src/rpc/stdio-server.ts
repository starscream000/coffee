// Runs one protocol session over this process's stdin and stdout, with the exit
// codes of docs/protocol.md: 0 after `shutdown` or when stdin closes, 1 on an
// unexpected internal error, 3 after a refused handshake.

import { BUILTIN_SPECS_BY_NAME } from '../actions/builtin-specs.js';
import { getEngineInfo } from '../engine-info.js';
import { registerProjectHandlers } from '../project/handlers.js';
import { redirectConsoleToStderr } from './console.js';
import { LineReader } from './line-reader.js';
import { MessageWriter } from './message-writer.js';
import { EXIT_OK, Session } from './session.js';

/** Exit code for an unexpected internal error. */
export const EXIT_INTERNAL_ERROR = 1;

/**
 * Starts the engine's protocol server on stdin and stdout and keeps it running
 * until the session ends. Nothing is written to stdout before the first
 * request arrives.
 *
 * @returns The session, so later branches can register more request handlers.
 *
 * @example
 * ```ts
 * // packages/engine/src/main.ts
 * runStdioServer();
 * ```
 */
export function runStdioServer(): Session {
  redirectConsoleToStderr();

  const logError = (message: string): void => {
    process.stderr.write(`${message}\n`);
  };
  process.on('uncaughtException', (error) => {
    logError(`Unexpected internal error: ${error.stack ?? error.message}`);
    process.exit(EXIT_INTERNAL_ERROR);
  });
  process.on('unhandledRejection', (reason) => {
    logError(
      `Unexpected internal error: ${reason instanceof Error ? (reason.stack ?? reason.message) : String(reason)}`,
    );
    process.exit(EXIT_INTERNAL_ERROR);
  });

  const writer = new MessageWriter(process.stdout);
  const session = new Session(writer, {
    engineInfo: getEngineInfo(),
    // No browser can run yet; the runner branches add Chromium.
    browsers: [],
    exit: (code) => {
      process.exit(code);
    },
    logError,
  });
  registerProjectHandlers(session, BUILTIN_SPECS_BY_NAME);

  const reader = new LineReader();
  process.stdin.on('data', (chunk: Buffer) => {
    for (const item of reader.push(chunk)) {
      void session.receive(item);
    }
  });
  process.stdin.on('end', () => {
    for (const item of reader.end()) {
      void session.receive(item);
    }
    // Answer every request already received before exiting.
    void session.idle().then(() => {
      process.exit(EXIT_OK);
    });
  });
  return session;
}
