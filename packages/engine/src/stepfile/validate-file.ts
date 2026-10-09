// Validates one step file on its own (ADR 0006, passes 1 and 2): YAML syntax,
// the format version, the file's schema, and every step in its canonical long
// form. Cross-file checks (pass 3) build on the result.

import type { Diagnostic } from '@cfe/protocol';
import type { ActionSpec } from '../actions/action-spec.js';
import type { NotLoaded } from '../actions/registry.js';
import {
  FILE_SCHEMAS,
  SUPPORTED_VERSION,
  type ConfigFile,
  type FileKind,
  type FlowFile,
  type TargetsFile,
  type TestFile,
} from '../schema/files.js';
import { reportIssues } from '../schema/issues.js';
import { keysAt } from '../schema/shape.js';
import { DiagnosticSink } from './diagnostics.js';
import { parseSource, type SourceFile } from './source.js';
import { normalizeSteps, type NormalizedStep } from './steps.js';

/** The step lists of a test, normalised. */
export interface TestSteps {
  /** Setup steps. */
  readonly before: readonly NormalizedStep[];
  /** The test's steps. */
  readonly steps: readonly NormalizedStep[];
  /** Clean-up steps. */
  readonly after: readonly NormalizedStep[];
}

/** A file that passed its own validation, with its data typed by kind. */
export type ParsedFile =
  | { readonly kind: 'test'; readonly data: TestFile; readonly steps: TestSteps }
  | { readonly kind: 'flow'; readonly data: FlowFile; readonly steps: readonly NormalizedStep[] }
  | { readonly kind: 'targets'; readonly data: TargetsFile }
  | { readonly kind: 'config'; readonly data: ConfigFile };

/**
 * The outcome of validating one file on its own.
 */
export interface FileValidation {
  /** The file's project-relative path. */
  readonly file: string;
  /** The kind the file was validated as. */
  readonly kind: FileKind;
  /** The parsed source, for positioning later diagnostics. */
  readonly source: SourceFile;
  /** The file's data, when it passed its schema; otherwise `undefined`. */
  readonly parsed: ParsedFile | undefined;
  /** Every step that has a known action, even if the file has other problems. */
  readonly steps: readonly NormalizedStep[];
  /** Every problem found in this file. */
  readonly diagnostics: readonly Diagnostic[];
}

const KIND_LABEL: Record<FileKind, string> = {
  test: 'the test file',
  flow: 'the flow file',
  targets: 'the targets file',
  config: 'the config file',
};

const STEP_SECTIONS: Record<FileKind, readonly string[]> = {
  test: ['before', 'steps', 'after'],
  flow: ['steps'],
  targets: [],
  config: [],
};

function isMapping(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Validates one file's text as a file of `kind`.
 *
 * @param file - Project-relative path with forward slashes.
 * @param text - The file's contents.
 * @param kind - What the file is (from its name).
 * @param actions - Every known action by name.
 * @param notLoaded - User actions and files that failed to load, so a step
 *   calling one says so.
 * @returns The parsed file and every problem found in it. Never throws for
 *   problems in the file.
 *
 * @example
 * ```ts
 * const result = validateFile('tests/a.test.yaml', text, 'test', BUILTIN_SPECS_BY_NAME);
 * for (const d of result.diagnostics) console.log(`${d.file}:${d.line}:${d.column} ${d.code}`);
 * ```
 */
export function validateFile(
  file: string,
  text: string,
  kind: FileKind,
  actions: ReadonlyMap<string, ActionSpec>,
  notLoaded?: NotLoaded,
): FileValidation {
  const source = parseSource(file, text);
  const sink = new DiagnosticSink(source);
  const result = (parsed?: ParsedFile, steps: readonly NormalizedStep[] = []): FileValidation => ({
    file,
    kind,
    source,
    parsed,
    steps,
    diagnostics: [...source.syntaxErrors, ...sink.items],
  });

  if (source.syntaxErrors.length > 0) {
    return result();
  }
  const data = source.data;
  if (!isMapping(data)) {
    sink.error(
      [],
      'InvalidFile',
      `${capitalise(KIND_LABEL[kind])} must be a mapping that starts with "version: ${String(SUPPORTED_VERSION)}".`,
    );
    return result();
  }
  if (data.version !== SUPPORTED_VERSION) {
    if (data.version === undefined) {
      sink.error(
        [],
        'MissingKey',
        `${capitalise(KIND_LABEL[kind])} needs "version: ${String(SUPPORTED_VERSION)}".`,
        'Add it as the first line.',
      );
    } else {
      sink.error(
        ['version'],
        'UnsupportedVersion',
        `Format version ${JSON.stringify(data.version)} is not supported; this engine reads version ${String(SUPPORTED_VERSION)}.`,
        'Use a newer engine, or write the file in version 1.',
      );
    }
    return result();
  }

  const schema = FILE_SCHEMAS[kind];
  const checked = schema.safeParse(data, { reportInput: true });
  if (!checked.success) {
    reportIssues(checked.error.issues, [], sink, {
      subject: KIND_LABEL[kind],
      missingCode: 'MissingKey',
      knownKeysAt: (path) => keysAt(schema, path),
    });
  }

  // Steps are checked even when the rest of the file has problems, so every
  // problem is reported at once.
  const sections: Record<string, NormalizedStep[]> = {};
  for (const section of STEP_SECTIONS[kind]) {
    const list = data[section];
    sections[section] = Array.isArray(list)
      ? normalizeSteps(list, [section], actions, sink, notLoaded)
      : [];
  }
  const allSteps = Object.values(sections).flat();

  if (!checked.success) {
    return result(undefined, allSteps);
  }
  switch (kind) {
    case 'test':
      return result(
        {
          kind,
          data: checked.data as TestFile,
          steps: {
            before: sections.before ?? [],
            steps: sections.steps ?? [],
            after: sections.after ?? [],
          },
        },
        allSteps,
      );
    case 'flow':
      return result(
        { kind, data: checked.data as FlowFile, steps: sections.steps ?? [] },
        allSteps,
      );
    case 'targets':
      return result({ kind, data: checked.data as TargetsFile });
    case 'config':
      return result({ kind, data: checked.data as ConfigFile });
  }
}

function capitalise(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}
