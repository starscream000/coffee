// An open project: its config, the files its globs find, and the shared
// targets. Answers `openProject` and `validate` (docs/protocol.md). User
// actions are not loaded yet: plan branch 4 adds that to `Project.open`.

import { existsSync, globSync, readFileSync, statSync } from 'node:fs';
import { isAbsolute, join, relative, resolve, sep } from 'node:path';
import {
  PRODUCT,
  type Diagnostic,
  type OpenProjectResult,
  type ValidateParams,
} from '@cfe/protocol';
import type { ActionSpec } from '../actions/action-spec.js';
import { DEFAULT_GLOBS, fileKindOf, type ConfigFile } from '../schema/files.js';
import { TargetSchema } from '../schema/targets.js';
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
  /** Files found by the config's globs. */
  readonly files: ProjectFiles;
  private readonly actions: ReadonlyMap<string, ActionSpec>;
  private readonly sharedTargets = new Map<string, SharedTarget>();
  private readonly sharedTargetFiles: FileValidation[] = [];
  private readonly flowCache = new Map<string, FileValidation | undefined>();

  private constructor(
    root: string,
    configFile: string,
    text: string,
    actions: ReadonlyMap<string, ActionSpec>,
  ) {
    this.root = root;
    this.configFile = configFile;
    this.actions = actions;
    this.configValidation = validateFile(PRODUCT.configFile, text, 'config', actions);
    const parsed = this.configValidation.parsed;
    this.config = parsed?.kind === 'config' ? parsed.data : undefined;
    const raw = this.configValidation.source.data;
    const globs = (key: keyof ProjectFiles): readonly string[] => {
      const value = isMapping(raw) ? raw[key] : undefined;
      return Array.isArray(value) && value.every((item) => typeof item === 'string')
        ? value
        : DEFAULT_GLOBS[key];
    };
    this.files = {
      tests: findFiles(root, globs('tests')),
      flows: findFiles(root, globs('flows')),
      targets: findFiles(root, globs('targets')),
    };
    for (const file of this.files.targets) {
      const validation = this.validateOne(file, readFileSync(join(root, file), 'utf8'));
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
   * Opens the project in `root`. User actions are not loaded yet; plan branch
   * 4 adds that here.
   *
   * @param root - The project's root folder, absolute or relative to the
   *   engine's working directory.
   * @param actions - Every known action by name.
   * @returns The open project.
   * @throws RpcError `ProjectInvalid` when the folder has no readable config
   *   file.
   */
  static open(root: string, actions: ReadonlyMap<string, ActionSpec>): Project {
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
    return new Project(absoluteRoot, configFile, text, actions);
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
    // Flows may have changed on disk since the last request.
    this.flowCache.clear();
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
            const absolute = join(this.root, projectPath);
            return {
              file: projectPath,
              text: existsSync(absolute) ? readFileSync(absolute, 'utf8') : undefined,
            };
          });
    for (const { file, text } of inputs) {
      if (file.startsWith('../')) {
        diagnostics.push(
          problem(file, 'FileOutsideProject', `"${file}" is outside the project folder.`),
        );
        continue;
      }
      if (text === undefined) {
        diagnostics.push(
          problem(file, 'FileNotFound', `There is no file "${file}" in the project.`),
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

  private validateOne(file: string, text: string): FileValidation | undefined {
    const kind = fileKindOf(file, PRODUCT.configFile);
    return kind === undefined ? undefined : validateFile(file, text, kind, this.actions);
  }

  private toProjectPath(file: string): string {
    const absolute = isAbsolute(file) ? file : join(this.root, file);
    return posix(relative(this.root, absolute));
  }

  private context(): CheckContext {
    return {
      config: this.config,
      sharedTargets: this.sharedTargets,
      actions: this.actions,
      loadFlow: (file) => {
        const projectPath = this.toProjectPath(file);
        if (!this.flowCache.has(projectPath)) {
          const absolute = join(this.root, projectPath);
          const exists =
            !projectPath.startsWith('../') && existsSync(absolute) && statSync(absolute).isFile();
          this.flowCache.set(
            projectPath,
            exists ? this.validateOne(projectPath, readFileSync(absolute, 'utf8')) : undefined,
          );
        }
        return this.flowCache.get(projectPath);
      },
      knownFlowFiles: () => this.files.flows,
      exists: (file) => existsSync(join(this.root, file)),
    };
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
