// Turns whatever a step throws into the `ErrorInfo` a tester reads
// (docs/architecture.md, "Errors"; docs/protocol.md, "Shared types"): a stable
// code, a message, the step's location and, for assertions, the expected and
// actual values. Secrets are masked later, by the message writer.

import type { ErrorInfo, Location } from '@cfe/protocol';
import { InterpolationError } from '../context/interpolate.js';
import { LocateError, type LevelReport } from '../locate/locate.js';

/** Why a step ended early, when the runner ended it. */
export type StepStop = 'timeout' | 'cancelled';

/**
 * A step failure the engine raises itself, with its code and the fields of
 * `ErrorInfo`.
 *
 * @example
 * ```ts
 * throw new StepError('NotImplemented', 'The built-in action "hover" cannot run yet.');
 * ```
 */
export class StepError extends Error {
  /** Stable, machine-readable code. */
  readonly code: string;
  /** Optional next step for the tester. */
  readonly hint: string | undefined;

  /**
   * @param code - Stable code, such as `NotImplemented`.
   * @param message - What went wrong, for the tester.
   * @param hint - What to do about it.
   */
  constructor(code: string, message: string, hint?: string) {
    super(message);
    this.name = 'StepError';
    this.code = code;
    this.hint = hint;
  }
}

/**
 * A saved login's flow failed. Every step that needs the login fails with the
 * flow's own error and location (ADR 0018).
 */
export class LoginFailedError extends Error {
  /** The failing flow step's error, with its location in the flow file. */
  readonly info: ErrorInfo;

  /**
   * @param login - The login's name.
   * @param info - The error of the flow step that failed.
   */
  constructor(login: string, info: ErrorInfo) {
    super(`The saved login "${login}" could not sign in: ${info.message}`);
    this.name = 'LoginFailedError';
    this.info = { ...info, message: this.message };
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

/** Playwright's message without its call log, which repeats the action. */
function playwrightMessage(message: string): string {
  return message.split(/\n\s*Call log:/)[0]?.trim() ?? message;
}

/** The outermost level whose search failed: inner levels were never searched. */
function failedLevel(level: LevelReport): LevelReport {
  for (const outer of [level.frame, level.within]) {
    if (outer !== undefined && !outer.candidates.some((candidate) => candidate.matches === 1)) {
      return failedLevel(outer);
    }
  }
  return level;
}

/**
 * The `candidates` of an `ErrorInfo` for `TargetNotFound`: the candidates of
 * the level where the search stopped (the element itself, or the frame or
 * `within` target it lies in), each with its last match count. The protocol
 * has no place for the level or for a candidate never tried, so candidates
 * never tried are left out; the message names every level.
 */
function notFoundCandidates(report: LevelReport): NonNullable<ErrorInfo['candidates']> {
  return failedLevel(report).candidates.flatMap(({ candidate, matches }) =>
    matches === null ? [] : [{ candidate: { ...candidate }, matches }],
  );
}

/**
 * Describes a step failure for `stepFailed.error`.
 *
 * @param error - What the step threw.
 * @param location - The step's location in its file.
 * @param stop - Set when the runner ended the step: its timeout passed, or the
 *   run was cancelled.
 * @param timeoutMs - The step's timeout, for the message.
 * @returns The protocol's `ErrorInfo`.
 */
export function toErrorInfo(
  error: unknown,
  location: Location,
  stop?: StepStop,
  timeoutMs?: number,
): ErrorInfo {
  const at = { location };
  if (stop === 'cancelled') {
    return { code: 'Cancelled', message: 'The run was cancelled during this step.', ...at };
  }
  if (stop === 'timeout' || (error instanceof Error && error.name === 'TimeoutError')) {
    return {
      code: 'ActionTimeout',
      message: `The step did not finish within its timeout${timeoutMs === undefined ? '' : ` of ${formatMs(timeoutMs)}`}.`,
      hint: 'Check that the page reaches the expected state, or give the step a longer "timeout".',
      ...at,
    };
  }
  if (error instanceof LoginFailedError) {
    return error.info;
  }
  if (error instanceof StepError) {
    return {
      code: error.code,
      message: error.message,
      ...(error.hint === undefined ? {} : { hint: error.hint }),
      ...at,
    };
  }
  if (error instanceof LocateError) {
    return {
      code: error.code,
      message: error.message,
      ...at,
      ...(error.report === undefined ? {} : { candidates: notFoundCandidates(error.report) }),
    };
  }
  if (error instanceof InterpolationError) {
    return { code: error.code, message: error.message, ...at };
  }
  // SDK errors are recognised by their tag, not by instanceof (docs/actions.md).
  if (isRecord(error) && typeof error.message === 'string') {
    if (error.sdkError === 'AssertionError') {
      return {
        code: 'AssertionFailed',
        message: error.message,
        ...('expected' in error ? { expected: error.expected } : {}),
        ...('actual' in error ? { actual: error.actual } : {}),
        ...(typeof error.hint === 'string' ? { hint: error.hint } : {}),
        ...at,
      };
    }
    if (error.sdkError === 'ActionError') {
      return {
        code: 'ActionError',
        message: error.message,
        ...(typeof error.hint === 'string' ? { hint: error.hint } : {}),
        ...at,
      };
    }
  }
  if (error instanceof Error) {
    if (error.name === 'TargetClosedError' || error.message.includes('has been closed')) {
      return {
        code: 'PageClosed',
        message: 'The page was closed while the step was running.',
        hint: 'Check whether the page closes itself, or a previous step closed it.',
        ...at,
      };
    }
    return { code: 'ActionError', message: playwrightMessage(error.message), ...at };
  }
  return { code: 'ActionError', message: `The action failed with ${String(error)}.`, ...at };
}

/**
 * Writes milliseconds the way step files write durations.
 *
 * @param ms - Milliseconds.
 * @returns Such as `10s` or `1500ms`.
 */
export function formatMs(ms: number): string {
  return ms % 1000 === 0 ? `${String(ms / 1000)}s` : `${String(ms)}ms`;
}
