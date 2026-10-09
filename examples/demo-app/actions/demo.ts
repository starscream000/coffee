// A user action of the demo app: adds an item on the to-do page. Used by
// sample S13 (tests/user-action.test.yaml).
import { defineAction, z } from '@cfe/engine/sdk';

export default defineAction({
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
  },
});
