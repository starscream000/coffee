// defineAction: an ActionSpec plus run (ADR 0021). Built-in and user actions
// are defined the same way; the engine accepts any object of this shape.

import type { z } from 'zod';
import type { ActionContext } from './context.js';

/**
 * What `defineAction` takes: the action's spec and its `run`.
 *
 * @typeParam P - The parameter schema; `run` receives its parsed output.
 */
export interface ActionDefinition<P extends z.ZodType<Record<string, unknown>>> {
  /** Name used in step files: `<namespace>.<name>` for user actions. */
  readonly name: string;
  /** One sentence shown in clients and in `listActions`. */
  readonly description: string;
  /** The one parameter set by `- action: value`. */
  readonly shorthand?: string;
  /** Schema of the parameters' canonical long form. */
  readonly params: P;
  /** Does the action's work; throw `ActionError` or `AssertionError` to fail the step. */
  run(ctx: ActionContext, params: z.infer<P>): Promise<void>;
}

/** A runnable action as the engine stores it. */
export type RunnableAction = ActionDefinition<z.ZodType<Record<string, unknown>>>;

/**
 * Defines an action.
 *
 * @param definition - The action's name, description, optional shorthand,
 *   parameter schema and `run`.
 * @returns The same definition, typed, for a file's default export.
 *
 * @example
 * ```ts
 * import { defineAction, target, z } from '@cfe/engine/sdk';
 *
 * export default defineAction({
 *   name: 'shop.addToCart',
 *   description: 'Adds a product to the cart.',
 *   shorthand: 'product',
 *   params: z.object({ product: z.string(), button: target().optional() }),
 *   async run(ctx, params) {
 *     ctx.log.info(`Adding ${params.product}`);
 *   },
 * });
 * ```
 */
export function defineAction<P extends z.ZodType<Record<string, unknown>>>(
  definition: ActionDefinition<P>,
): ActionDefinition<P> {
  return definition;
}
