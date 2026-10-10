// The assertions (docs/actions.md, "Assertions"). Each retries until it passes
// or the step's time runs out, then fails with AssertionFailed showing the
// expected and the last actual value.

import type { Locator } from 'playwright';
import { remainingMs } from '../../runner/context.js';
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
