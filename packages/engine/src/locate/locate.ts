// `ctx.locate` (ADR 0010; docs/actions.md, "ctx.locate"): resolves a target to
// the Playwright locator of the first candidate that matches exactly one
// element. Frames and `within` targets are resolved from the outside in by the
// same rule; `${…}` is interpolated before each attempt; for the first
// `fallbackGrace` only first candidates are tried; it polls until the step's
// deadline and stops at once when the signal is aborted. Each call is reported
// as a LocatorUse, and each fallback as a LocatorFallback warning.

import type { LocatorUse } from '@cfe/protocol';
import type { Locator, Page } from 'playwright';
import { interpolate, type InterpolationScope } from '../context/interpolate.js';
import {
  CANDIDATE_KINDS,
  toLongTarget,
  type Candidate,
  type TargetValue,
} from '../schema/targets.js';
import { candidateLocator, describeCandidate, type SearchScope } from './candidates.js';

/** Longest pause between two attempts, in milliseconds (ADR 0010: at least every 100 ms). */
export const POLL_INTERVAL_MS = 50;

/** Deepest `frame` / `within` nesting followed before giving up. */
const MAX_DEPTH = 32;

/** A `LocatorFallback` warning, for the runner to send as a `log` event. */
export interface LocatorFallbackWarning {
  /** Always `LocatorFallback`. */
  readonly code: 'LocatorFallback';
  /** For the tester. */
  readonly message: string;
  /** The target's name (absent for an inline target) and the candidate used. */
  readonly data: { readonly target?: string; readonly candidateIndex: number };
}

/**
 * Receives what `ctx.locate` reports. The runner implements it: it adds each
 * use to the step result's `locators` and sends each warning as a `log` event
 * with the step's location.
 */
export interface LocatorReporter {
  /** Called once per call, whether the target was found or not. */
  locatorUsed(use: LocatorUse): void;
  /** Called for every level (frame, `within`, element) found by a candidate other than its first. */
  locatorFallback(warning: LocatorFallbackWarning): void;
}

/** Finds a named target's definition. */
export type TargetLookup = (name: string) => TargetValue | undefined;

/**
 * Looks a name up in the test or flow file's own targets, then in the
 * project's shared targets.
 *
 * @param local - The file's `targets`, if it has any.
 * @param shared - The shared targets of the project.
 * @returns A lookup for {@link LocateOptions.targets}.
 */
export function targetLookup(
  local: Readonly<Record<string, TargetValue>> | undefined,
  shared: ReadonlyMap<string, TargetValue>,
): TargetLookup {
  return (name) =>
    local !== undefined && Object.hasOwn(local, name) ? local[name] : shared.get(name);
}

/** What `locate` needs besides the target. */
export interface LocateOptions {
  /** The page to search. */
  readonly page: Page;
  /** Finds named targets. */
  readonly targets: TargetLookup;
  /** Values for `${…}` in candidates: the current step's vars, row, params and env. */
  readonly scope: InterpolationScope;
  /** The attribute `testId` candidates match (`defaults.testIdAttribute`). */
  readonly testIdAttribute: string;
  /** How long only first candidates are tried, in milliseconds. */
  readonly fallbackGraceMs: number;
  /** How long to keep trying: the step's remaining time, in milliseconds. */
  readonly timeoutMs: number;
  /** Aborted on step timeout or cancellation; stops the call at once. */
  readonly signal: AbortSignal;
  /** Receives the LocatorUse and any LocatorFallback warnings. */
  readonly reporter: LocatorReporter;
  /** The step parameter the target came from; defaults to `target`. */
  readonly param?: string;
}

/** Why `locate` failed. */
export type LocateErrorCode =
  'TargetNotFound' | 'InvalidSelector' | 'UnknownTarget' | 'TargetCycle';

/** One candidate in a {@link LocateError} report. */
export interface CandidateReport {
  /** The candidate, after interpolation when it was tried. */
  readonly candidate: Candidate;
  /** How many elements it matched at the last attempt; `null` when never tried. */
  readonly matches: number | null;
}

