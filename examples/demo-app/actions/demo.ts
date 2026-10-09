// A user action of the demo app: adds an item on the to-do page. Used by
// sample S13 (tests/user-action.test.yaml); it runs once the runner exists.
import { defineAction, z } from '@cfe/engine/sdk';

export default defineAction({
  name: 'demo.addTodo',
  description: "Adds an item on the demo app's to-do page.",
  shorthand: 'title',
  params: z.strictObject({
    title: z.string().min(1),
    done: z.boolean().optional(),
  }),
  run(ctx, params) {
    ctx.log.info(`Adding "${params.title}"${params.done === true ? ' as done' : ''}`);
    return Promise.resolve();
  },
});
