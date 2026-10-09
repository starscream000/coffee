// Finds the `${namespace.path}` references in step-file values
// (docs/step-format.md, "Variables and interpolation"). `$${` writes a literal
// `${` and is not a reference.

import type { DataPath } from './source.js';

/** The namespaces a `${…}` reference may use. */
export const NAMESPACES = ['vars', 'env', 'secrets', 'row', 'params'] as const;

/** One `${…}` reference found in a value. */
export interface Reference {
  /** The text between `${` and `}`, such as `row.product`. */
  readonly expression: string;
  /** The first segment, such as `row`. */
  readonly namespace: string;
  /** The segments after the namespace, such as `['product']`. */
  readonly path: readonly string[];
  /** Data path of the string value that contains the reference. */
  readonly at: DataPath;
}

const REFERENCE = /\$(\$?)\{([^{}]*)\}/g;

/**
 * Lists every `${…}` reference in one string.
 *
 * @param text - A string value from a step file.
 * @param at - Data path of that value.
 * @returns The references in order; `$${…}` is skipped.
 *
 * @example
 * ```ts
 * referencesIn('guest+${row.sku}@example.com', ['steps', 2]);
 * // [{ expression: 'row.sku', namespace: 'row', path: ['sku'], at: ['steps', 2] }]
 * ```
 */
export function referencesIn(text: string, at: DataPath): Reference[] {
  const found: Reference[] = [];
  for (const match of text.matchAll(REFERENCE)) {
    if (match[1] === '$') {
      continue; // `$${` is a literal
    }
    const expression = (match[2] ?? '').trim();
    const [namespace = '', ...path] = expression.split('.');
    found.push({ expression, namespace, path, at });
  }
  return found;
}

/**
 * Lists every `${…}` reference in any value, walking into lists and mappings.
 * Mapping keys are not searched.
 *
 * @param value - Any value from a parsed step file.
 * @param at - Data path of `value`.
 * @returns Every reference, in document order.
 */
export function referencesInValue(value: unknown, at: DataPath): Reference[] {
  if (typeof value === 'string') {
    return referencesIn(value, at);
  }
  if (Array.isArray(value)) {
    return value.flatMap((item, index) => referencesInValue(item, [...at, index]));
  }
  if (typeof value === 'object' && value !== null) {
    return Object.entries(value).flatMap(([key, item]) => referencesInValue(item, [...at, key]));
  }
  return [];
}
