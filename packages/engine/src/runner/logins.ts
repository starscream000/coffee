// Saved logins (ADR 0018; docs/step-format.md, "Saved logins"). A login is a
// flow that signs in; its browser storage state is saved under
// `.cfe/logins/<key>.json`, where the key is an HMAC-SHA-256 of the
// environment, the login's name, the text of its flow and of every flow that
// flow calls, and the resolved `with` values (secrets included). The HMAC key
// is 32 random bytes in `.cfe/logins/.key`. Next to each state,
// `<key>.meta.json` holds its creation time, environment, login name and the
// engine version; no file holds a parameter value. A state older than the
// login's `maxAge` is not used; `refreshLogins` ignores every saved state for
// one run; `freshLogin` signs in for one test without reading or writing the
// cache. At the start of each run, stale states are deleted.

import { createHmac, randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { PRODUCT, type Location } from '@cfe/protocol';
import type { Browser } from 'playwright';
import type { ActionRegistry } from '../actions/registry.js';
import type { EnvironmentProfile } from '../context/environment.js';
import { interpolate } from '../context/interpolate.js';
import { VariableStore } from '../context/variables.js';
import { targetLookup } from '../locate/locate.js';
import type { Project } from '../project/project.js';
import { durationToMs } from '../schema/common.js';
import type { ConfigFile, FlowFile } from '../schema/files.js';
import type { TargetValue } from '../schema/targets.js';
import type { Secrets } from '../sdk/context.js';
import type { NormalizedStep } from '../stepfile/steps.js';
import { LoginFailedError, StepError } from './errors.js';
import { PageSet, type LoginStates, type StorageState } from './pages.js';
import { executeStep } from './step.js';
import type { EmitEvent } from './test-run.js';

/** The default `maxAge` of a saved login. */
export const DEFAULT_LOGIN_MAX_AGE = '12h';

/**
 * Environment variable for tests only: a file name in `.cfe/logins/` that
 * clean-up treats as held open, so it cannot be deleted (check I11).
 */
export const HELD_LOGIN_FILE_VARIABLE = `${PRODUCT.envPrefix}TEST_HELD_LOGIN_FILE`;

/** What `<key>.meta.json` holds. */
interface LoginMeta {
  readonly createdAt: string;
  readonly env: string;
  readonly login: string;
  readonly engineVersion: string;
}

/** The folder of saved logins in a project. */
export function loginsDir(root: string): string {
  return join(root, PRODUCT.dataDir, 'logins');
}

/** JSON with the keys of every object sorted, so equal values give equal text. */
function canonicalJson(value: unknown): string {
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(',')}]`;
  if (typeof value === 'object' && value !== null) {
    const entries = Object.entries(value as Record<string, unknown>).sort(([a], [b]) =>
      a < b ? -1 : a > b ? 1 : 0,
    );
    return `{${entries.map(([key, item]) => `${JSON.stringify(key)}:${canonicalJson(item)}`).join(',')}}`;
  }
  // Values here come from YAML and JSON; undefined is written as JSON writes it in a list.
  return value === undefined ? 'null' : JSON.stringify(value);
}

function readMeta(path: string): LoginMeta | undefined {
  try {
    const meta = JSON.parse(readFileSync(path, 'utf8')) as Partial<LoginMeta>;
    return typeof meta.createdAt === 'string' &&
      typeof meta.env === 'string' &&
      typeof meta.login === 'string'
      ? (meta as LoginMeta)
      : undefined;
  } catch {
    return undefined;
  }
}

/** What {@link pruneLogins} did. */
export interface LoginPruneResult {
  /** Files deleted. */
  readonly removed: readonly string[];
  /** Files that could not be deleted, with the reason. */
  readonly failed: readonly { readonly file: string; readonly reason: string }[];
}

/**
 * Deletes saved logins that can no longer be used, at the start of a run: a
 * state older than its login's current `maxAge`, a state whose metadata names
 * an environment or a login the config no longer has, and a state without
 * readable metadata. The `.key` file is never deleted. A file that cannot be
 * deleted is reported and tried again at the next run.
 *
 * @param dir - The `.cfe/logins` folder; it may not exist.
 * @param config - The project config.
 * @param now - The current time in milliseconds.
 * @param remove - Deletes one file; replaceable for tests.
 * @returns The files deleted and those that could not be.
 */
export function pruneLogins(
  dir: string,
  config: ConfigFile,
  now: number,
  remove: (path: string) => void = defaultRemove,
): LoginPruneResult {
  let names: string[];
  try {
    names = readdirSync(dir);
  } catch {
    return { removed: [], failed: [] };
  }
  const removed: string[] = [];
  const failed: { file: string; reason: string }[] = [];
  const drop = (file: string): void => {
    if (!names.includes(file)) return;
    try {
      remove(join(dir, file));
      removed.push(file);
    } catch (error) {
      failed.push({ file, reason: error instanceof Error ? error.message : String(error) });
    }
  };
  const logins = config.logins ?? {};
  for (const name of names) {
    if (name === '.key' || name.endsWith('.meta.json') || !name.endsWith('.json')) continue;
    const key = name.slice(0, -'.json'.length);
    const meta = readMeta(join(dir, `${key}.meta.json`));
    const login = meta === undefined ? undefined : logins[meta.login];
    const stale =
      meta === undefined ||
      login === undefined ||
      !Object.hasOwn(config.environments, meta.env) ||
      Date.parse(meta.createdAt) + durationToMs(login.maxAge ?? DEFAULT_LOGIN_MAX_AGE) <= now;
    if (stale) {
      drop(name);
      drop(`${key}.meta.json`);
    }
  }
  // Metadata whose state is gone.
  for (const name of names) {
    if (name.endsWith('.meta.json') && !names.includes(name.replace(/\.meta\.json$/, '.json'))) {
      drop(name);
    }
  }
  return { removed, failed };
}

function defaultRemove(path: string): void {
  const held = process.env[HELD_LOGIN_FILE_VARIABLE];
  if (held !== undefined && held !== '' && path.endsWith(held)) {
    throw new Error('EBUSY: resource busy or locked (held open by a test)');
  }
  rmSync(path, { force: true });
}

/** What a {@link LoginStore} needs. */
export interface LoginStoreOptions {
  /** The open project, to read login flows. */
  readonly project: Project;
  /** The project config. */
  readonly config: ConfigFile;
  /** The selected environment. */
  readonly profile: EnvironmentProfile;
  /** The declared secrets. */
  readonly secrets: Secrets;
  /** Every action. */
  readonly registry: ActionRegistry;
  /** The project's shared targets. */
  readonly sharedTargets: ReadonlyMap<string, TargetValue>;
  /** The attribute `testId` candidates match. */
  readonly testIdAttribute: string;
  /** The run's browser. */
  readonly browser: Browser;
  /** `startRun`'s `refreshLogins`: ignore every saved state for this run. */
  readonly refresh: boolean;
  /** The engine's version, for the metadata. */
  readonly engineVersion: string;
}

/**
 * The saved logins of one run.
 *
 * @example
 * ```ts
 * const store = new LoginStore({ project, config, profile, secrets, … });
 * const states = store.forTest(testId, false, emit);
 * const state = await states('customer', 'steps.0');
 * ```
 */
export class LoginStore {
  private readonly options: LoginStoreOptions;
  private readonly dir: string;
  /** States produced or read in this run, by login. */
  private readonly states = new Map<string, Promise<StorageState>>();

  /**
   * @param options - The project, environment, browser and run options.
   */
  constructor(options: LoginStoreOptions) {
    this.options = options;
    this.dir = loginsDir(options.project.root);
  }

  /**
   * The login states for one test instance.
   *
   * @param testId - The test instance.
   * @param fresh - The test's `freshLogin`: sign in for this test only.
   * @param emit - Sends the test's events.
   * @returns A function that gives a login's storage state.
   */
  forTest(testId: string, fresh: boolean, emit: EmitEvent): LoginStates {
    return (login, stepId) => {
      const log = (message: string): void => {
        emit('log', { level: 'info', message, testId, stepId });
      };
      if (fresh) {
        return this.signIn(login, testId, stepId, emit).then((state) => {
          log(`Signed in with the login "${login}" for this test only (freshLogin).`);
          return state;
        });
      }
      let state = this.states.get(login);
      if (state === undefined) {
        state = this.cachedOrSignIn(login, testId, stepId, emit, log);
        this.states.set(login, state);
        // A failed sign-in is not remembered: the next test tries again.
        state.catch(() => {
          this.states.delete(login);
        });
      }
      return state;
    };
  }

  private async cachedOrSignIn(
    login: string,
    testId: string,
    stepId: string,
    emit: EmitEvent,
    log: (message: string) => void,
  ): Promise<StorageState> {
    const definition = this.definition(login);
    const key = this.cacheKey(login, definition);
    const statePath = join(this.dir, `${key}.json`);
    const metaPath = join(this.dir, `${key}.meta.json`);
    const maxAgeText = definition.maxAge ?? DEFAULT_LOGIN_MAX_AGE;
    const meta = readMeta(metaPath);
    if (
      !this.options.refresh &&
      meta !== undefined &&
      existsSync(statePath) &&
      Date.parse(meta.createdAt) + durationToMs(maxAgeText) > Date.now()
    ) {
      log(`Reused the saved login "${login}" from ${meta.createdAt}.`);
      return JSON.parse(readFileSync(statePath, 'utf8')) as StorageState;
    }
    const state = await this.signIn(login, testId, stepId, emit);
    mkdirSync(this.dir, { recursive: true });
    writeFileSync(statePath, JSON.stringify(state));
    const written: LoginMeta = {
      createdAt: new Date().toISOString(),
      env: this.options.profile.name,
      login,
      engineVersion: this.options.engineVersion,
    };
    writeFileSync(metaPath, `${JSON.stringify(written, null, 2)}\n`);
    log(`Signed in with the login "${login}" and saved it for ${maxAgeText}.`);
    return state;
  }

  private definition(login: string): NonNullable<ConfigFile['logins']>[string] {
    const definition = this.options.config.logins?.[login];
    if (definition === undefined) {
      throw new StepError(
        'UnknownLogin',
        `There is no saved login named "${login}" in the config.`,
      );
    }
    return definition;
  }

  /** The login's `with` values, filled in; secrets are read and so registered for masking. */
  private resolvedWith(login: string): Record<string, unknown> {
    const definition = this.definition(login);
    return interpolate(definition.with ?? {}, {
      vars: new VariableStore(),
      env: this.options.profile,
      secrets: this.options.secrets,
      step: `login "${login}"`,
    }) as Record<string, unknown>;
  }

  /** The HMAC key, created on first use. */
  private hmacKey(): string {
    const path = join(this.dir, '.key');
    if (!existsSync(path)) {
      mkdirSync(this.dir, { recursive: true });
      writeFileSync(path, randomBytes(32).toString('hex'), { flag: 'wx' });
    }
    return readFileSync(path, 'utf8').trim();
  }

  /** The text of a login flow and of every flow it calls, by file. */
  private flowTexts(file: string, found: Map<string, string>): Map<string, string> {
    if (found.has(file)) return found;
    found.set(file, this.options.project.readText(file) ?? '');
    const parsed = this.options.project.readStepFile(file)?.parsed;
    if (parsed?.kind === 'flow') {
      for (const step of parsed.steps) {
        const called = step.action === 'call' ? step.params.flow : undefined;
        if (typeof called === 'string') this.flowTexts(called, found);
      }
    }
    return found;
  }

  private cacheKey(login: string, definition: NonNullable<ConfigFile['logins']>[string]): string {
    const flows = [...this.flowTexts(definition.flow, new Map())].map(([file, text]) => ({
      file,
      text,
    }));
    const material = canonicalJson({
      env: this.options.profile.name,
      login,
      flows,
      with: this.resolvedWith(login),
    });
    return createHmac('sha256', this.hmacKey()).update(material).digest('hex');
  }

  /** Runs the login flow in a context of its own and returns the state it leaves. */
  private async signIn(
    login: string,
    testId: string,
    stepId: string,
    emit: EmitEvent,
  ): Promise<StorageState> {
    const definition = this.definition(login);
    const validation = this.options.project.readStepFile(definition.flow);
    const parsed = validation?.parsed;
    if (validation === undefined || parsed?.kind !== 'flow') {
      throw new StepError(
        'LoginFlowInvalid',
        `The flow of the saved login "${login}" (${definition.flow}) cannot be read; validate it.`,
      );
    }
    const flow: FlowFile = parsed.data;
    const params = { ...defaultsOf(flow), ...this.resolvedWith(login) };
    // The flow's own events stay inside the engine; its logs and warnings are
    // sent for the test step that needed the login.
    const forward: EmitEvent = (method, eventParams) => {
      if (method === 'log') emit('log', { ...eventParams, testId, stepId });
    };
    const pages = new PageSet({
      browser: this.options.browser,
      profile: this.options.profile,
      pageLogins: new Map(),
      logins: () =>
        Promise.reject(new StepError('NestedLogin', 'A login flow cannot use a saved login.')),
      testId,
      emit: forward,
    });
    try {
      const vars = new VariableStore();
      const targets = targetLookup(flow.targets, this.options.sharedTargets);
      for (const [index, step] of parsed.steps.entries()) {
        const result = await executeStep(step, {
          registry: this.options.registry,
          profile: this.options.profile,
          secrets: this.options.secrets,
          testIdAttribute: this.options.testIdAttribute,
          vars,
          targets,
          flowParams: params,
          pages,
          section: 'steps',
          testId,
          stepId: `${stepId}/login.${String(index)}`,
          location: locationOf(validation.source, definition.flow, step),
          root: this.options.project.root,
          registerSecret: (value) => this.options.project.registerSecret(value),
          timeoutMs: durationToMs(step.timeout ?? this.options.profile.settings.timeout),
          emit: forward,
        });
        if (result.error !== undefined) throw new LoginFailedError(login, result.error);
      }
      const state = await pages.storageState();
      if (state === undefined) {
        throw new StepError(
          'LoginFlowInvalid',
          `The flow of the saved login "${login}" opened no page, so there is nothing to save.`,
        );
      }
      return state;
    } finally {
      await pages.close();
    }
  }
}

/** The default values of a flow's parameters. */
function defaultsOf(flow: FlowFile): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(flow.params ?? {}).flatMap(([name, param]) =>
      param.default === undefined ? [] : [[name, param.default]],
    ),
  );
}

function locationOf(
  source: { positionOf: (path: readonly (string | number)[]) => { line: number; column: number } },
  file: string,
  step: NormalizedStep,
): Location {
  const at = source.positionOf(step.path);
  return { file, line: at.line, column: at.column };
}
