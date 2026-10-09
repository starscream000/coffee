// Finds the keys an object schema allows at a given data path, so that an
// unknown key can be answered with "did you mean …?".

import { z } from 'zod';
import type { DataPath } from '../stepfile/source.js';

function unwrap(schema: unknown): unknown {
  let current = schema;
  while (
    current instanceof z.ZodOptional ||
    current instanceof z.ZodNullable ||
    current instanceof z.ZodDefault
  ) {
    current = current.unwrap();
  }
  return current;
}

function step(schema: unknown, segment: string | number): unknown {
  const current = unwrap(schema);
  if (current instanceof z.ZodObject) {
    return typeof segment === 'string'
      ? (current.shape as Record<string, unknown>)[segment]
      : undefined;
  }
  if (current instanceof z.ZodRecord) {
    return current.valueType;
  }
  if (current instanceof z.ZodArray) {
    return typeof segment === 'number' ? current.element : undefined;
  }
  if (current instanceof z.ZodUnion) {
    const options = current.options as readonly unknown[];
    const wanted = typeof segment === 'number' ? z.ZodArray : z.ZodObject;
    const option = options.map(unwrap).find((candidate) => candidate instanceof wanted);
    return option === undefined ? undefined : step(option, segment);
  }
  return undefined;
}

/**
 * Lists the keys allowed in the object found at `path` inside `schema`.
 *
 * @param schema - The schema of the whole value (a file, or an action's
 *   parameters).
 * @param path - Data path of the object, relative to that value.
 * @returns The allowed keys, or `undefined` when no object schema is found
 *   there (for example inside a record of free-form values).
 *
 * @example
 * ```ts
 * keysAt(TestFileSchema, ['pages', 'admin']); // ["login"]
 * ```
 */
export function keysAt(schema: z.ZodType, path: DataPath): readonly string[] | undefined {
  let current: unknown = schema;
  for (const segment of path) {
    current = step(current, segment);
    if (current === undefined) {
      return undefined;
    }
  }
  const target = unwrap(current);
  if (target instanceof z.ZodObject) {
    return Object.keys(target.shape);
  }
  if (target instanceof z.ZodUnion) {
    const object = (target.options as readonly unknown[])
      .map(unwrap)
      .find((option) => option instanceof z.ZodObject);
    return object instanceof z.ZodObject ? Object.keys(object.shape) : undefined;
  }
  return undefined;
}
