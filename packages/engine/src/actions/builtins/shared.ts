// Helpers shared by the built-in actions: their specs, waiting that stops when
// the step's signal is aborted, how targets are named in messages, and the
// retry rhythm of the `expect.*` and `wait.*` actions.

import type { TargetValue } from '../../schema/targets.js';
import type { ActionSpec } from '../action-spec.js';
import { BUILTIN_SPECS } from '../builtin-specs.js';

/** How often `expect.*` and `wait.*` check again, in milliseconds. */
export const RETRY_INTERVAL_MS = 100;

/**
 * Time an `expect.*` or `wait.*` action keeps back before the step's deadline,
 * so it can report what it last saw (`AssertionFailed`, `WaitTimeout`) instead
 * of the runner reporting `ActionTimeout`.
 */
export const ASSERTION_MARGIN_MS = 150;

/**
 * The spec of a built-in action.
 *
 * @param name - The action's name.
 * @returns Its spec from BUILTIN_SPECS.
 * @throws Error when there is no such built-in.
 */
export function specOf(name: string): ActionSpec {
  const spec = BUILTIN_SPECS.find((candidate) => candidate.name === name);
  if (spec === undefined) throw new Error(`No built-in spec named "${name}".`);
  return spec;
}

/**
 * Waits `ms`, or less when the signal is aborted.
 *
 * @param ms - How long to wait.
 * @param signal - The step's signal.
 * @returns A promise that resolves after the wait or on abort.
 */
export function pause(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const done = (): void => {
      clearTimeout(timer);
      signal.removeEventListener('abort', done);
      resolve();
    };
    const timer = setTimeout(done, ms);
    signal.addEventListener('abort', done, { once: true });
  });
}

/**
 * How a target is named in messages.
 *
 * @param target - A target value.
 * @returns Its name in quotes, or "the target" when it is inline.
 */
export function targetLabel(target: TargetValue): string {
  return typeof target === 'string' ? `"${target}"` : 'the target';
}

/**
 * Text as Playwright's text assertions compare it: whitespace collapsed and
 * trimmed.
 *
 * @param text - Any text.
 * @returns The normalised text.
 */
export function normaliseText(text: string): string {
  return text.replace(/\s+/g, ' ').trim();
}

/** A value of `equals`, `contains` or `matches`, as the assertions accept them. */
export interface Matcher {
  /** Exact value (whitespace normalised for text). */
  readonly equals?: string | number | boolean | undefined;
  /** A part the value must contain. */
  readonly contains?: string | undefined;
  /** A regular expression the value must match. */
  readonly matches?: string | undefined;
  /** Compare without regard to case. */
  readonly ignoreCase?: boolean | undefined;
}

/**
 * Builds the check of one matcher.
 *
 * @param matcher - Exactly one of `equals`, `contains`, `matches`, as the
 *   spec guarantees.
 * @param normalise - Applied to both sides of `equals` (text normalisation).
 * @returns The expected value as shown to the tester, how it reads in a
 *   message, and the test.
 */
export function buildMatcher(
  matcher: Matcher,
  normalise: (text: string) => string = (text) => text,
): { expected: string; expectation: string; passes: (actual: string) => boolean } {
  const fold = (text: string): string => (matcher.ignoreCase === true ? text.toLowerCase() : text);
  if (matcher.equals !== undefined) {
    const expected = normalise(String(matcher.equals));
    return {
      expected,
      expectation: `expected "${expected}"`,
      passes: (actual) => fold(normalise(actual)) === fold(expected),
    };
  }
  if (matcher.contains !== undefined) {
    const expected = matcher.contains;
    return {
      expected,
      expectation: `expected it to contain "${expected}"`,
      passes: (actual) => fold(actual).includes(fold(expected)),
    };
  }
  const expected = matcher.matches ?? '';
  const pattern = new RegExp(expected, matcher.ignoreCase === true ? 'i' : '');
  return {
    expected,
    expectation: `expected it to match /${expected}/`,
    passes: (actual) => pattern.test(actual),
  };
}
