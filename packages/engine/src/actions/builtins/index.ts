// The built-in actions that can run, defined with `defineAction` like user
// actions (docs/actions.md, "Built-in actions"): goto, click, fill and
// expect.text. Each takes its spec from BUILTIN_SPECS, honours ctx.signal and
// gives every Playwright call at most the step's remaining time. The other
// built-ins are specs only; a step that calls one fails with NotImplemented.

import type { Locator } from 'playwright';
import { remainingMs } from '../../runner/context.js';
import type { TargetValue } from '../../schema/targets.js';
import type { ActionContext } from '../../sdk/context.js';
import { defineAction, type RunnableAction } from '../../sdk/define-action.js';
import { AssertionError } from '../../sdk/errors.js';
import type { ActionSpec } from '../action-spec.js';
import { BUILTIN_SPECS } from '../builtin-specs.js';

/** How often `expect.*` checks again, in milliseconds. */
const RETRY_INTERVAL_MS = 100;

/**
 * Time an `expect.*` action keeps back before the step's deadline, so it can
 * report `AssertionFailed` with the last actual value instead of the runner
 * reporting `ActionTimeout`.
 */
const ASSERTION_MARGIN_MS = 150;

function specOf(name: string): ActionSpec {
  const spec = BUILTIN_SPECS.find((candidate) => candidate.name === name);
  if (spec === undefined) throw new Error(`No built-in spec named "${name}".`);
  return spec;
}

/** Waits `ms`, or less when the signal is aborted. */
function pause(ms: number, signal: AbortSignal): Promise<void> {
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

/** How a target is named in messages: its name, or "the target" when inline. */
function targetLabel(target: TargetValue): string {
  return typeof target === 'string' ? `"${target}"` : 'the target';
}

// The parameters below were checked against each action's spec before `run`
// is called, so reading them with these shapes is safe.

interface GotoParams {
  url: string;
  waitUntil?: 'load' | 'domcontentloaded' | 'commit';
}

interface ClickParams {
  target: TargetValue;
  button?: 'left' | 'right' | 'middle';
  clickCount?: number;
  modifiers?: ('Alt' | 'Control' | 'Meta' | 'Shift')[];
}

interface FillParams {
  target: TargetValue;
  value: string | number | boolean;
}

interface ExpectTextParams {
  target: TargetValue;
  equals?: string | number | boolean;
  contains?: string;
  matches?: string;
  ignoreCase?: boolean;
}

/** `goto`: opens a URL; relative URLs resolve against the environment's base URL. */
const goto = defineAction({
  ...specOf('goto'),
  async run(ctx, params) {
    const { url, waitUntil } = params as unknown as GotoParams;
    await ctx.page.goto(url, { waitUntil: waitUntil ?? 'load', timeout: remainingMs(ctx) });
  },
});

/** `click`: clicks the element a target resolves to. */
const click = defineAction({
  ...specOf('click'),
  async run(ctx, params) {
    const { target, button, clickCount, modifiers } = params as unknown as ClickParams;
    const locator = await ctx.locate(target);
    await locator.click({
      ...(button === undefined ? {} : { button }),
      ...(clickCount === undefined ? {} : { clickCount }),
      ...(modifiers === undefined ? {} : { modifiers }),
      timeout: remainingMs(ctx),
    });
  },
});

/** `fill`: clears a field and types a value. */
const fill = defineAction({
  ...specOf('fill'),
  async run(ctx, params) {
    const { target, value } = params as unknown as FillParams;
    const locator = await ctx.locate(target);
    await locator.fill(String(value), { timeout: remainingMs(ctx) });
  },
});

/** Text as Playwright's text assertions compare it: whitespace collapsed and trimmed. */
function normaliseText(text: string): string {
  return text.replace(/\s+/g, ' ').trim();
}

/** The element's text, waiting at most the time the assertion has left. */
async function textOf(locator: Locator, ctx: ActionContext): Promise<string | undefined> {
  const budget = remainingMs(ctx) - ASSERTION_MARGIN_MS;
  if (budget <= 0) return undefined;
  try {
    return normaliseText((await locator.textContent({ timeout: budget })) ?? '');
  } catch (error) {
    if (error instanceof Error && error.name === 'TimeoutError') return undefined;
    throw error;
  }
}

/** `expect.text`: retries until the element's text matches or the step's time runs out. */
const expectText = defineAction({
  ...specOf('expect.text'),
  async run(ctx, params) {
    const { target, equals, contains, matches, ignoreCase } = params as unknown as ExpectTextParams;
    const fold = (text: string): string => (ignoreCase === true ? text.toLowerCase() : text);
    let expectation: string;
    let expected: string;
    let passes: (actual: string) => boolean;
    if (equals !== undefined) {
      expected = normaliseText(String(equals));
      expectation = `expected "${expected}"`;
      passes = (actual) => fold(actual) === fold(expected);
    } else if (contains !== undefined) {
      expected = contains;
      expectation = `expected it to contain "${contains}"`;
      passes = (actual) => fold(actual).includes(fold(contains));
    } else {
      expected = matches ?? '';
      const pattern = new RegExp(expected, ignoreCase === true ? 'i' : '');
      expectation = `expected it to match /${expected}/`;
      passes = (actual) => pattern.test(actual);
    }

    const locator = await ctx.locate(target);
    let actual: string | undefined;
    for (;;) {
      ctx.signal.throwIfAborted();
      actual = (await textOf(locator, ctx)) ?? actual;
      if (actual !== undefined && passes(actual)) return;
      if (remainingMs(ctx) <= ASSERTION_MARGIN_MS + RETRY_INTERVAL_MS) break;
      await pause(RETRY_INTERVAL_MS, ctx.signal);
    }
    ctx.signal.throwIfAborted();
    throw new AssertionError(
      actual === undefined
        ? `Text of ${targetLabel(target)} could not be read; ${expectation}.`
        : `Text of ${targetLabel(target)} is "${actual}", ${expectation}.`,
      { expected, actual: actual ?? null },
    );
  },
});

/** The built-in actions that have a `run`, by name. */
const IMPLEMENTED: ReadonlyMap<string, RunnableAction> = new Map(
  [goto, click, fill, expectText].map((action) => [action.name, action as RunnableAction]),
);

/**
 * Every built-in action in the order of BUILTIN_SPECS: the runnable ones as
 * actions, the others as specs only.
 *
 * @example
 * ```ts
 * const registry = new ActionRegistry(BUILTIN_ACTIONS);
 * ```
 */
export const BUILTIN_ACTIONS: readonly (ActionSpec | RunnableAction)[] = BUILTIN_SPECS.map(
  (spec) => IMPLEMENTED.get(spec.name) ?? spec,
);
