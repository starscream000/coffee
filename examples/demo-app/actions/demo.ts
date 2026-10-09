// User actions of the demo app: demo.addTodo adds an item on the to-do page
// (sample S13, tests/user-action.test.yaml); demo.ignoreSignal keeps running
// after its step has ended (fixture F8, fixtures/failing/stray-action.test.yaml).
import { defineAction, z } from '@cfe/engine/sdk';

const addTodo = defineAction({
  name: 'demo.addTodo',
  description: "Adds an item on the demo app's to-do page.",
  shorthand: 'title',
  params: z.strictObject({
    title: z.string().min(1),
    done: z.boolean().optional(),
  }),
  async run(ctx, params) {
    ctx.log.info(`Adding "${params.title}"${params.done === true ? ' as done' : ''}`);
    await (await ctx.locate('todos.new')).fill(params.title);
    if (params.done === true) {
      await ctx.page.getByLabel('Done').check();
    }
    await (await ctx.locate('todos.add')).click();
    // Return only once the item is on the page. The page clears the field when
    // the server has answered; returning earlier would let that answer wipe
    // what the next step types (review 0006, finding 2).
    const text = `${params.title}${params.done === true ? ' (done)' : ''}`;
    await ctx.page
      .getByTestId('todo-item')
      .filter({ hasText: new RegExp(`^${escapeRegExp(text)}$`) })
      .last()
      .waitFor();
  },
});

/**
 * Waits 5 seconds without looking at ctx.signal, then logs: a user action that
 * does not stop when its step ends. Do not write actions like this one.
 */
const ignoreSignal = defineAction({
  name: 'demo.ignoreSignal',
  description: 'Waits 5 seconds and ignores ctx.signal (for fixture F8).',
  params: z.strictObject({}),
  async run(ctx) {
    await new Promise((resolve) => setTimeout(resolve, 5_000));
    ctx.log.info('This line never reaches the client: the step has ended.');
  },
});

export default [addTodo, ignoreSignal];

/** Escapes text for use inside a regular expression. */
function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
