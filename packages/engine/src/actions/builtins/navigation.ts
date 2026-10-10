// The navigation actions (docs/actions.md, "Navigation"): goto, back and
// reload. Relative URLs resolve against the environment's base URL, which is
// the browser context's base URL.

import { remainingMs } from '../../runner/context.js';
import { defineAction } from '../../sdk/define-action.js';
import { specOf } from './shared.js';

/** `goto`: opens a URL; relative URLs resolve against the environment's base URL. */
export const goto = defineAction({
  ...specOf('goto'),
  async run(ctx, params) {
    const { url, waitUntil } = params as unknown as {
      url: string;
      waitUntil?: 'load' | 'domcontentloaded' | 'commit';
    };
    await ctx.page.goto(url, { waitUntil: waitUntil ?? 'load', timeout: remainingMs(ctx) });
  },
});
