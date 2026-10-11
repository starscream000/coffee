// Runs one protocol session over this process's stdin and stdout, with the exit
// codes of docs/protocol.md: 0 after `shutdown` or when stdin closes, 1 on an
// unexpected internal error, 3 after a refused handshake. Everything the engine
// writes, to stdout or to stderr, passes through the secret registry first.
// Before the process exits, any run is cancelled and its browser closed; if
// the process is killed outright, Chromium exits on its own when its pipe to
// Playwright closes.

import { BUILTIN_ACTIONS } from '../actions/builtins/index.js';
import { SecretRegistry } from '../context/mask.js';
import { getEngineInfo } from '../engine-info.js';
import { registerProjectHandlers } from '../project/handlers.js';
import { RecordingManager } from '../recorder/manager.js';
import { RunManager } from '../runner/run.js';
import { availableBrowsers, INSTALL_COMMAND } from '../runner/browser.js';
import { redirectConsoleToStderr } from './console.js';
import { LineReader } from './line-reader.js';
import { MessageWriter } from './message-writer.js';
import { guardStdio } from './stdio-guard.js';
import { EXIT_OK, Session } from './session.js';

/** Exit code for an unexpected internal error. */
export const EXIT_INTERNAL_ERROR = 1;

/** What {@link runStdioServer} starts. */
export interface StdioServer {
  /** The protocol session, so more request handlers can be registered. */
  readonly session: Session;
  /** The engine-wide secret registry every output passes through. */
  readonly secrets: SecretRegistry;
}

/**
 * Starts the engine's protocol server on stdin and stdout and keeps it running
 * until the session ends. Nothing is written to stdout before the first
 * request arrives.
 *
 * @returns The session and the secret registry.
 *
 * @example
 * ```ts
 * // packages/engine/src/main.ts
 * runStdioServer();
 * ```
 */
export function runStdioServer(): StdioServer {
  // Every message and every line on stderr passes through the registry (ADR 0014).
  const secrets = new SecretRegistry();
  const mask = (text: string): string => secrets.mask(text);
  const stdout = guardStdio(mask);
  // process.stderr now masks every write, so the console can write to it directly.
  redirectConsoleToStderr();

  const logError = (message: string): void => {
    process.stderr.write(`${mask(message)}\n`);
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

  const writer = new MessageWriter(stdout, { mask });
  // The run manager sends events through the session created next.
  const engineInfo = getEngineInfo();
  const runs: RunManager = new RunManager(
    {
      render: (method, params): string => session.renderNotification(method, params),
      send: (line): void => {
        session.sendLine(line);
      },
    },
    { engineVersion: engineInfo.version, protocolVersion: engineInfo.protocolVersion },
  );
  const recordings = new RecordingManager(
    {
      render: (method, params): string => session.renderNotification(method, params),
      send: (line): void => {
        session.sendLine(line);
      },
    },
    runs,
  );
  const session: Session = new Session(writer, {
    engineInfo,
    browsers: availableBrowsers(),
    installCommand: INSTALL_COMMAND,
    exit: (code) => {
      process.exit(code);
    },
    logError,
    beforeShutdown: async () => {
      await Promise.all([runs.stop(), recordings.stopAll()]);
    },
  });
  registerProjectHandlers(session, BUILTIN_ACTIONS, secrets, runs, recordings);

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
    // Answer every request already received, cancel any run, stop any recording, then exit.
    void session
      .idle()
      .then(() => Promise.all([runs.stop(), recordings.stopAll()]))
      .finally(() => {
        process.exit(EXIT_OK);
      });
  });
  return { session, secrets };
}
