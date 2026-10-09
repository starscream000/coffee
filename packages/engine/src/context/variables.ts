// The variable store of one test or flow (docs/step-format.md, "Variables and
// interpolation"): `vars:`, `set`, `extract`, `api` and flow outputs write to
// it; `${vars.…}` and `ctx.vars` read from it.

import type { Variables } from '../sdk/context.js';

/**
 * Variables of one test instance or one flow call. A flow gets its own store;
 * only its declared outputs are copied back to the caller.
 *
 * @example
 * ```ts
 * const vars = new VariableStore({ email: 'a@b.c' });
 * vars.set('orderNumber', 'A-1');
 * vars.names(); // ['email', 'orderNumber']
 * ```
 */
export class VariableStore implements Variables {
  private readonly values = new Map<string, unknown>();

  /**
   * @param initial - Initial variables, such as a test's `vars:`.
   */
  constructor(initial: Readonly<Record<string, unknown>> = {}) {
    for (const [name, value] of Object.entries(initial)) {
      this.values.set(name, value);
    }
  }

  /**
   * Reads a variable.
   *
   * @param name - The variable's name.
   * @returns Its value, or `undefined` when it was never set.
   */
  get(name: string): unknown {
    return this.values.get(name);
  }

  /**
   * Sets a variable.
   *
   * @param name - The variable's name.
   * @param value - Any value.
   */
  set(name: string, value: unknown): void {
    this.values.set(name, value);
  }

  /**
   * Tells whether a variable has been set.
   *
   * @param name - The variable's name.
   * @returns True once it has been set, even to `undefined` or `null`.
   */
  has(name: string): boolean {
    return this.values.has(name);
  }

  /**
   * Lists the variables that exist, for error messages.
   *
   * @returns Their names, sorted.
   */
  names(): string[] {
    return [...this.values.keys()].sort();
  }
}
