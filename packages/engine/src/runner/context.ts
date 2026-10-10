// Builds `ctx`, the object an action's `run` receives (docs/actions.md, "The
// context (ctx)"): page, request, vars, env, secrets, log, locate and signal,
// for one step. Built-in actions also read the step's deadline through
// `remainingMs`, so every Playwright call gets at most the step's remaining
// time; user actions see only the public members.

import type { APIRequestContext, Locator, Page } from 'playwright';
import type { InterpolationScope } from '../context/interpolate.js';
import type { VariableStore } from '../context/variables.js';
import { locate, type LocatorReporter, type TargetLookup } from '../locate/locate.js';
import type { ActionContext, Environment, Secrets, TargetRef } from '../sdk/context.js';

/** Levels `ctx.log` sends as `log` events. */
export type LogLevel = 'debug' | 'info' | 'warn';

/** Everything `ctx` needs for one step. */
export interface StepContextOptions {
  /** The page the step runs on. */
  readonly page: Page;
  /** HTTP client that shares cookies with the page's browser context. */
  readonly request: APIRequestContext;
  /** Variables of the current test. */
  readonly vars: VariableStore;
  /** The selected environment. */
  readonly env: Environment;
  /** The declared secrets. */
  readonly secrets: Secrets;
  /** Values for `${…}`, for `ctx.locate`. */
  readonly interpolation: InterpolationScope;
  /** Finds named targets: the file's own, then the shared ones. */
  readonly targets: TargetLookup;
  /** The attribute `testId` candidates match. */
  readonly testIdAttribute: string;
  /** How long only first candidates are tried, in milliseconds. */
  readonly fallbackGraceMs: number;
  /** When the step's time is up, on the `performance.now()` clock. */
  readonly deadline: number;
  /** Aborted on timeout or cancellation. */
  readonly signal: AbortSignal;
  /** The step's parameters, to name the parameter a target came from. */
  readonly params: Readonly<Record<string, unknown>>;
  /** Receives each LocatorUse and LocatorFallback warning. */
  readonly reporter: LocatorReporter;
  /** Sends a `log` event for the step. */
  readonly log: (level: LogLevel, message: string) => void;
  /**
   * True once the step has ended while its action kept running: from then on
   * `ctx.vars.set`, `ctx.log` and `ctx.locate` are ignored (docs/actions.md,
   * "A user action that ignores the signal").
   */
  readonly sealed?: () => boolean;
  /** Absolute path of the file the step is written in, for paths relative to it. */
  readonly stepFile?: string | undefined;
}

const deadlines = new WeakMap<ActionContext, number>();
const stepFiles = new WeakMap<ActionContext, string>();

/**
 * The absolute path of the file a step is written in, for built-in actions
 * whose paths are relative to it (`upload`).
 *
 * @param ctx - A context built by {@link createStepContext}.
 * @returns The path, or undefined when it is not known.
 */
export function stepFileOf(ctx: ActionContext): string | undefined {
  return stepFiles.get(ctx);
}

/** Time `ctx.locate` keeps back before the step's deadline, in milliseconds. */
export const LOCATE_MARGIN_MS = 150;

/**
 * The time left before a step's deadline, for Playwright `timeout` options.
 *
 * @param ctx - A context built by {@link createStepContext}.
 * @returns Milliseconds, at least 1 (Playwright reads 0 as "no timeout").
 */
export function remainingMs(ctx: ActionContext): number {
  const deadline = deadlines.get(ctx);
  return deadline === undefined ? 1 : Math.max(1, Math.floor(deadline - performance.now()));
}

/** The name of the step parameter that holds `target`, compared by identity. */
function paramOf(target: TargetRef, params: Readonly<Record<string, unknown>>): string {
  return Object.entries(params).find(([, value]) => value === target)?.[0] ?? 'target';
}

/**
 * Builds `ctx` for one step.
 *
 * @param options - The step's page, values, timing and reporters.
 * @returns The context to pass to the action's `run`.
 */
export function createStepContext(options: StepContextOptions): ActionContext {
  const sealed = options.sealed ?? ((): boolean => false);
  const log = (level: LogLevel, message: string): void => {
    if (!sealed()) options.log(level, message);
  };
  const ctx: ActionContext = {
    page: options.page,
    request: options.request,
    vars: {
      get: (name) => options.vars.get(name),
      has: (name) => options.vars.has(name),
      set: (name, value) => {
        if (!sealed()) options.vars.set(name, value);
      },
    },
    env: options.env,
    secrets: options.secrets,
    signal: options.signal,
    log: {
      debug: (message) => {
        log('debug', message);
      },
      info: (message) => {
        log('info', message);
      },
      warn: (message) => {
        log('warn', message);
      },
    },
    locate: (target: TargetRef): Promise<Locator> =>
      sealed()
        ? Promise.reject(new Error('The step this action belongs to has ended.'))
        : locate(target, {
            page: options.page,
            targets: options.targets,
            scope: options.interpolation,
            testIdAttribute: options.testIdAttribute,
            fallbackGraceMs: options.fallbackGraceMs,
            // Stop a little before the step's deadline, so the step reports
            // TargetNotFound with its candidates rather than ActionTimeout.
            timeoutMs: Math.max(0, options.deadline - performance.now() - LOCATE_MARGIN_MS),
            signal: options.signal,
            reporter: options.reporter,
            param: paramOf(target, options.params),
          }),
  };
  deadlines.set(ctx, options.deadline);
  if (options.stepFile !== undefined) stepFiles.set(ctx, options.stepFile);
  return ctx;
}
