// What can be known about an action without running it: its name, description,
// shorthand and parameter schema (ADR 0021). Built-in and user actions are
// described the same way; `run` is added separately.

import { z } from 'zod';
import { TargetSchema, type TargetValue } from '../schema/targets.js';

/**
 * The description of one action. A runnable action is an `ActionSpec` plus
 * `run(ctx, params)`.
 *
 * @example
 * ```ts
 * const goto: ActionSpec = {
 *   name: 'goto',
 *   description: 'Opens a URL in the page.',
 *   shorthand: 'url',
 *   params: z.strictObject({ url: z.string() }),
 * };
 * ```
 */
export interface ActionSpec {
  /** Name used in step files, such as `goto` or `shop.addToCart`. */
  readonly name: string;
  /** One sentence shown in clients and in `listActions`. */
  readonly description: string;
  /** The one parameter set by `- action: value`, if the action has a shorthand. */
  readonly shorthand?: string;
  /**
   * Schema of the canonical long form of the parameters. Usually a strict
   * object, so unknown keys are reported.
   */
  readonly params: z.ZodType<Record<string, unknown>>;
}

/**
 * Schema helper for a target parameter: accepts a target name, a list of
 * candidates or a long-form target (docs/step-format.md, "Targets").
 *
 * @returns The target schema.
 *
 * @example
 * ```ts
 * const params = z.strictObject({ target: target(), button: z.enum(['left', 'right']).optional() });
 * ```
 */
export function target(): z.ZodType<TargetValue> {
  return TargetSchema;
}

/**
 * Reads the keys of an action's parameter object, for "did you mean" hints.
 *
 * @param spec - The action.
 * @returns The parameter names, or an empty list when the schema is not an
 *   object (for example `set`, whose keys are variable names).
 */
export function paramKeys(spec: ActionSpec): readonly string[] {
  // Refinements in Zod 4 keep the object schema, so `shape` is still there.
  const schema: unknown = spec.params;
  return schema instanceof z.ZodObject ? Object.keys(schema.shape) : [];
}
