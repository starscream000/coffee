// An open project: its config, its user actions, the files its globs find,
// and the shared targets. Answers `openProject`, `validate` and
// `listActions` (docs/protocol.md).

import { existsSync, globSync, readFileSync, statSync } from 'node:fs';
import { isAbsolute, join, relative, resolve, sep } from 'node:path';
import {
  PRODUCT,
  type Diagnostic,
  type OpenProjectResult,
  type TestSummary,
  type ValidateParams,
} from '@cfe/protocol';
import { checkSdkVersion, loadUserActions } from '../actions/loader.js';
import { ActionRegistry, type BuiltinAction } from '../actions/registry.js';
import { SecretRegistry } from '../context/mask.js';
import { SecretStore } from '../context/secrets.js';
import { DEFAULT_GLOBS, fileKindOf, type ConfigFile, type FlowFile } from '../schema/files.js';
import { TargetSchema, type TargetValue } from '../schema/targets.js';
import { readDataRows } from '../stepfile/data-rows.js';
import type { SourceFile } from '../stepfile/source.js';
import type { NormalizedStep } from '../stepfile/steps.js';
import { validateFile, type FileValidation } from '../stepfile/validate-file.js';
import { RpcError } from '../rpc/rpc-error.js';
import { crossCheck, type CheckContext, type SharedTarget } from './cross-checks.js';

/** Project-relative file lists found by the config's globs. */
export interface ProjectFiles {
  /** Test files. */
  readonly tests: readonly string[];
  /** Flow files. */
  readonly flows: readonly string[];
  /** Shared targets files. */
  readonly targets: readonly string[];
}

/** Options of {@link Project.open}. */
export interface ProjectOptions {
  /** Where loaded secrets are registered for masking; a private registry by default. */
  readonly secrets?: SecretRegistry;
  /** The process environment secrets are read from; `process.env` by default. */
  readonly environment?: Readonly<Record<string, string | undefined>>;
}

/** Turns a native path into forward slashes. */
function posix(path: string): string {
  return path.split(sep).join('/');
}

