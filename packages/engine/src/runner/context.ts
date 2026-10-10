// Builds `ctx`, the object an action's `run` receives (docs/actions.md, "The
// context (ctx)"): page, request, vars, env, secrets, log, locate and signal,
// for one step. Built-in actions also read the step's deadline through
// `remainingMs`, so every Playwright call gets at most the step's remaining
// time; user actions see only the public members.

import type { APIRequestContext, Locator, Page } from 'playwright';
import type { InterpolationScope } from '../context/interpolate.js';
import type { VariableStore } from '../context/variables.js';
import {
  countCandidates,
  locate,
  type CandidateCounts,
  type LocatorReporter,
  type TargetLookup,
} from '../locate/locate.js';
import type { ResponseLog } from './responses.js';
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
  /** Registers a value found during the step as a secret. */
  readonly registerSecret?: ((value: string) => boolean) | undefined;
  /** The test's response log. */
  readonly responses?: ResponseLog | undefined;
  /** When the previous step started, on the `performance.now()` clock. */
  readonly responsesSince?: number | undefined;
}

const deadlines = new WeakMap<ActionContext, number>();

/** Counts a target's matches for one step; see {@link countMatches}. */
type Counter = (
  target: TargetRef,
  visibleOnly: boolean,
) => Promise<(CandidateCounts & { report: (index: number | null) => void }) | undefined>;
const counters = new WeakMap<ActionContext, Counter>();

/**
 * Counts the matches of every candidate of a target, without the "exactly one
 * element" rule, for the built-ins ADR 0010 names as exceptions. The result's
 * `report` records which candidate the action used (`null` for none) in the
 * step's `locators`.
 *
 * @param ctx - A context built by {@link createStepContext}.
 * @param target - The target.
 * @param visibleOnly - Count only visible elements.
 * @returns The counts, or undefined when the target's frame or `within` element
 *   is not on the page.
 * @throws Error when the context was not built by the runner.
 */
export function countMatches(
  ctx: ActionContext,
  target: TargetRef,
  visibleOnly: boolean,
): ReturnType<Counter> {
  const counter = counters.get(ctx);
  if (counter === undefined) throw new Error('This context cannot count matches.');
  return counter(target, visibleOnly);
}
const stepFiles = new WeakMap<ActionContext, string>();

/** The engine internals the built-ins `wait.response`, `expect.response` and `api` use. */
export interface StepInternals {
  /** Registers a value as a secret; false when it is too short. */
  readonly registerSecret: (value: string) => boolean;
  /** The test's response log, if the step has one. */
  readonly responses: ResponseLog | undefined;
  /** When the previous step started: responses from then on count. */
  readonly responsesSince: number;
}
const internals = new WeakMap<ActionContext, StepInternals>();

/**
 * The engine internals of a step, for the built-ins docs/actions.md calls
 * internal extensions ("Internal extensions").
 *
 * @param ctx - A context built by {@link createStepContext}.
 * @returns Its internals; a context built elsewhere gets inert ones.
 */
export function internalsOf(ctx: ActionContext): StepInternals {
  return (
    internals.get(ctx) ?? {
      registerSecret: () => false,
      responses: undefined,
      responsesSince: 0,
    }
  );
}

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
  counters.set(ctx, async (target, visibleOnly) => {
    const param = paramOf(target, options.params);
    const counted = await countCandidates(
      target,
      {
        page: options.page,
        targets: options.targets,
        scope: options.interpolation,
        testIdAttribute: options.testIdAttribute,
        fallbackGraceMs: 0,
        timeoutMs: 0,
        signal: options.signal,
        reporter: options.reporter,
        param,
      },
      visibleOnly,
    );
    if (counted === undefined) return undefined;
    return {
      ...counted,
      report: (index) => {
        const candidate = index === null ? undefined : counted.candidates[index];
        options.reporter.locatorUsed({
          param,
          ...(typeof target === 'string' ? { target } : {}),
          candidateIndex: candidate === undefined ? null : index,
          candidate: candidate === undefined ? null : { ...candidate },
        });
      },
    };
  });
  deadlines.set(ctx, options.deadline);
  if (options.stepFile !== undefined) stepFiles.set(ctx, options.stepFile);
  internals.set(ctx, {
    registerSecret: options.registerSecret ?? (() => false),
    responses: options.responses,
    responsesSince: options.responsesSince ?? 0,
  });
  return ctx;
}
