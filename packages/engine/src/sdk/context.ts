// The type of `ctx`, the context an action's `run` receives (docs/actions.md,
// "The context (ctx)"). Only the type exists in this branch; the runner builds
// the object in a later plan branch.
//
// `page`, `request` and `locate`'s result are Playwright's own `Page`,
// `APIRequestContext` and `Locator`, from the engine's copy of Playwright.

import type { APIRequestContext, Locator, Page } from 'playwright';
import type { TargetValue } from '../schema/targets.js';

export type { APIRequestContext, Locator, Page };

/** A target as an action receives it: unresolved, to be passed to `ctx.locate`. */
export type TargetRef = TargetValue;

/** The variables of the current test or flow. */
export interface Variables {
  /** Reads a variable; `undefined` when it was never set. */
  get(name: string): unknown;
  /** Sets a variable. */
  set(name: string, value: unknown): void;
  /** Whether a variable has been set. */
  has(name: string): boolean;
}

/** The selected environment profile. Never the process environment. */
export interface Environment {
  /** The environment's name, such as `local`. */
  readonly name: string;
  /** Its base URL; relative URLs resolve against it. */
  readonly baseUrl: string;
  /** Its values, read in step files as `${env.<name>}`. */
  readonly values: Readonly<Record<string, string | number | boolean>>;
}

/** The declared secrets. Every value read here is masked in all output. */
export interface Secrets {
  /** Reads a declared secret; throws for a name the config does not declare. */
  get(name: string): string;
}

/** Messages for the tester, sent to clients as `log` events with secrets masked. */
export interface Logger {
  /** Detail for troubleshooting. */
  debug(message: string): void;
  /** Normal progress. */
  info(message: string): void;
  /** Something the tester should look at. */
  warn(message: string): void;
}

/**
 * The context an action's `run` receives.
 *
 * @example
 * ```ts
 * async run(ctx, params) {
 *   const button = await ctx.locate(params.target);
 *   ctx.log.info('found the button');
 * }
 * ```
 */
export interface ActionContext {
  /** The page the step runs on (`main` unless the step sets `page`). */
  readonly page: Page;
  /** HTTP client that shares cookies with `page`'s browser context. */
  readonly request: APIRequestContext;
  /** Variables of the current test or flow. */
  readonly vars: Variables;
  /** The selected environment profile. */
  readonly env: Readonly<Environment>;
  /** The declared secrets. */
  readonly secrets: Secrets;
  /** Messages for the tester. */
  readonly log: Logger;
  /** Resolves a target's candidates to a locator, waiting up to the step timeout. */
  locate(target: TargetRef): Promise<Locator>;
  /** Aborted on step timeout or run cancellation; pass it on and stop when aborted. */
  readonly signal: AbortSignal;
}