function isMapping(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function findFiles(root: string, patterns: readonly string[]): string[] {
  const found = new Set<string>();
  for (const pattern of patterns) {
    for (const match of globSync(pattern, { cwd: root })) {
      const file = posix(match);
      if (!file.split('/').some((part) => part === 'node_modules' || part === PRODUCT.dataDir)) {
        found.add(file);
      }
    }
  }
  return [...found].sort();
}

/**
 * Sorts diagnostics by file, line and column and removes exact duplicates
 * (the same problem reached through two paths, such as a flow called twice).
 *
 * @param diagnostics - Diagnostics in any order.
 * @returns A new, sorted list without duplicates.
 */
export function tidyDiagnostics(diagnostics: readonly Diagnostic[]): Diagnostic[] {
  const seen = new Set<string>();
  return diagnostics
    .filter((d) => {
      const key = `${d.file}\u0000${String(d.line)}\u0000${String(d.column)}\u0000${d.code}\u0000${d.message}`;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line || a.column - b.column);
}

/**
 * An open project.
 *
 * @example
 * ```ts
 * const project = Project.open('/work/shop-tests', BUILTIN_SPECS_BY_NAME);
 * const diagnostics = project.validate({ files: ['tests/checkout.test.yaml'] });
 * ```
 */
export class Project {
  /** Absolute root folder. */
  readonly root: string;
  /** Absolute path of the config file. */
  readonly configFile: string;
  /** The config's own validation. */
  readonly configValidation: FileValidation;
  /** The config, when it is valid. */
  readonly config: ConfigFile | undefined;
  /** Files found by the config's globs; refreshed by every `validate`. */
  files: ProjectFiles = { tests: [], flows: [], targets: [] };
  /** Built-in and user actions. */
  readonly registry: ActionRegistry;
  private actionDiagnostics: readonly Diagnostic[] = [];
  private readonly secretRegistry: SecretRegistry;
  private readonly environment: Readonly<Record<string, string | undefined>>;
  /** The declared secrets, re-read with the `.env` file on every `validate`. */
  secrets: SecretStore | undefined;
  private sharedTargets = new Map<string, SharedTarget>();
  private sharedTargetFiles: FileValidation[] = [];
  private readonly flowCache = new Map<string, FileValidation | undefined>();

  private constructor(
    root: string,
    configFile: string,
    text: string,
    builtins: readonly BuiltinAction[],
    options: ProjectOptions,
  ) {
    this.secretRegistry = options.secrets ?? new SecretRegistry();
    this.environment = options.environment ?? process.env;
    this.root = root;
    this.configFile = configFile;
    this.registry = new ActionRegistry(builtins);
    this.configValidation = validateFile(PRODUCT.configFile, text, 'config', this.registry.specs);
    const parsed = this.configValidation.parsed;
    this.config = parsed?.kind === 'config' ? parsed.data : undefined;
    this.refresh();
  }

  /**
   * Finds the project's files again and re-reads the shared targets files, so
   * a client that stays open sees changes made on disk (review 0003,
   * finding 7). Flows are re-read on demand.
   */
  refresh(): void {
    const raw = this.configValidation.source.data;
    const globs = (key: keyof ProjectFiles): readonly string[] => {
      const value = isMapping(raw) ? raw[key] : undefined;
      return Array.isArray(value) && value.every((item) => typeof item === 'string')
        ? value
        : DEFAULT_GLOBS[key];
    };
    this.files = {
      tests: findFiles(this.root, globs('tests')),
      flows: findFiles(this.root, globs('flows')),
      targets: findFiles(this.root, globs('targets')),
    };
    this.flowCache.clear();
    this.secrets =
      this.config === undefined
        ? undefined
        : SecretStore.load(
            this.config.secrets ?? [],
            this.root,
            this.environment,
            this.secretRegistry,
          );
    this.sharedTargets = new Map();
    this.sharedTargetFiles = [];
    for (const file of this.files.targets) {
      const text = readFileIfFile(join(this.root, file));
      const validation = text === undefined ? undefined : this.validateOne(file, text);
      if (validation === undefined) continue;
      this.sharedTargetFiles.push(validation);
      const data = validation.source.data;
      if (!isMapping(data) || !isMapping(data.targets)) continue;
      for (const [name, value] of Object.entries(data.targets)) {
        if (!this.sharedTargets.has(name) && TargetSchema.safeParse(value).success) {
          this.sharedTargets.set(name, {
            name,
            source: validation.source,
            path: ['targets', name],
            value,
          });
        }
      }
    }
  }

  /**
   * Opens the project in `root` and loads its user actions (ADR 0008).
   *
   * @param root - The project's root folder, absolute or relative to the
   *   engine's working directory.
   * @param builtins - The built-in action specs.
   * @param options - The secret registry and the process environment.
   * @returns The open project; problems with the config or the user actions are
   *   in its summary's diagnostics.
   * @throws RpcError `ProjectInvalid` when the folder has no readable config
   *   file.
   */
  static async open(
    root: string,
    builtins: readonly BuiltinAction[],
    options: ProjectOptions = {},
  ): Promise<Project> {
    const absoluteRoot = resolve(root);
    const configFile = join(absoluteRoot, PRODUCT.configFile);
    let text: string;
    try {
      if (!statSync(absoluteRoot).isDirectory()) {
        throw new Error('not a folder');
      }
      text = readFileSync(configFile, 'utf8');
    } catch {
      throw new RpcError(
        'ProjectInvalid',
        existsSync(absoluteRoot)
          ? `There is no readable ${PRODUCT.configFile} in "${posix(absoluteRoot)}". Create one, or open the folder that has it.`
          : `The folder "${posix(absoluteRoot)}" does not exist.`,
      );
    }
    const project = new Project(absoluteRoot, configFile, text, builtins, options);
    await project.loadActions();
    return project;
  }

  private async loadActions(): Promise<void> {
    const raw = this.configValidation.source.data;
    const value = isMapping(raw) ? raw.actions : undefined;
    const patterns =
      Array.isArray(value) && value.every((item) => typeof item === 'string')
        ? value
        : DEFAULT_GLOBS.actions;
    const report = await loadUserActions(this.root, patterns, this.registry);
    const sdk = checkSdkVersion(this.root);
    this.actionDiagnostics = [...report.diagnostics, ...(sdk === undefined ? [] : [sdk])];
  }

  /**
   * The answer to `openProject`.
   *
   * @returns Paths with forward slashes, the environments and logins, and the
   *   problems in the config and in shared targets files.
   */
  summary(): OpenProjectResult {
    const raw = this.configValidation.source.data;
    const environments =
      isMapping(raw) && isMapping(raw.environments) ? Object.keys(raw.environments) : [];
    const logins = isMapping(raw) && isMapping(raw.logins) ? Object.keys(raw.logins) : [];
    const defaults = isMapping(raw) && isMapping(raw.defaults) ? raw.defaults : {};
    const defaultEnvironment =
      typeof defaults.environment === 'string' ? defaults.environment : environments[0];
    return {
      root: posix(this.root),
      configFile: posix(this.configFile),
      environments,
      ...(defaultEnvironment === undefined ? {} : { defaultEnvironment }),
      logins,
      diagnostics: tidyDiagnostics([
        ...this.configValidation.diagnostics,
        ...this.actionDiagnostics,
        ...crossCheck(this.configValidation, this.context()),
        ...this.sharedTargetFiles.flatMap((file) => [
          ...file.diagnostics,
          ...crossCheck(file, this.context()),
        ]),
      ]),
    };
  }

  /**
   * Validates files on disk, or one unsaved buffer (the `validate` request).
   *
   * @param params - `files` (project-relative or absolute paths inside the
   *   project) or `content` (a file name and its text).
   * @returns Every problem in those files and in the flows they call, sorted,
   *   with project-relative paths using forward slashes.
   */
  validate(params: ValidateParams): Diagnostic[] {
    // Files, shared targets and flows may have changed on disk since the last request.
    this.refresh();
    const diagnostics: Diagnostic[] = [];
    // The protocol's params objects accept unknown fields, which defeats
    // narrowing with "in"; the schema guarantees one of the two shapes.
    const content = (params as { content?: { file: string; text: string } }).content;
    const files = (params as { files?: string[] }).files ?? [];
    const inputs: { file: string; text: string | undefined }[] =
      content !== undefined
        ? [{ file: this.toProjectPath(content.file), text: content.text }]
        : files.map((file) => {
            const projectPath = this.toProjectPath(file);
            return {
              file: projectPath,
              text: isOutside(projectPath)
                ? undefined
                : readFileIfFile(join(this.root, projectPath)),
            };
          });
    for (const { file, text } of inputs) {
      if (isOutside(file)) {
        diagnostics.push(
          problem(file, 'FileOutsideProject', `"${file}" is outside the project folder.`),
        );
        continue;
      }
      if (text === undefined) {
        const absolute = join(this.root, file);
        diagnostics.push(
          existsSync(absolute)
            ? problem(
                file,
                'NotAFile',
                `"${file || '.'}" is a folder, not a step file.`,
                'Name the step files to validate.',
              )
            : problem(file, 'FileNotFound', `There is no file "${file}" in the project.`),
        );
        continue;
      }
      const validation = this.validateOne(file, text);
      if (validation === undefined) {
        diagnostics.push(
          problem(
            file,
            'UnknownFileKind',
            `"${file}" is not a step file.`,
            'Step files end in .test.yaml, .flow.yaml or .targets.yaml.',
          ),
        );
        continue;
      }
      diagnostics.push(...validation.diagnostics, ...crossCheck(validation, this.context()));
    }
    return tidyDiagnostics(diagnostics);
  }

  /**
   * Reads and validates one step file, for running it. Call {@link validate}
   * first: it refreshes the project's files and reports every problem.
   *
   * @param file - Project-relative path.
   * @returns The file's own validation, or `undefined` when it cannot be read
   *   or is not a step file.
   */
  readStepFile(file: string): FileValidation | undefined {
    const text = isOutside(file) ? undefined : readFileIfFile(join(this.root, file));
    return text === undefined ? undefined : this.validateOne(file, text);
  }

  /**
   * Reads a flow file for `call`, with its steps and source.
   *
   * @param file - Project-relative path.
   * @returns The flow, or `undefined` when it cannot be read, is not a flow
   *   file or does not pass its own validation.
   */
  readFlow(
    file: string,
  ): { data: FlowFile; steps: readonly NormalizedStep[]; source: SourceFile } | undefined {
    const validation = this.readStepFile(file);
    const parsed = validation?.parsed;
    if (validation === undefined || parsed?.kind !== 'flow') return undefined;
    return { data: parsed.data, steps: parsed.steps, source: validation.source };
  }

  /**
   * The answer to `listTests`: every test file the config's `tests` globs find,
   * with its name as written (`${row.…}` uninterpolated), its tags and how many
   * test instances it makes. A file too broken to read still appears, named
   * after its file, so a client can show it and its diagnostics.
   *
   * @param tags - Only tests with at least one of these tags; all when absent.
   * @returns The tests, in file order.
   */
  listTests(tags?: readonly string[]): TestSummary[] {
    this.refresh();
    const tests: TestSummary[] = [];
    for (const file of this.files.tests) {
      const raw = this.readStepFile(file)?.source.data;
      const data = isMapping(raw) ? raw : {};
      const fileTags = Array.isArray(data.tags)
        ? data.tags.filter((tag): tag is string => typeof tag === 'string')
        : [];
      if (tags !== undefined && !tags.some((tag) => fileTags.includes(tag))) continue;
      const rows = readDataRows(
        typeof data.data === 'string' || Array.isArray(data.data)
          ? (data.data as string | Record<string, unknown>[])
          : undefined,
        file,
        (path) => this.readText(path),
      );
      tests.push({
        file,
        name: typeof data.name === 'string' ? data.name : file,
        tags: fileTags,
        rows: Math.max(1, rows.rows?.length ?? 1),
      });
    }
    return tests;
  }

  /**
   * Registers a value found while a test runs (such as an `Authorization`
   * header) as a secret, so it is masked wherever the engine writes it out
   * (ADR 0014).
   *
   * @param value - The value.
   * @returns False when it is shorter than 4 characters and was not registered.
   */
  registerSecret(value: string): boolean {
    return this.secretRegistry.register(value);
  }

  /**
   * Masks every registered secret in a text, in every encoded form (ADR 0014).
   *
   * @param text - Any text the engine is about to write out.
   * @returns The text with each secret replaced by `•••`.
   */
  mask(text: string): string {
    return this.secretRegistry.mask(text);
  }

  /**
   * Reads a project file's text.
   *
   * @param file - Project-relative path.
   * @returns The text, or `undefined` when the file is missing, a folder or
   *   outside the project.
   */
  readText(file: string): string | undefined {
    return isOutside(file) ? undefined : readFileIfFile(join(this.root, file));
  }

  /**
   * The shared targets by name, as read by the latest {@link validate}.
   *
   * @returns Every shared target whose definition is valid.
   */
  sharedTargetValues(): ReadonlyMap<string, TargetValue> {
    return new Map(
      [...this.sharedTargets].map(([name, target]) => [name, target.value as TargetValue]),
    );
  }

  private validateOne(file: string, text: string): FileValidation | undefined {
    const kind = fileKindOf(file, PRODUCT.configFile);
    return kind === undefined
      ? undefined
      : validateFile(file, text, kind, this.registry.specs, this.registry);
  }

  /**
   * Turns a path the client sent into a project-relative one.
   *
   * @param file - Project-relative, or absolute inside the project.
   * @returns Project-relative, with forward slashes; it starts with `../`
   *   when the file is outside the project.
   */
  toProjectPath(file: string): string {
    const absolute = isAbsolute(file) ? file : join(this.root, file);
    return posix(relative(this.root, absolute));
  }

  private context(): CheckContext {
    return {
      config: this.config,
      sharedTargets: this.sharedTargets,
      actions: this.registry.specs,
      loadFlow: (file) => {
        const projectPath = this.toProjectPath(file);
        if (!this.flowCache.has(projectPath)) {
          const absolute = join(this.root, projectPath);
          const exists =
            !isOutside(projectPath) && existsSync(absolute) && statSync(absolute).isFile();
          this.flowCache.set(
            projectPath,
            exists ? this.validateOne(projectPath, readFileSync(absolute, 'utf8')) : undefined,
          );
        }
        return this.flowCache.get(projectPath);
      },
      knownFlowFiles: () => this.files.flows,
      secretProblem: (name) => this.secrets?.problem(name),
      exists: (file) => existsSync(join(this.root, file)),
      readText: (file) => (isOutside(file) ? undefined : readFileIfFile(join(this.root, file))),
    };
  }
}

/** Whether a project-relative path points outside the project folder. */
function isOutside(projectPath: string): boolean {
  return projectPath === '..' || projectPath.startsWith('../') || isAbsolute(projectPath);
}

/** Reads a file's text, or returns undefined when it is missing or a folder. */
function readFileIfFile(path: string): string | undefined {
  try {
    return statSync(path).isFile() ? readFileSync(path, 'utf8') : undefined;
  } catch {
    return undefined;
  }
}

function problem(file: string, code: string, message: string, hint?: string): Diagnostic {
  return {
    file,
    line: 1,
    column: 1,
    severity: 'error',
    code,
    message,
    ...(hint === undefined ? {} : { hint }),
  };
}
