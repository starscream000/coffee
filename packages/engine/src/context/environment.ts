// Selects an environment profile (docs/step-format.md, "Environments",
// "Viewport, locale and timezone" and "Other defaults"): by name or by the
// config's default, with name, baseUrl and values, and every setting resolved
// from the environment, then the config's defaults, then the built-in defaults.

import type { ConfigFile } from '../schema/files.js';
import type { Environment } from '../sdk/context.js';

/** Settings an environment may override, fully resolved. */
export interface RunSettings {
  /** Default step timeout, such as `10s`. */
  readonly timeout: string;
  /** How long only a target's first candidate is tried. */
  readonly fallbackGrace: string;
  /** When page snapshots are kept. */
  readonly snapshots: 'always' | 'onFailure' | 'off';
  /** Browser viewport. */
  readonly viewport: { readonly width: number; readonly height: number };
  /** Browser locale. */
  readonly locale: string;
  /** Browser timezone. */
  readonly timezone: string;
}

/** A selected environment: what `ctx.env` holds, plus the resolved settings. */
export interface EnvironmentProfile extends Environment {
  /** The settings for runs in this environment. */
  readonly settings: RunSettings;
}

/** The built-in defaults (docs/step-format.md). */
export const BUILT_IN_SETTINGS: RunSettings = {
  timeout: '10s',
  fallbackGrace: '1s',
  snapshots: 'always',
  viewport: { width: 1280, height: 720 },
  locale: 'en-US',
  timezone: 'UTC',
};

/** An environment that does not exist in the config. */
export class UnknownEnvironmentError extends Error {
  /**
   * @param name - The requested name.
   * @param known - The environments the config has.
   */
  constructor(name: string, known: readonly string[]) {
    super(`There is no environment "${name}". The config has: ${known.join(', ') || 'none'}.`);
    this.name = 'UnknownEnvironmentError';
  }
}

/**
 * Selects an environment profile.
 *
 * @param config - The project config.
 * @param name - The requested environment; when absent, the config's
 *   `defaults.environment`, or else its first environment.
 * @returns The profile with every setting resolved.
 * @throws UnknownEnvironmentError when the name is not in the config, or the
 *   config has no environments.
 *
 * @example
 * ```ts
 * const env = selectEnvironment(config, 'staging');
 * env.settings.locale; // "de-DE" when staging overrides it
 * ```
 */
export function selectEnvironment(config: ConfigFile, name?: string): EnvironmentProfile {
  const names = Object.keys(config.environments);
  const selected = name ?? config.defaults?.environment ?? names[0];
  const environment = selected === undefined ? undefined : config.environments[selected];
  if (selected === undefined || environment === undefined) {
    throw new UnknownEnvironmentError(selected ?? '(default)', names);
  }
  const defaults = config.defaults ?? {};
  const pick = <K extends keyof RunSettings>(key: K): RunSettings[K] =>
    (environment[key] ?? defaults[key] ?? BUILT_IN_SETTINGS[key]) as RunSettings[K];
  return {
    name: selected,
    baseUrl: environment.baseUrl,
    values: environment.values ?? {},
    settings: {
      timeout: pick('timeout'),
      fallbackGrace: pick('fallbackGrace'),
      snapshots: pick('snapshots'),
      viewport: pick('viewport'),
      locale: pick('locale'),
      timezone: pick('timezone'),
    },
  };
}
