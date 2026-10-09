// Argument handling for the command-line program. Kept separate from main.ts so it
// can be unit tested without spawning a process. Running step files is added in
// Milestone 1.

import { PRODUCT, PROTOCOL_VERSION } from '@test-tool/protocol';

/** Version of the command-line client. */
export const CLI_VERSION = '0.0.0';

/** Exit code: success. */
export const EXIT_OK = 0;
/** Exit code: the command line could not be understood. */
export const EXIT_USAGE = 2;

const USAGE = `Usage: ${PRODUCT.command} [options]

Options:
  --version   Print the CLI and protocol versions
  --help      Print this help
`;

/**
 * Minimal writable output used by {@link runCli}, satisfied by `process.stdout`.
 */
export interface Output {
  /** Writes text without adding a newline. */
  write(text: string): unknown;
}

/**
 * Runs the command line and returns the process exit code.
 *
 * @param args - Command-line arguments without the node and script paths.
 * @param out - Where normal output is written.
 * @param err - Where error output is written.
 * @returns {@link EXIT_OK} on success, {@link EXIT_USAGE} for unknown arguments.
 *
 * @example
 * ```ts
 * process.exitCode = runCli(process.argv.slice(2), process.stdout, process.stderr);
 * ```
 */
export function runCli(args: readonly string[], out: Output, err: Output): number {
  const [first] = args;
  if (first === undefined || first === '--help') {
    out.write(USAGE);
    return EXIT_OK;
  }
  if (first === '--version') {
    out.write(`${PRODUCT.command} ${CLI_VERSION} (protocol ${PROTOCOL_VERSION})\n`);
    return EXIT_OK;
  }
  err.write(`Unknown argument "${first}". Run "${PRODUCT.command} --help" for usage.\n`);
  return EXIT_USAGE;
}
