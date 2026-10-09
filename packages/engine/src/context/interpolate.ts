// Resolves `${namespace.path}` in step-file values when a step runs
// (docs/step-format.md, "Variables and interpolation"): a value that is exactly
// one `${…}` keeps its type; anything else becomes text; dotted paths reach
// into objects and lists; `$${` writes a literal `${`. The runner decides what
// an unset variable means (in `after` steps the step is skipped); this module
// reports it with InterpolationError and lists the unset variables a value uses.

import type { Environment, Secrets } from '../sdk/context.js';
import { referencesInValue } from '../stepfile/interpolation.js';
import type { VariableStore } from './variables.js';

/** What `${…}` can read while a step runs. */
export interface InterpolationScope {
  /** Variables of the current test or flow. */
  readonly vars: VariableStore;
  /** The selected environment profile. */
  readonly env: Environment;
  /** The declared secrets; reading one registers it for masking. */
  readonly secrets: Secrets;
  /** The current data row, in a test with `data`. */
  readonly row?: Readonly<Record<string, unknown>> | undefined;
  /** The parameters of the current flow, inside a flow. */
  readonly params?: Readonly<Record<string, unknown>> | undefined;
  /** How the current step is named in messages, such as `steps.3 (fill)`. */
  readonly step?: string | undefined;
}

/** Why a `${…}` could not be resolved. */
export type InterpolationErrorCode = 'VariableNotSet' | 'PathNotFound' | 'NamespaceNotAvailable';

/**
 * A `${…}` that cannot be resolved. `variable` is set for an unset variable,
 * so the runner can apply the rule for `after` steps.
 */
export class InterpolationError extends Error {
  /** Why it failed. */
  readonly code: InterpolationErrorCode;
  /** The unset variable, for `VariableNotSet`. */
  readonly variable: string | undefined;

  /**
   * @param code - Why it failed.
   * @param message - For the tester.
   * @param variable - The unset variable's name, for `VariableNotSet`.
   */
  constructor(code: InterpolationErrorCode, message: string, variable?: string) {
    super(message);
    this.name = 'InterpolationError';
    this.code = code;
    this.variable = variable;
  }
}

const REFERENCE = /\$(\$?)\{([^{}]*)\}/g;

function describeStep(scope: InterpolationScope): string {
  return scope.step === undefined ? '' : ` in step ${scope.step}`;
}

function walk(value: unknown, path: readonly string[], expression: string): unknown {
  let current = value;
  for (const segment of path) {
    // Own properties only: `${vars.x.constructor}` must not reach the prototype.
    if (Array.isArray(current)) {
      if (!/^\d+$/.test(segment) || Number(segment) >= current.length) {
        throw new InterpolationError('PathNotFound', `"\${${expression}}" has no "${segment}".`);
      }
      current = current[Number(segment)];
    } else if (typeof current === 'object' && current !== null && Object.hasOwn(current, segment)) {
      current = (current as Record<string, unknown>)[segment];
    } else {
      throw new InterpolationError('PathNotFound', `"\${${expression}}" has no "${segment}".`);
    }
  }
  return current;
}

function resolveExpression(expression: string, scope: InterpolationScope): unknown {
  const [namespace = '', first, ...rest] = expression.trim().split('.');
  if (first === undefined || first === '') {
    throw new InterpolationError(
      'PathNotFound',
      `"\${${expression}}" needs a name after "${namespace}.".`,
    );
  }
  switch (namespace) {
    case 'vars': {
      if (!scope.vars.has(first)) {
        const existing = scope.vars.names();
        throw new InterpolationError(
          'VariableNotSet',
          `The variable "${first}" was never set${describeStep(scope)}. Variables that exist: ${existing.length > 0 ? existing.join(', ') : 'none'}.`,
          first,
        );
      }
      return walk(scope.vars.get(first), rest, expression);
    }
    case 'env': {
      const builtIn: Record<string, unknown> = { name: scope.env.name, baseUrl: scope.env.baseUrl };
      const value = Object.hasOwn(builtIn, first)
        ? builtIn[first]
        : Object.hasOwn(scope.env.values, first)
          ? scope.env.values[first]
          : undefined;
      if (value === undefined) {
        throw new InterpolationError(
          'PathNotFound',
          `The environment "${scope.env.name}" has no value "${first}".`,
        );
      }
      return walk(value, rest, expression);
    }
    case 'secrets':
      return walk(scope.secrets.get(first), rest, expression);
    case 'row':
    case 'params': {
      const source = namespace === 'row' ? scope.row : scope.params;
      if (source === undefined) {
        throw new InterpolationError(
          'NamespaceNotAvailable',
          `"\${${expression}}" uses "${namespace}", which is not available here.`,
        );
      }
      if (!Object.hasOwn(source, first)) {
        throw new InterpolationError(
          'PathNotFound',
          `"\${${expression}}": there is no ${namespace === 'row' ? 'column' : 'parameter'} "${first}".`,
        );
      }
      return walk(source[first], rest, expression);
    }
    default:
      throw new InterpolationError(
        'NamespaceNotAvailable',
        `"\${${expression}}" uses "${namespace}", which is not a namespace.`,
      );
  }
}

function asText(value: unknown): string {
  if (typeof value === 'string') return value;
  if (value === undefined) return '';
  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
    return String(value);
  }
  // null, objects and lists become JSON text.
  // JSON.stringify gives undefined for functions and symbols, despite its type.
  const text = JSON.stringify(value) as string | undefined;
  return text ?? '';
}

function interpolateString(text: string, scope: InterpolationScope): unknown {
  const whole = /^\$\{([^{}]*)\}$/.exec(text);
  if (whole !== null) {
    return resolveExpression(whole[1] ?? '', scope);
  }
  return text.replace(REFERENCE, (_match, literal: string, expression: string) =>
    literal === '$' ? `\${${expression}}` : asText(resolveExpression(expression, scope)),
  );
}

/**
 * Resolves every `${…}` in a value: strings, and strings inside lists and
 * mappings (keys are left alone).
 *
 * @param value - A value from a step, in its canonical long form.
 * @param scope - What the references can read.
 * @returns The resolved value. A string that is exactly one `${…}` becomes the
 *   referenced value with its type; other strings become text.
 * @throws InterpolationError when a reference cannot be resolved; for an
 *   unset variable its `variable` names it and its message lists the variables
 *   that exist. Reading an undeclared secret throws the secret store's error.
 *
 * @example
 * ```ts
 * interpolate({ value: '${vars.n}', url: '/p/${row.sku}' }, scope);
 * // { value: 3, url: '/p/desk-lamp' }
 * ```
 */
export function interpolate(value: unknown, scope: InterpolationScope): unknown {
  if (typeof value === 'string') {
    return interpolateString(value, scope);
  }
  if (Array.isArray(value)) {
    return value.map((item) => interpolate(item, scope));
  }
  if (typeof value === 'object' && value !== null) {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, interpolate(item, scope)]),
    );
  }
  return value;
}

/**
 * Lists the variables a value uses that have never been set, so the runner can
 * skip an `after` step that depends on one ("skipped: orderNumber was never
 * set") instead of failing it.
 *
 * @param value - A value from a step.
 * @param vars - The current variables.
 * @returns The unset variable names, in order of first use, without repeats.
 */
export function unsetVariables(value: unknown, vars: VariableStore): string[] {
  const names = referencesInValue(value, [])
    .filter((reference) => reference.namespace === 'vars' && reference.unclosed !== true)
    .map((reference) => reference.path[0] ?? '')
    .filter((name) => name !== '' && !vars.has(name));
  return [...new Set(names)];
}
