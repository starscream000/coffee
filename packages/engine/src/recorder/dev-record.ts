// A developer command to try the recorder (instruction 0009, task 12):
// `pnpm record --project <dir> --url <start URL> --file <test file>`, with
// optional `--env <name>` and `--login <name>`. It opens the recording
// browser, prints each recorded step and notice as it happens and, when the
// browser is closed, runs verify and prints the result. It is a script of the
// repository, not the product's command line, and goes away once the desktop
// app's Record screen exists.

import { pathToFileURL } from 'node:url';
import { BUILTIN_ACTIONS } from '../actions/builtins/index.js';
import { Project } from '../project/project.js';
import { stepValue, type RecordedStep, type RecordedTarget } from './document.js';
import { RecordingSession, type RecordingNotice } from './session.js';
import { verifyRecording, type VerifyResult } from './verify.js';

/** The command's options. */
export interface RecordArguments {
  /** The project's root folder. */
  readonly project: string;
  /** Where the browser starts. */
  readonly url: string;
  /** The test file to write, relative to the project root. */
  readonly file: string;
  /** The environment. */
  readonly env?: string | undefined;
  /** A saved login for the main page. */
  readonly login?: string | undefined;
}

const FLAGS = ['--project', '--url', '--file', '--env', '--login'];

const USAGE =
  'Usage: pnpm record --project <dir> --url <start URL> --file <tests/name.test.yaml> [--env <name>] [--login <name>]';

/**
 * Reads the command line.
 *
 * @param argv - The arguments after the script's name.
 * @returns The options.
 * @throws Error that names the unknown argument, the option without a value or
 *   the missing options, followed by the usage.
 */
export function parseArguments(argv: readonly string[]): RecordArguments {
  const values = new Map<string, string>();
  for (let index = 0; index < argv.length; index++) {
    const flag = argv[index];
    if (flag === undefined || flag === '--') continue;
    if (!FLAGS.includes(flag)) {
      throw new Error(
        `Unknown argument "${flag}". Each option is a flag followed by one value; type passwords into the browser, not here.\n${USAGE}`,
      );
    }
    const value = argv[index + 1];
    if (value === undefined) throw new Error(`The option ${flag} needs a value.\n${USAGE}`);
    values.set(flag.slice(2), value);
    index += 1;
  }
  const project = values.get('project');
  const url = values.get('url');
  const file = values.get('file');
  const missing = [
    ...(project === undefined ? ['--project'] : []),
    ...(url === undefined ? ['--url'] : []),
    ...(file === undefined ? ['--file'] : []),
  ];
  if (project === undefined || url === undefined || file === undefined) {
    throw new Error(`Missing ${missing.join(', ')}.\n${USAGE}`);
  }
  return { project, url, file, env: values.get('env'), login: values.get('login') };
}

/**
 * One recorded or changed step as the command prints it.
 *
 * @param index - The step's index in the file.
 * @param step - The step.
 * @param changed - Whether it is a change of an earlier step.
 * @param targets - Targets the step made.
 * @returns Lines to print.
 */
export function formatStep(
  index: number,
  step: RecordedStep,
  changed: boolean,
  targets: Readonly<Record<string, RecordedTarget>> = {},
): string[] {
  const lines = [
    `${changed ? '~' : '+'} steps.${String(index)}  ${JSON.stringify(stepValue(step))}`,
  ];
  for (const [name, target] of Object.entries(targets)) {
    lines.push(`    target ${name}: ${JSON.stringify(target)}`);
  }
  if (step.review !== undefined) lines.push(`    review: ${step.review}`);
  return lines;
}

/**
 * A notice as the command prints it.
 *
 * @param notice - The notice.
 * @returns One line.
 */
export function formatNotice(notice: RecordingNotice): string {
  return `! ${notice.kind} (${notice.page}): ${notice.message}`;
}

/**
 * The result of a verify as the command prints it: every step, and for a
 * failed one its error and the match count of every candidate.
 *
 * @param result - The verify's outcome.
 * @returns Lines to print.
 */
export function formatVerify(result: VerifyResult): string[] {
  const lines = [`Verify: ${result.status} (run ${result.runId})`];
  for (const step of result.steps) {
    const mark = step.status === 'passed' ? 'ok  ' : step.status === 'failed' ? 'FAIL' : 'skip';
    lines.push(`  ${mark} ${step.stepId}  ${step.title}`);
    if (step.error !== undefined) {
      lines.push(`       ${step.error.code}: ${step.error.message}`);
      for (const level of step.error.candidates ?? []) {
        lines.push(
          `       candidate ${JSON.stringify(level.candidate)} matched ${String(level.matches)}`,
        );
      }
    }
  }
  lines.push(
    result.status === 'passed'
      ? 'The recording is runnable.'
      : 'The recording is not runnable yet: fix the failed step, then record or verify again.',
  );
  return lines;
}

async function main(argv: readonly string[]): Promise<void> {
  const args = parseArguments(argv);
  const project = await Project.open(args.project, BUILTIN_ACTIONS);
  const print = (lines: string | string[]): void => {
    for (const line of typeof lines === 'string' ? [lines] : lines) {
      process.stdout.write(`${project.mask(line)}\n`);
    }
  };
  const session = await RecordingSession.start(
    { project, file: args.file, startUrl: args.url, environment: args.env, login: args.login },
    {
      stepRecorded: (index, step, targets) => {
        print(formatStep(index, step, false, targets));
      },
      stepChanged: (index, step) => {
        print(formatStep(index, step, true));
      },
      notice: (notice) => {
        print(formatNotice(notice));
      },
    },
  );
  print(`Recording into ${args.file}. Use the browser; close it to stop.`);
  const end = await session.ended;
  await session.stop();
  print(`Recording ${end === 'browserClosed' ? 'ended: the browser was closed' : 'stopped'}.`);
  print('Verifying…');
  print(formatVerify(await verifyRecording(project, args.file, { environment: args.env })));
}

const entry = process.argv[1];
if (entry !== undefined && import.meta.url === pathToFileURL(entry).href) {
  main(process.argv.slice(2)).catch((error: unknown) => {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
  });
}