/** One level (element, frame or `within`) in a {@link LocateError} report. */
export interface LevelReport {
  /** `target` (or the step parameter), `frame` or `within`. */
  readonly param: string;
  /** The target's name; absent for an inline target. */
  readonly target?: string;
  /** Each candidate with its last match count. */
  readonly candidates: readonly CandidateReport[];
  /** The frame level, if any. */
  readonly frame?: LevelReport;
  /** The `within` level, if any. */
  readonly within?: LevelReport;
}

/** A target that could not be resolved, with every candidate's last match count. */
export class LocateError extends Error {
  /** Why it failed. */
  readonly code: LocateErrorCode;
  /** Every level and candidate with its last match count, for `TargetNotFound`. */
  readonly report: LevelReport | undefined;

  /**
   * @param code - Why it failed.
   * @param message - For the tester.
   * @param report - The candidates and their counts.
   */
  constructor(code: LocateErrorCode, message: string, report?: LevelReport) {
    super(message);
    this.name = 'LocateError';
    this.code = code;
    this.report = report;
  }
}

/** A level of the target being resolved, with the state of the last attempt. */
interface Level {
  readonly param: string;
  readonly name: string | undefined;
  readonly candidates: readonly Candidate[];
  /** Frame targets must match an `<iframe>` (or `<frame>`). */
  readonly isFrame: boolean;
  readonly frame: Level | undefined;
  readonly within: Level | undefined;
  /** Last match count of each candidate; `null` when never tried. */
  readonly counts: (number | null)[];
  /** Each candidate after its last interpolation. */
  readonly tried: (Candidate | undefined)[];
  /** The candidate used in the latest attempt, if any. */
  used: { index: number; candidate: Candidate } | undefined;
}

/** What one attempt found at a level. */
interface Found {
  readonly locator: Locator;
}

function buildLevel(
  value: TargetValue,
  param: string,
  isFrame: boolean,
  lookup: TargetLookup,
  chain: readonly string[],
): Level {
  let name: string | undefined;
  let definition = value;
  if (typeof value === 'string') {
    name = value;
    if (chain.includes(name)) {
      throw new LocateError(
        'TargetCycle',
        `The targets refer to each other in a cycle: ${[...chain, name].join(' → ')}.`,
      );
    }
    const found = lookup(name);
    if (found === undefined) {
      throw new LocateError('UnknownTarget', `There is no target named "${name}".`);
    }
    definition = found;
  }
  if (typeof definition === 'string') {
    // A name that only names another target: follow it.
    return buildLevel(
      definition,
      param,
      isFrame,
      lookup,
      name === undefined ? chain : [...chain, name],
    );
  }
  if (chain.length > MAX_DEPTH) {
    throw new LocateError(
      'TargetCycle',
      `The target nests "frame" and "within" more than ${String(MAX_DEPTH)} levels deep.`,
    );
  }
  const long = toLongTarget(definition);
  const inner = name === undefined ? [...chain, `(inline ${param})`] : [...chain, name];
  return {
    param,
    name,
    candidates: long.candidates,
    isFrame,
    frame:
      long.frame === undefined ? undefined : buildLevel(long.frame, 'frame', true, lookup, inner),
    within:
      long.within === undefined
        ? undefined
        : buildLevel(long.within, 'within', false, lookup, inner),
    counts: long.candidates.map(() => null),
    tried: long.candidates.map(() => undefined),
    used: undefined,
  };
}

function resetUsed(level: Level | undefined): void {
  if (level === undefined) return;
  level.used = undefined;
  resetUsed(level.frame);
  resetUsed(level.within);
}

/** Resolves `${…}` in a candidate's text fields; the result's fields stay strings. */
function interpolateCandidate(candidate: Candidate, scope: InterpolationScope): Candidate {
  const result: Candidate = { ...candidate };
  for (const field of [...CANDIDATE_KINDS, 'name'] as const) {
    const value = candidate[field];
    if (value !== undefined) {
      const resolved = interpolate(value, scope);
      result[field] = typeof resolved === 'string' ? resolved : JSON.stringify(resolved);
    }
  }
  return result;
}

