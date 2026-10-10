// The variable actions (docs/actions.md, "Variables and data"): set and
// extract.

import { remainingMs } from '../../runner/context.js';
import type { TargetValue } from '../../schema/targets.js';
import { defineAction } from '../../sdk/define-action.js';
import { ActionError } from '../../sdk/errors.js';
import { normaliseText, specOf, targetLabel } from './shared.js';

/** `set`: sets one or more variables; its long form is the mapping of variables itself. */
export const set = defineAction({
  ...specOf('set'),
  run(ctx, params) {
    for (const [name, value] of Object.entries(params)) ctx.vars.set(name, value);
    return Promise.resolve();
  },
});

/**
 * `extract`: reads an element's text (whitespace collapsed), value or
 * attribute into a variable; with `pattern`, only the part its one capturing
 * group matches.
 */
export const extract = defineAction({
  ...specOf('extract'),
  async run(ctx, params) {
    const {
      target,
      as,
      from = 'text',
      attribute,
      pattern,
    } = params as unknown as {
      target: TargetValue;
      as: string;
      from?: 'text' | 'value' | 'attribute';
      attribute?: string;
      pattern?: string;
    };
    const locator = await ctx.locate(target);
    const timeout = remainingMs(ctx);
    const raw =
      from === 'value'
        ? await locator.inputValue({ timeout })
        : from === 'attribute'
          ? ((await locator.getAttribute(attribute ?? '', { timeout })) ?? '')
          : normaliseText((await locator.textContent({ timeout })) ?? '');
    let value = raw;
    if (pattern !== undefined) {
      const match = new RegExp(pattern).exec(raw);
      if (match?.[1] === undefined) {
        throw new ActionError(
          `The ${from} of ${targetLabel(target)} is "${raw}", which does not match /${pattern}/.`,
          { hint: 'Check the pattern, or wait for the page to show the value first.' },
        );
      }
      value = match[1];
    }
    ctx.vars.set(as, value);
  },
});
