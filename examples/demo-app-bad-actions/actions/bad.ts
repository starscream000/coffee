// F9 / A7: three actions that break the naming rules of ADR 0016. Opening this
// project must report each one at its name, and load none of them.
import { defineAction, z } from '@cfe/engine/sdk';

const run = (): Promise<void> => Promise.resolve();

export default [
  defineAction({
    name: 'fillOtp',
    description: 'Fills in a one-time password (no namespace: rejected).',
    params: z.strictObject({ code: z.string() }),
    run,
  }),
  defineAction({
    name: 'expect.priceFormat',
    description: 'Checks a price format (reserved namespace: rejected).',
    params: z.strictObject({ target: z.string() }),
    run,
  }),
  defineAction({
    name: 'click',
    description: 'Clicks (a built-in name: rejected).',
    params: z.strictObject({ target: z.string() }),
    run,
  }),
];
