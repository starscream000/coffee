// Loads user actions (ADR 0008): finds the files by the config's `actions`
// globs, bundles each with esbuild into the data folder's cache with a source
// map, reuses a bundle while none of its input files changed, and imports it.
// Every import of `@cfe/engine/sdk` resolves to the running engine's own SDK.
// Every problem becomes a diagnostic with file and line; other files still load.

import { createHash } from 'node:crypto';
import { existsSync, globSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { isAbsolute, join, relative, sep } from 'node:path';
import { pathToFileURL } from 'node:url';
import { PRODUCT, type Diagnostic } from '@cfe/protocol';
import { build, type Message, type Plugin } from 'esbuild';
import { getEngineInfo } from '../engine-info.js';
import type { ActionLocation, ActionRegistry } from './registry.js';

const require = createRequire(import.meta.url);

/** File URL of the engine's own SDK, which every user action must use. */
const SDK_URL = pathToFileURL(require.resolve(`${PRODUCT.npmScope}/engine/sdk`)).href;

/** Bumped when the bundling settings change, so old cache entries are not reused. */
const BUNDLE_FORMAT = '1';

/** What happened while loading the user actions. */
export interface LoadReport {
  /** Problems with file and line; files with problems are skipped, others load. */
  readonly diagnostics: readonly Diagnostic[];
  /** Project-relative action files found. */
  readonly files: readonly string[];
  /** Files bundled in this call (the others came from the cache). */
  readonly built: readonly string[];
}

function posix(path: string): string {
  return path.split(sep).join('/');
}

function sha256(text: string | Buffer): string {
  return createHash('sha256').update(text).digest('hex');
}

/** Redirects the SDK to the engine's copy. Playwright is redirected the same way once it is added. */
const sdkRedirect: Plugin = {
  name: 'cfe-sdk-redirect',
  setup(builder) {
    builder.onResolve({ filter: new RegExp(`^${PRODUCT.npmScope}/engine/sdk$`) }, () => ({
      path: SDK_URL,
      external: true,
    }));
    // When Playwright is added: redirect `playwright` to the engine's copy here too (ADR 0008).
  },
};

/** Where an exported action's name is written in its source, for diagnostics. */
function locateName(text: string, name: unknown, file: string): ActionLocation {
  if (typeof name === 'string') {
    for (const quote of ["'", '"', '`']) {
      const index = text.indexOf(`${quote}${name}${quote}`);
      if (index !== -1) {
        const before = text.slice(0, index).split('\n');
        return { file, line: before.length, column: (before.at(-1)?.length ?? 0) + 1 };
      }
    }
  }
  return { file, line: 1, column: 1 };
}

function messageToDiagnostic(message: Message, root: string, fallbackFile: string): Diagnostic {
  const location = message.location;
  const file =
    location === null
      ? fallbackFile
      : posix(isAbsolute(location.file) ? relative(root, location.file) : location.file);
  return {
    file,
    line: location?.line ?? 1,
    column: (location?.column ?? 0) + 1,
    severity: 'error',
    code: 'ActionCompileError',
    message: `This action file does not compile: ${message.text}`,
  };
}

interface CacheEntry {
  readonly bundle: string;
  readonly inputsFile: string;
}

function cacheEntry(cacheDir: string, file: string, text: string): CacheEntry {
  const key = sha256(`${BUNDLE_FORMAT}\0${getEngineInfo().version}\0${file}\0${text}`).slice(0, 32);
  return { bundle: join(cacheDir, `${key}.mjs`), inputsFile: join(cacheDir, `${key}.inputs.json`) };
}

/** A cached bundle is fresh when every file it was built from is unchanged. */
function isFresh(entry: CacheEntry, root: string): boolean {
  if (!existsSync(entry.bundle) || !existsSync(entry.inputsFile)) return false;
  try {
    const inputs = JSON.parse(readFileSync(entry.inputsFile, 'utf8')) as Record<string, string>;
    return Object.entries(inputs).every(([input, hash]) => {
      const path = join(root, input);
      return existsSync(path) && sha256(readFileSync(path)) === hash;
    });
  } catch {
    return false;
  }
}

async function bundle(root: string, file: string, entry: CacheEntry): Promise<Diagnostic[]> {
  try {
    const result = await build({
      entryPoints: [join(root, file)],
      absWorkingDir: root,
      bundle: true,
      platform: 'node',
      format: 'esm',
      target: 'node24',
      outfile: entry.bundle,
      sourcemap: 'linked',
      metafile: true,
      write: true,
      logLevel: 'silent',
      plugins: [sdkRedirect],
    });
    const inputs: Record<string, string> = {};
    for (const input of Object.keys(result.metafile.inputs)) {
      const path = isAbsolute(input) ? input : join(root, input);
      if (existsSync(path)) inputs[posix(relative(root, path))] = sha256(readFileSync(path));
    }
    writeFileSync(entry.inputsFile, JSON.stringify(inputs, null, 2));
    return [];
  } catch (error) {
    const errors: unknown =
      typeof error === 'object' && error !== null && 'errors' in error ? error.errors : undefined;
    if (Array.isArray(errors) && errors.length > 0) {
      return (errors as Message[]).map((message) => messageToDiagnostic(message, root, file));
    }
    return [
      {
        file,
        line: 1,
        column: 1,
        severity: 'error',
        code: 'ActionCompileError',
        message: `This action file could not be bundled: ${error instanceof Error ? error.message : String(error)}`,
      },
    ];
  }
}

/**
 * Loads every user action of a project into the registry.
 *
 * @param root - Absolute project root.
 * @param patterns - The config's `actions` globs.
 * @param registry - Where the actions go; failed files and rejected actions are
 *   recorded there too.
 * @returns The diagnostics, the files found and the files that were bundled.
 *
 * @example
 * ```ts
 * const report = await loadUserActions(root, ['actions/**\/*.ts'], registry);
 * ```
 */
export async function loadUserActions(
  root: string,
  patterns: readonly string[],
  registry: ActionRegistry,
): Promise<LoadReport> {
  const files = [
    ...new Set(
      patterns.flatMap((pattern) =>
        globSync(pattern, { cwd: root })
          .map(posix)
          .filter(
            (file) =>
              !file.split('/').some((part) => part === 'node_modules' || part === PRODUCT.dataDir),
          ),
      ),
    ),
  ].sort();
  const cacheDir = join(root, PRODUCT.dataDir, 'cache', 'actions');
  mkdirSync(cacheDir, { recursive: true });

  const diagnostics: Diagnostic[] = [];
  const built: string[] = [];
  for (const file of files) {
    const text = readFileSync(join(root, file), 'utf8');
    const entry = cacheEntry(cacheDir, file, text);
    if (!isFresh(entry, root)) {
      built.push(file);
      const problems = await bundle(root, file, entry);
      if (problems.length > 0) {
        diagnostics.push(...problems);
        registry.addFailedFile(file);
        continue;
      }
    }
    let module: unknown;
    try {
      module = await import(pathToFileURL(entry.bundle).href);
    } catch (error) {
      diagnostics.push({
        file,
        line: 1,
        column: 1,
        severity: 'error',
        code: 'ActionLoadError',
        message: `This action file failed while loading: ${error instanceof Error ? error.message : String(error)}`,
        hint: 'Code at the top level of an action file runs when the project is opened; move work into run().',
      });
      registry.addFailedFile(file);
      continue;
    }
    const exported: unknown =
      typeof module === 'object' && module !== null && 'default' in module
        ? module.default
        : undefined;
    const items: readonly unknown[] = Array.isArray(exported)
      ? (exported as unknown[])
      : [exported];
    for (const action of items) {
      const name: unknown =
        typeof action === 'object' && action !== null && 'name' in action ? action.name : undefined;
      const problem = registry.addUserAction(action, locateName(text, name, file));
      if (problem !== undefined) diagnostics.push(problem);
    }
  }
  return { diagnostics, files, built };
}

/**
 * Warns when the project's installed SDK differs from the running engine's
 * (ADR 0008): the code that runs is always the engine's copy, but the user's
 * editor and `tsc` read the installed one.
 *
 * @param root - Absolute project root.
 * @returns A `SdkVersionMismatch` warning, or `undefined` when there is no
 *   installed copy or its version matches.
 */
export function checkSdkVersion(root: string): Diagnostic | undefined {
  const manifest = join(
    root,
    'node_modules',
    ...PRODUCT.npmScope.split('/'),
    'engine',
    'package.json',
  );
  if (!existsSync(manifest)) return undefined;
  let installed: unknown;
  try {
    const parsed: unknown = JSON.parse(readFileSync(manifest, 'utf8'));
    installed =
      typeof parsed === 'object' && parsed !== null && 'version' in parsed
        ? parsed.version
        : undefined;
  } catch {
    return undefined;
  }
  const engine = getEngineInfo().version;
  if (typeof installed !== 'string' || installed === engine) return undefined;
  return {
    file: existsSync(join(root, 'package.json')) ? 'package.json' : posix(relative(root, manifest)),
    line: 1,
    column: 1,
    severity: 'warning',
    code: 'SdkVersionMismatch',
    message: `This project has ${PRODUCT.npmScope}/engine ${installed} installed, but the engine is ${engine}; your editor's types may not match what runs.`,
    hint: `Run "pnpm add -D ${PRODUCT.npmScope}/engine@${engine}" (or the npm equivalent).`,
  };
}
