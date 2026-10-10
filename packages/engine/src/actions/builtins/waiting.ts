// The waits (docs/actions.md, "Waiting"): wait.element and wait.url.
// wait.response comes with the response log of plan branch 13. Every wait
// polls and checks the step's signal on every round; when time runs out it
// fails with ActionTimeout and says what it saw last.

import { countMatches } from '../../runner/context.js';
import { StepError } from '../../runner/errors.js';
import type { TargetValue } from '../../schema/targets.js';
import { defineAction } from '../../sdk/define-action.js';
import { poll, specOf, targetLabel } from './shared.js';
import { urlMatcher } from './url-pattern.js';

type ElementState = 'visible' | 'hidden' | 'attached' | 'detached';

/**
 * `wait.element`: waits until an element is visible (the default) or
 * attached, or until no element of the target is visible (`hidden`) or
 * attached (`detached`).
 */
export const waitElement = defineAction({
  ...specOf('wait.element'),
  async run(ctx, params) {
    const { target, state = 'visible' } = params as unknown as {
      target: TargetValue;
      state?: ElementState;
    };
    if (state === 'visible' || state === 'attached') {
      // Exactly one attached element (ctx.locate waits for it), then visible.
      const locator = await ctx.locate(target);
      if (state === 'attached') return;
      if (await poll(ctx, () => locator.isVisible())) return;
      throw new StepError(
        'ActionTimeout',
        `The element ${targetLabel(target)} is on the page but did not become visible in time.`,
      );
    }
    // hidden and detached: no candidate may match a visible (or any) element.
    let last: number[] = [];
    const gone = await poll(ctx, async () => {
      const counted = await countMatches(ctx, target, state === 'hidden');
      last = [...(counted?.counts ?? [])];
      return last.every((count) => count === 0);
    });
    if (gone) return;
    throw new StepError(
      'ActionTimeout',
      `The target ${targetLabel(target)} did not become ${state} in time: its candidates still match ${last.join(', ')} ${state === 'hidden' ? 'visible ' : ''}element(s).`,
    );
  },
});

/** `wait.url`: waits until the page's URL matches a pattern (a glob, or `regex:`). */
export const waitUrl = defineAction({
  ...specOf('wait.url'),
  async run(ctx, params) {
    const { url } = params as unknown as { url: string };
    const matches = urlMatcher(url, ctx.env.baseUrl);
    if (await poll(ctx, () => Promise.resolve(matches(ctx.page.url())))) return;
    throw new StepError(
      'ActionTimeout',
      `The page's URL is "${ctx.page.url()}", which did not come to match "${url}" in time.`,
    );
  },
});