/** One attempt at a level and, first, at its frame or `within` levels. */
async function attempt(
  level: Level,
  root: SearchScope,
  allowFallback: boolean,
  options: LocateOptions,
): Promise<Found | undefined> {
  let scope = root;
  if (level.frame !== undefined) {
    const frame = await attempt(level.frame, scope, allowFallback, options);
    if (frame === undefined) return undefined;
    scope = frame.locator.contentFrame();
  }
  if (level.within !== undefined) {
    const within = await attempt(level.within, scope, allowFallback, options);
    if (within === undefined) return undefined;
    scope = within.locator;
  }
  const last = allowFallback ? level.candidates.length : 1;
  for (let index = 0; index < last; index++) {
    const raw = level.candidates[index];
    if (raw === undefined) break;
    const candidate = interpolateCandidate(raw, options.scope);
    let locator = candidateLocator(scope, candidate, options.testIdAttribute);
    if (level.isFrame) locator = locator.and(scope.locator('iframe, frame'));
    // Counts every attached element, hidden ones too, as Playwright's strict mode does.
    let matches: number;
    try {
      matches = await locator.count();
    } catch (error) {
      throw invalidSelector(error, level, candidate) ?? error;
    }
    level.counts[index] = matches;
    level.tried[index] = candidate;
    if (matches === 1) {
      level.used = { index, candidate };
      return { locator };
    }
  }
  return undefined;
}

/** Playwright's words for a selector it cannot parse or run. */
const SELECTOR_ERROR = /while parsing|is not a valid|Unknown engine/;

/**
 * Turns Playwright's rejection of a candidate's selector into an
 * `InvalidSelector` error that names the target and the candidate. Waiting
 * cannot fix a bad selector, so it is not retried. Other errors (a closed
 * page, for example) are not selector errors and are left to the caller.
 */
function invalidSelector(
  error: unknown,
  level: Level,
  candidate: Candidate,
): LocateError | undefined {
  if (!(error instanceof Error) || !SELECTOR_ERROR.test(error.message)) return undefined;
  const reason = (error.message.split('\n')[0] ?? '').replace(/^[\w.]+: /, '');
  const what =
    level.name === undefined ? `the inline ${level.param} target` : `target "${level.name}"`;
  return new LocateError(
    'InvalidSelector',
    `The candidate ${describeCandidate(candidate)} of ${what} is not a valid selector: ${reason}`,
  );
}

function toUse(level: Level): LocatorUse {
  return {
    param: level.param,
    ...(level.name === undefined ? {} : { target: level.name }),
    candidateIndex: level.used?.index ?? null,
    candidate: level.used === undefined ? null : { ...level.used.candidate },
    ...(level.frame === undefined ? {} : { frame: toUse(level.frame) }),
    ...(level.within === undefined ? {} : { within: toUse(level.within) }),
  };
}

function toReport(level: Level): LevelReport {
  return {
    param: level.param,
    ...(level.name === undefined ? {} : { target: level.name }),
    candidates: level.candidates.map((candidate, index) => ({
      candidate: level.tried[index] ?? candidate,
      matches: level.counts[index] ?? null,
    })),
    ...(level.frame === undefined ? {} : { frame: toReport(level.frame) }),
    ...(level.within === undefined ? {} : { within: toReport(level.within) }),
  };
}

function fallbacks(
  level: Level | undefined,
  found: LocatorFallbackWarning[],
): LocatorFallbackWarning[] {
  if (level === undefined) return found;
  fallbacks(level.frame, found);
  fallbacks(level.within, found);
  const index = level.used?.index;
  if (index !== undefined && index > 0) {
    const what =
      level.name === undefined ? `The inline ${level.param} target` : `Target "${level.name}"`;
    found.push({
      code: 'LocatorFallback',
      message: `${what} was found by its candidate ${String(index)} (counting from 0), not its first; the first candidate may be out of date.`,
      data: { ...(level.name === undefined ? {} : { target: level.name }), candidateIndex: index },
    });
  }
  return found;
}

function describeLevel(level: LevelReport, indent: string): string[] {
  const lines: string[] = [];
  if (level.frame !== undefined) {
    lines.push(
      `${indent}in frame ${level.frame.target === undefined ? '(inline)' : `"${level.frame.target}"`}:`,
    );
    lines.push(...describeLevel(level.frame, `${indent}  `));
  }
  if (level.within !== undefined) {
    lines.push(
      `${indent}within ${level.within.target === undefined ? '(inline)' : `"${level.within.target}"`}:`,
    );
    lines.push(...describeLevel(level.within, `${indent}  `));
  }
  level.candidates.forEach(({ candidate, matches }, index) => {
    const count =
      matches === null ? 'not tried' : `${String(matches)} element${matches === 1 ? '' : 's'}`;
    lines.push(`${indent}${String(index)}: ${describeCandidate(candidate)} → ${count}`);
  });
  return lines;
}

