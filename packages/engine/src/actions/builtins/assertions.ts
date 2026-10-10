// The assertions (docs/actions.md, "Assertions"). Each retries until it passes
// or the step's time runs out, then fails with AssertionFailed showing the
// expected and the last actual value.

import type { Locator } from 'playwright';
import { countMatches, remainingMs } from '../../runner/context.js';
import type { TargetValue } from '../../schema/targets.js';
import type { ActionContext } from '../../sdk/context.js';
import { defineAction } from '../../sdk/define-action.js';
import { AssertionError } from '../../sdk/errors.js';
import {
  ASSERTION_MARGIN_MS,
  RETRY_INTERVAL_MS,
  buildMatcher,
  normaliseText,
  pause,
  poll,
  pollBudget,
  specOf,
  targetLabel,
  type Matcher,
} from './shared.js';

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
export const expectText = defineAction({
  ...specOf('expect.text'),
  async run(ctx, params) {
    const { target, ...matcher } = params as unknown as { target: TargetValue } & Matcher;
    const { expected, expectation, passes } = buildMatcher(matcher, normaliseText);
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

/**
 * `expect.visible`: the target's element is visible; with `visible: false`,
 * no candidate matches a visible element (ADR 0010).
 */
export const expectVisible = defineAction({
  ...specOf('expect.visible'),
  async run(ctx, params) {
    const { target, visible = true } = params as unknown as {
      target: TargetValue;
      visible?: boolean;
    };
    if (visible) {
      const locator = await ctx.locate(target);
      if (await poll(ctx, () => locator.isVisible())) return;
      ctx.signal.throwIfAborted();
      throw new AssertionError(`${capitalised(targetLabel(target))} is on the page but hidden.`, {
        expected: 'visible',
        actual: 'hidden',
      });
    }
    let visibleCounts: number[] = [];
    let report: ((index: number | null) => void) | undefined;
    const hidden = await poll(ctx, async () => {
      const counted = await countMatches(ctx, target, true);
      visibleCounts = [...(counted?.counts ?? [])];
      report = counted?.report;
      return visibleCounts.every((count) => count === 0);
    });
    if (hidden) {
      report?.(null);
      return;
    }
    ctx.signal.throwIfAborted();
    const index = visibleCounts.findIndex((count) => count > 0);
    report?.(index);
    throw new AssertionError(
      `${capitalised(targetLabel(target))} is visible: candidate ${String(index)} matches ${String(visibleCounts[index] ?? 0)} visible element(s).`,
      { expected: 'hidden', actual: 'visible' },
    );
  },
});

/** `expect.value`: a field's value equals, contains or matches the expected one. */
export const expectValue = defineAction({
  ...specOf('expect.value'),
  async run(ctx, params) {
    const { target, ...matcher } = params as unknown as { target: TargetValue } & Matcher;
    const { expected, expectation, passes } = buildMatcher(matcher);
    const locator = await ctx.locate(target);
    let actual: string | undefined;
    const passed = await poll(ctx, async () => {
      actual = await locator.inputValue({ timeout: pollBudget(ctx) }).catch(() => actual);
      return actual !== undefined && passes(actual);
    });
    if (passed) return;
    ctx.signal.throwIfAborted();
    throw new AssertionError(
      actual === undefined
        ? `The value of ${targetLabel(target)} could not be read; ${expectation}.`
        : `The value of ${targetLabel(target)} is "${actual}", ${expectation}.`,
      { expected, actual: actual ?? null },
    );
  },
});

/**
 * `expect.url`: the page's URL equals, contains or matches the expected one.
 * An `equals` that starts with `/` is relative to the environment's base URL.
 */
export const expectUrl = defineAction({
  ...specOf('expect.url'),
  async run(ctx, params) {
    const matcher = params as unknown as Matcher;
    const absolute =
      typeof matcher.equals === 'string' && matcher.equals.startsWith('/')
        ? { ...matcher, equals: new URL(matcher.equals, ctx.env.baseUrl).href }
        : matcher;
    const { expected, expectation, passes } = buildMatcher(absolute);
    if (await poll(ctx, () => Promise.resolve(passes(ctx.page.url())))) return;
    ctx.signal.throwIfAborted();
    throw new AssertionError(`The page's URL is "${ctx.page.url()}", ${expectation}.`, {
      expected,
      actual: ctx.page.url(),
    });
  },
});

/**
 * `expect.count`: how many elements the target matches, counted with the
 * first candidate that matches at least one (ADR 0010: the one action where
 * several matches are expected).
 */
export const expectCount = defineAction({
  ...specOf('expect.count'),
  async run(ctx, params) {
    const { target, equals, min, max } = params as unknown as {
      target: TargetValue;
      equals?: number;
      min?: number;
      max?: number;
    };
    const fits = (count: number): boolean =>
      equals !== undefined
        ? count === equals
        : (min === undefined || count >= min) && (max === undefined || count <= max);
    const expectation =
      equals !== undefined
        ? String(equals)
        : [
            ...(min === undefined ? [] : [`at least ${String(min)}`]),
            ...(max === undefined ? [] : [`at most ${String(max)}`]),
          ].join(' and ');
    let actual = 0;
    let index: number | null = null;
    let report: ((index: number | null) => void) | undefined;
    const passed = await poll(ctx, async () => {
      const counted = await countMatches(ctx, target, false);
      const found = counted?.counts.findIndex((count) => count > 0) ?? -1;
      index = found === -1 ? null : found;
      actual = index === null ? 0 : (counted?.counts[index] ?? 0);
      report = counted?.report;
      return fits(actual);
    });
    report?.(index);
    if (passed) return;
    ctx.signal.throwIfAborted();
    throw new AssertionError(
      `${capitalised(targetLabel(target))} matches ${String(actual)} element(s), expected ${expectation}.`,
      { expected: expectation, actual },
    );
  },
});

function capitalised(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}
