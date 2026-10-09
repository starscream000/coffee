// One registry for built-in and user actions (ADR 0021), enforcing the naming
// rules of ADR 0016 through a single RESERVED_NAMESPACES list, and remembering
// which actions or files failed to load so steps that call them can say so.

import { PRODUCT, type Diagnostic } from '@cfe/protocol';
import type { RunnableAction } from '../sdk/define-action.js';
import type { ActionSpec } from './action-spec.js';
import { paramKeys } from './action-spec.js';

/**
 * Namespaces reserved for built-in actions (ADR 0016). The only list of them:
 * the name check and the built-ins both read it.
 */
export const RESERVED_NAMESPACES: readonly string[] = ['expect', 'wait', 'api', PRODUCT.command];

const NAME = /^[a-z][a-zA-Z0-9]*(?:\.[a-z][a-zA-Z0-9]*)?$/;

/** Where a user action was defined. */
export interface ActionLocation {
  /** Project-relative file, forward slashes. */
  readonly file: string;
  /** 1-based line of the action's name. */
  readonly line: number;
  /** 1-based column of the action's name. */
  readonly column: number;
}

/** An action in the registry. */
export interface RegisteredAction {
  /** What the validator needs. */
  readonly spec: ActionSpec;
  /** The action's code; built-ins get theirs in later plan branches. */
  readonly run: RunnableAction['run'] | undefined;
  /** Where it comes from. */
  readonly source: { readonly kind: 'builtin' } | ({ readonly kind: 'file' } & ActionLocation);
}

/** Why steps that call a name cannot run: the action or its file failed to load. */
export interface NotLoaded {
  /** Actions that were found but rejected, by name, with the reason. */
  readonly rejected: ReadonlyMap<
    string,
    { readonly location: ActionLocation; readonly reason: string }
  >;
  /** Action files that failed to compile or load. */
  readonly failedFiles: readonly string[];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function capitalise(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** A namespace suggestion from the file name: actions/auth.ts → "auth". */
function namespaceFor(file: string): string {
  const base = (file.split('/').at(-1) ?? '').replace(/\.[^.]*$/, '').replace(/[^a-zA-Z0-9]/g, '');
  const candidate = base.charAt(0).toLowerCase() + base.slice(1);
  return /^[a-z][a-zA-Z0-9]*$/.test(candidate) && !RESERVED_NAMESPACES.includes(candidate)
    ? candidate
    : 'shop';
}

/**
 * Holds every known action.
 *
 * @example
 * ```ts
 * const registry = new ActionRegistry(BUILTIN_SPECS);
 * const problem = registry.addUserAction(exported, { file: 'actions/auth.ts', line: 4, column: 9 });
 * ```
 */
export class ActionRegistry implements NotLoaded {
  private readonly actions = new Map<string, RegisteredAction>();
  /** Actions that were found but rejected, by name. */
  readonly rejected = new Map<
    string,
    { readonly location: ActionLocation; readonly reason: string }
  >();
  /** Action files that failed to compile or load. */
  readonly failedFiles: string[] = [];

  /**
   * @param builtins - The built-in specs.
   */
  constructor(builtins: readonly ActionSpec[]) {
    for (const spec of builtins) {
      this.actions.set(spec.name, { spec, run: undefined, source: { kind: 'builtin' } });
    }
  }

  /** Every action's spec by name, for the validator. */
  get specs(): ReadonlyMap<string, ActionSpec> {
    return new Map([...this.actions].map(([name, action]) => [name, action.spec]));
  }

  /**
   * Every action, built-ins first, then user actions in load order.
   *
   * @returns The registered actions.
   */
  list(): readonly RegisteredAction[] {
    return [...this.actions.values()];
  }

  /**
   * Notes a file that failed to compile or load.
   *
   * @param file - Project-relative path.
   */
  addFailedFile(file: string): void {
    this.failedFiles.push(file);
  }

  /**
   * Checks a user action's shape and name and adds it.
   *
   * @param value - One exported value from an action file.
   * @param location - Where its name is written.
   * @returns A diagnostic when the action is rejected, otherwise `undefined`.
   */
  addUserAction(value: unknown, location: ActionLocation): Diagnostic | undefined {
    const problem = (code: string, message: string, hint?: string): Diagnostic => ({
      ...location,
      severity: 'error',
      code,
      message,
      ...(hint === undefined ? {} : { hint }),
    });
    if (
      !isRecord(value) ||
      typeof value.name !== 'string' ||
      typeof value.description !== 'string' ||
      !isRecord(value.params) ||
      typeof value.params.safeParse !== 'function' ||
      typeof value.run !== 'function'
    ) {
      return problem(
        'InvalidActionExport',
        'This file does not export an action: the default export must be defineAction({ … }) or a list of them.',
        'Each action needs name, description, params and run.',
      );
    }
    const action = value as unknown as RunnableAction;
    const reject = (code: string, message: string, hint?: string): Diagnostic => {
      this.rejected.set(action.name, { location, reason: message });
      return problem(code, message, hint);
    };

    const name = action.name;
    const [namespace = '', local] = name.split('.');
    if (local === undefined) {
      const builtin = this.actions.get(name)?.source.kind === 'builtin';
      return reject(
        'ActionNameNotNamespaced',
        `Action "${name}" needs a namespace, for example "${namespaceFor(location.file)}.${name}". Names without a dot are reserved for built-in actions${builtin ? `, and "${name}" is a built-in action` : ''}.`,
      );
    }
    if (!NAME.test(name)) {
      return reject(
        'InvalidActionName',
        `"${name}" is not a valid action name: write <namespace>.<name>, each part starting with a lower-case letter and containing letters and digits.`,
      );
    }
    if (RESERVED_NAMESPACES.includes(namespace)) {
      return reject(
        'ActionNamespaceReserved',
        `Action "${name}" uses the namespace "${namespace}", which is reserved for built-in actions. Use your own namespace, for example "${namespaceFor(location.file)}.${namespace}${capitalise(local)}".`,
      );
    }
    const existing = this.actions.get(name);
    if (existing?.source.kind === 'file') {
      const at = `${existing.source.file}:${String(existing.source.line)}`;
      return reject(
        'ActionNameTaken',
        `Action "${name}" is already defined in ${at}.`,
        'Give one of them another name.',
      );
    }
    const spec: ActionSpec = {
      name,
      description: action.description,
      params: action.params,
      ...(action.shorthand === undefined ? {} : { shorthand: action.shorthand }),
    };
    if (spec.shorthand !== undefined && !paramKeys(spec).includes(spec.shorthand)) {
      return reject(
        'InvalidShorthand',
        `The shorthand of "${name}" is "${spec.shorthand}", which is not one of its parameters.`,
        `Its parameters: ${paramKeys(spec).join(', ') || 'none'}.`,
      );
    }
    const run: RunnableAction['run'] = (ctx, params) => action.run(ctx, params);
    this.actions.set(name, { spec, run, source: { kind: 'file', ...location } });
    return undefined;
  }
}
