// Loads the declared secrets (docs/step-format.md, "Secrets"; ADR 0014): each
// from the process environment first, then from a `.env` file at the project
// root. Values shorter than 4 characters are rejected. Every loaded value is
// registered for masking. `ctx.secrets.get` reads them.

import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Secrets } from '../sdk/context.js';
import { MIN_SECRET_LENGTH, type SecretRegistry } from './mask.js';

/** Why a declared secret cannot be used. */
export type SecretProblem = 'missing' | 'tooShort';

/**
 * Parses a `.env` file: `NAME=value` lines, optional `export ` prefix, `#`
 * comments, and values in single or double quotes.
 *
 * @param text - The file's contents.
 * @returns The values by name.
 */
export function parseDotEnv(text: string): Map<string, string> {
  const values = new Map<string, string>();
  for (const rawLine of text.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (line === '' || line.startsWith('#')) continue;
    const match = /^(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$/.exec(line);
    if (match === null) continue;
    const [, name = '', rest = ''] = match;
    let value = rest.trim();
    const quote = value.charAt(0);
    if ((quote === '"' || quote === "'") && value.endsWith(quote) && value.length >= 2) {
      value = value.slice(1, -1);
      if (quote === '"') value = value.replace(/\\n/g, '\n').replace(/\\"/g, '"');
    } else {
      value = value.replace(/\s+#.*$/, '');
    }
    values.set(name, value);
  }
  return values;
}

/**
 * The declared secrets of a project, loaded and registered for masking.
 *
 * @example
 * ```ts
 * const secrets = SecretStore.load(['API_TOKEN'], root, process.env, registry);
 * secrets.problem('API_TOKEN'); // undefined, 'missing' or 'tooShort'
 * ```
 */
export class SecretStore implements Secrets {
  private readonly values: ReadonlyMap<string, string>;
  private readonly problems: ReadonlyMap<string, SecretProblem>;
  private readonly declared: ReadonlySet<string>;

  private constructor(
    declared: readonly string[],
    values: ReadonlyMap<string, string>,
    problems: ReadonlyMap<string, SecretProblem>,
  ) {
    this.declared = new Set(declared);
    this.values = values;
    this.problems = problems;
  }

  /**
   * Loads every declared secret and registers its value for masking.
   *
   * @param declared - The names the config declares under `secrets`.
   * @param root - The project root, where a `.env` file may be.
   * @param environment - The process environment (normally `process.env`).
   * @param registry - Where loaded values are registered for masking.
   * @returns The loaded secrets, with a problem noted for each one that is
   *   missing or too short.
   */
  static load(
    declared: readonly string[],
    root: string,
    environment: Readonly<Record<string, string | undefined>>,
    registry: SecretRegistry,
  ): SecretStore {
    const dotEnvPath = join(root, '.env');
    const dotEnv = existsSync(dotEnvPath)
      ? parseDotEnv(readFileSync(dotEnvPath, 'utf8'))
      : new Map<string, string>();
    const values = new Map<string, string>();
    const problems = new Map<string, SecretProblem>();
    for (const name of declared) {
      const value = environment[name] ?? dotEnv.get(name);
      if (value === undefined || value === '') {
        problems.set(name, 'missing');
      } else if (value.length < MIN_SECRET_LENGTH) {
        problems.set(name, 'tooShort');
      } else {
        values.set(name, value);
        registry.register(value);
      }
    }
    return new SecretStore(declared, values, problems);
  }

  /**
   * Why a declared secret cannot be used, if it cannot.
   *
   * @param name - A declared secret's name.
   * @returns `missing`, `tooShort`, or `undefined` when it has a usable value.
   */
  problem(name: string): SecretProblem | undefined {
    return this.problems.get(name);
  }

  /**
   * Reads a secret's value (`ctx.secrets.get`).
   *
   * @param name - A declared secret's name.
   * @returns Its value, which is masked in all output.
   * @throws Error when the name is not declared, or the secret has no usable
   *   value; the message says what to set.
   */
  get(name: string): string {
    if (!this.declared.has(name)) {
      throw new Error(`"${name}" is not declared as a secret in the config. Add it to "secrets:".`);
    }
    const value = this.values.get(name);
    if (value === undefined) {
      throw new Error(secretProblemMessage(name, this.problems.get(name) ?? 'missing'));
    }
    return value;
  }
}

/**
 * The message for a secret that cannot be used.
 *
 * @param name - The secret's name.
 * @param problem - What is wrong with it.
 * @returns A sentence that says which variable to set.
 */
export function secretProblemMessage(name: string, problem: SecretProblem): string {
  return problem === 'missing'
    ? `The secret "${name}" has no value. Set the environment variable ${name}, or add ${name}=… to the .env file at the project root.`
    : `The secret "${name}" is shorter than ${String(MIN_SECRET_LENGTH)} characters, which is too short to mask safely. Use a longer value for ${name}.`;
}