function seconds(ms: number): string {
  return ms % 1000 === 0 ? `${String(ms / 1000)}s` : `${String(ms)}ms`;
}

/** Waits `ms`, or less when the signal is aborted. */
function pause(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const done = (): void => {
      clearTimeout(timer);
      signal.removeEventListener('abort', done);
      resolve();
    };
    const timer = setTimeout(done, Math.max(0, ms));
    signal.addEventListener('abort', done, { once: true });
  });
}

/** Resolves when the signal is aborted. */
function aborted(signal: AbortSignal): Promise<'aborted'> {
  return new Promise((resolve) => {
    if (signal.aborted) resolve('aborted');
    else {
      signal.addEventListener(
        'abort',
        () => {
          resolve('aborted');
        },
        { once: true },
      );
    }
  });
}

/**
 * Resolves a target to a Playwright locator (ADR 0010).
 *
 * @param target - A target name, a list of candidates or a long-form target.
 * @param options - The page, lookups, timing, signal and reporter.
 * @returns The locator of the first candidate that matches exactly one
 *   element, in its frame and `within` scope.
 * @throws LocateError `TargetNotFound` when time runs out, listing every
 *   candidate at every level with its last match count; `InvalidSelector` at
 *   once when Playwright rejects a candidate's selector; `UnknownTarget` or
 *   `TargetCycle` when the target's definition is broken.
 * @throws The signal's reason when the signal is aborted.
 * @throws InterpolationError when a `${…}` in a candidate cannot be resolved.
 *
 * @example
 * ```ts
 * const button = await locate('checkout.submit', { page, targets, scope,
 *   testIdAttribute: 'data-testid', fallbackGraceMs: 1000, timeoutMs: 10_000,
 *   signal, reporter });
 * await button.click();
 * ```
 */
export async function locate(target: TargetValue, options: LocateOptions): Promise<Locator> {
  const { signal } = options;
  signal.throwIfAborted();
  const top = buildLevel(target, options.param ?? 'target', false, options.targets, []);
  const start = performance.now();
  const deadline = start + options.timeoutMs;
  const graceEnd = start + options.fallbackGraceMs;
  const abort = aborted(signal);
  // Set once an attempt has tried every candidate; until then the call does not give up.
  let triedAll = false;
  let lastChance = false;
  try {
    for (;;) {
      resetUsed(top);
      const allowFallback = lastChance || performance.now() >= graceEnd;
      triedAll ||= allowFallback;
      const found = await Promise.race([attempt(top, options.page, allowFallback, options), abort]);
      signal.throwIfAborted();
      if (found !== undefined && found !== 'aborted') {
        options.reporter.locatorUsed(toUse(top));
        for (const warning of fallbacks(top, [])) options.reporter.locatorFallback(warning);
        return found.locator;
      }
      const now = performance.now();
      if (now >= deadline && !triedAll) {
        // The timeout is shorter than the grace period: one attempt with every
        // candidate before giving up, so a fallback can still be found.
        lastChance = true;
        continue;
      }
      if (now >= deadline) {
        const report = toReport(top);
        const name = top.name === undefined ? 'The inline target' : `Target "${top.name}"`;
        throw new LocateError(
          'TargetNotFound',
          [
            `${name} was not found within ${seconds(options.timeoutMs)}: no candidate matched exactly one element. Last match counts:`,
            ...describeLevel(report, '  '),
          ].join('\n'),
          report,
        );
      }
      // Wake at the next poll, at the end of the grace period, or at the deadline.
      const wake = Math.min(now + POLL_INTERVAL_MS, deadline, now < graceEnd ? graceEnd : Infinity);
      await pause(wake - now, signal);
    }
  } catch (error) {
    // Levels the last attempt found keep their candidate; the rest are null.
    options.reporter.locatorUsed(toUse(top));
    throw error;
  }
}
