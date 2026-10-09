// Small schemas shared by every step-file schema: durations, names and values
// that may be written as a `${…}` interpolation instead of a literal.

import { z } from 'zod';

/** A duration such as `500ms`, `10s`, `2m` or `12h`. */
export const DurationSchema = z
  .string({
    error: (issue) =>
      issue.input === undefined ? undefined : 'must be a duration such as 500ms, 10s, 2m or 12h',
  })
  .regex(/^\d+(?:ms|s|m|h)$/, { error: 'must be a duration such as 500ms, 10s, 2m or 12h' });

/** A variable name: letters, digits and `_`, not starting with a digit. */
export const VarNameSchema = z.string().regex(/^[A-Za-z_][A-Za-z0-9_]*$/, {
  error: 'must be a variable name: letters, digits and _, not starting with a digit',
});

/** A page name: letters, digits, `-` and `_`, starting with a letter. */
export const PageNameSchema = z.string().regex(/^[A-Za-z][A-Za-z0-9_-]*$/, {
  error: 'must be a page name: letters, digits, - and _, starting with a letter',
});

/** A target name: letters, digits, `.`, `-` and `_`, starting with a letter. */
export const TargetNameSchema = z.string().regex(/^[A-Za-z][A-Za-z0-9_.-]*$/, {
  error: 'must be a target name: letters, digits, ., - and _, starting with a letter',
});

/** A string that is exactly one `${…}` interpolation, such as `${row.qty}`. */
export const InterpolationSchema = z
  .string()
  .regex(/^\$\{[^{}]+\}$/, { error: 'must be a value or one ${…} interpolation' });

/**
 * Accepts a value of `schema`, or a string that is exactly one `${…}`
 * interpolation (which keeps the value's type when the step runs).
 *
 * @param schema - The schema of the literal value, such as `z.number()`.
 * @returns A schema accepting either.
 *
 * @example
 * ```ts
 * const Quantity = interpolatable(z.number().int().min(1));
 * Quantity.parse('${row.qty}'); // ok
 * ```
 */
export function interpolatable<T extends z.ZodType>(
  schema: T,
): z.ZodUnion<[T, typeof InterpolationSchema]> {
  return z.union([schema, InterpolationSchema]);
}

/** A plain JSON-like mapping of string keys to any values. */
export const AnyMappingSchema = z.record(z.string(), z.unknown());

const DURATION_UNITS: Readonly<Record<string, number>> = {
  ms: 1,
  s: 1_000,
  m: 60_000,
  h: 3_600_000,
};

/**
 * Converts a duration such as `500ms`, `10s`, `2m` or `12h` to milliseconds.
 *
 * @param duration - A duration that passed {@link DurationSchema}.
 * @returns Milliseconds.
 * @throws Error when the text is not a duration.
 *
 * @example
 * ```ts
 * durationToMs('10s'); // 10000
 * ```
 */
export function durationToMs(duration: string): number {
  const match = /^(\d+)(ms|s|m|h)$/.exec(duration);
  const unit = match?.[2];
  if (match === null || unit === undefined) {
    throw new Error(`"${duration}" is not a duration such as 500ms, 10s, 2m or 12h.`);
  }
  return Number(match[1]) * (DURATION_UNITS[unit] ?? 1);
}
