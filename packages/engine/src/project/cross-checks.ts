// Cross-file checks (ADR 0006, pass 3): target names and their cycles, flows
// and their parameters and cycles, page names, `${…}` namespaces, declared
// secrets, environment values, logins and data files. They work on the raw
// data and the normalised steps, so they still run when a file has other
// problems, and every problem is reported at once.

import type { Diagnostic } from '@cfe/protocol';
import { targetParamKeys, type ActionSpec } from '../actions/action-spec.js';
import type { ConfigFile, FlowParam } from '../schema/files.js';
import { TargetSchema } from '../schema/targets.js';
import { DiagnosticSink, didYouMeanHint } from '../stepfile/diagnostics.js';
import { NAMESPACES, referencesInValue } from '../stepfile/interpolation.js';
import type { DataPath, SourceFile } from '../stepfile/source.js';
import type { NormalizedStep } from '../stepfile/steps.js';
import type { FileValidation } from '../stepfile/validate-file.js';

/** A target defined in a shared `*.targets.yaml` file. */
export interface SharedTarget {
  /** The target's name. */
  readonly name: string;
  /** The file that defines it. */
  readonly source: SourceFile;
  /** Data path of its definition in that file. */
  readonly path: DataPath;
  /** The definition as written. */
  readonly value: unknown;
}

/** What the cross-file checks need from the project. */
export interface CheckContext {
  /** The project config, when it is valid; checks that need it are skipped otherwise. */
  readonly config: ConfigFile | undefined;
  /** Every shared target by name (the first definition wins). */
  readonly sharedTargets: ReadonlyMap<string, SharedTarget>;
  /** Every known action by name. */
  readonly actions: ReadonlyMap<string, ActionSpec>;
  /** Validates (and caches) a flow file, by project-relative path; `undefined` if it does not exist. */
  loadFlow(file: string): FileValidation | undefined;
  /** Every flow file the project's globs find, for "did you mean" hints. */
  knownFlowFiles(): readonly string[];
  /** Whether a project-relative file exists. */
  exists(file: string): boolean;
}

type Raw = Record<string, unknown>;

function isMapping(value: unknown): value is Raw {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function where(source: SourceFile, path: DataPath): string {
  const { line, column } = source.positionOf(path);
  return `${source.file}:${String(line)}:${String(column)}`;
}

interface TargetRef {
  readonly name: string;
  readonly path: DataPath;
}

/** References by name inside a target value (itself, or its frame / within). */
function refsOfTarget(value: unknown, path: DataPath): TargetRef[] {
  if (typeof value === 'string') {
    return [{ name: value, path }];
  }
  if (isMapping(value)) {
    return [
      ...('frame' in value ? refsOfTarget(value.frame, [...path, 'frame']) : []),
      ...('within' in value ? refsOfTarget(value.within, [...path, 'within']) : []),
    ];
  }
  return [];
}

/** Only the frame / within references of a definition (not the definition itself). */
function nestedRefs(value: unknown, path: DataPath): TargetRef[] {
  return typeof value === 'string' ? [] : refsOfTarget(value, path);
}

/**
 * Runs every cross-file check on one validated file, and on the flows it
 * calls.
 *
 * @param validation - The file's own validation result.
 * @param context - What the checks need from the project.
 * @param flowStack - Flow files currently being checked, for cycle detection.
 * @returns Diagnostics for this file and for every flow it reaches. The
 *   caller removes duplicates.
 */
export function crossCheck(
  validation: FileValidation,
  context: CheckContext,
  flowStack: readonly string[] = [],
): Diagnostic[] {
  const sink = new DiagnosticSink(validation.source);
  const raw = validation.source.data;
  if (!isMapping(raw)) {
    return [];
  }
  const extra: Diagnostic[] = [];

  if (validation.kind === 'test' || validation.kind === 'flow' || validation.kind === 'targets') {
    checkTargets(validation, raw, context, sink);
  }
  if (validation.kind === 'test' || validation.kind === 'flow') {
    extra.push(...checkFlowCalls(validation, context, sink, [...flowStack, validation.file]));
  }
  if (validation.kind === 'test') {
    checkPages(validation, raw, sink);
    checkLogins(raw, context, sink);
    checkData(validation, raw, context, sink);
  }
  checkInterpolation(validation, raw, context, sink);
  return [...sink.items, ...extra];
}

function checkTargets(
  validation: FileValidation,
  raw: Raw,
  context: CheckContext,
  sink: DiagnosticSink,
): void {
  const shared = context.sharedTargets;
  const local = new Map<string, { path: DataPath; value: unknown }>();
  const isTargetsFile = validation.kind === 'targets';

  if (isMapping(raw.targets)) {
    for (const [name, value] of Object.entries(raw.targets)) {
      const path: DataPath = ['targets', name];
      if (!TargetSchema.safeParse(value).success) {
        continue; // its own validation already reported why
      }
      const other = shared.get(name);
      if (other !== undefined && other.source.file !== validation.source.file) {
        sink.error(
          path,
          'DuplicateTarget',
          `The target "${name}" is already defined in ${where(other.source, other.path)}.`,
          isTargetsFile
            ? 'Shared target names must be unique across the project; rename one of them.'
            : 'A test or flow may not reuse a shared target name; rename this one.',
          true,
        );
      }
      local.set(name, { path, value });
    }
  }

  const known = (): string[] => [...local.keys(), ...shared.keys()];
  const exists = (name: string): boolean => local.has(name) || shared.has(name);
  const reportUnknown = (ref: TargetRef): void => {
    sink.error(
      ref.path,
      'UnknownTarget',
      `There is no target named "${ref.name}".`,
      didYouMeanHint(ref.name, known()) ??
        'Define it under "targets:" in this file or in a shared *.targets.yaml file.',
    );
  };

  // References inside this file's own target definitions.
  for (const [, { path, value }] of local) {
    for (const ref of nestedRefs(value, path)) {
      if (!exists(ref.name)) reportUnknown(ref);
    }
  }
  // References from steps.
  for (const step of validation.steps) {
    const spec = context.actions.get(step.action);
    if (spec === undefined) continue;
    for (const key of targetParamKeys(spec)) {
      const value = step.params[key];
      if (value === undefined) continue;
      const path: DataPath =
        step.form === 'shorthand' && spec.shorthand === key
          ? step.actionPath
          : [...step.actionPath, key];
      for (const ref of refsOfTarget(value, path)) {
        if (!exists(ref.name)) reportUnknown(ref);
      }
    }
  }

  // Cycles through frame / within, starting from this file's definitions.
  const definition = (name: string): { inThisFile: boolean; refs: TargetRef[] } | undefined => {
    const mine = local.get(name);
    if (mine !== undefined) return { inThisFile: true, refs: nestedRefs(mine.value, mine.path) };
    const theirs = shared.get(name);
    return theirs === undefined
      ? undefined
      : { inThisFile: false, refs: nestedRefs(theirs.value, theirs.path) };
  };
  const reported = new Set<string>();
  const visit = (name: string, stack: string[]): void => {
    const def = definition(name);
    if (def === undefined) return;
    for (const ref of def.refs) {
      const at = stack.indexOf(ref.name);
      if (at !== -1) {
        const cycle = [...stack.slice(at), ref.name];
        const key = [...new Set(cycle)].sort().join(',');
        if (def.inThisFile && !reported.has(key)) {
          reported.add(key);
          sink.error(
            ref.path,
            'TargetCycle',
            `Targets refer to each other in a circle: ${cycle.join(' → ')}.`,
            'A target can be inside (or in the frame of) another, but not inside itself.',
          );
        }
        continue;
      }
      if (stack.length < 50) visit(ref.name, [...stack, ref.name]);
    }
  };
  for (const name of local.keys()) {
    visit(name, [name]);
  }
}

function checkFlowCalls(
  validation: FileValidation,
  context: CheckContext,
  sink: DiagnosticSink,
  flowStack: readonly string[],
): Diagnostic[] {
  const nested: Diagnostic[] = [];
  for (const step of validation.steps) {
    if (step.action !== 'call' || !step.paramsValid) continue;
    const flowFile = step.params.flow;
    if (typeof flowFile !== 'string') continue;
    const flowPath: DataPath =
      step.form === 'shorthand' ? step.actionPath : [...step.actionPath, 'flow'];
    if (flowFile.includes('${')) {
      sink.error(
        flowPath,
        'InvalidValue',
        'The flow path cannot use ${…}; write the path of the flow file.',
      );
      continue;
    }
    const flow = context.loadFlow(flowFile);
    if (flow === undefined) {
      sink.error(
        flowPath,
        'FlowNotFound',
        `There is no flow file "${flowFile}". Flow paths are relative to the project root.`,
        didYouMeanHint(flowFile, context.knownFlowFiles()),
      );
      continue;
    }
    if (flow.kind !== 'flow') {
      sink.error(
        flowPath,
        'FlowNotFound',
        `"${flowFile}" is not a flow file; flow files end in .flow.yaml.`,
      );
      continue;
    }
    checkFlowParams(step, flow, sink);
    const at = flowStack.indexOf(flow.file);
    if (at !== -1) {
      const chain = [...flowStack.slice(at), flow.file].map(
        (file) => file.split('/').at(-1) ?? file,
      );
      sink.error(
        flowPath,
        'FlowCycle',
        `Flow calls itself: ${chain.join(' → ')}.`,
        'Remove one of the calls so the flows no longer call each other in a circle.',
      );
      continue;
    }
    nested.push(...flow.diagnostics, ...crossCheck(flow, context, flowStack));
  }
  return nested;
}

function checkFlowParams(step: NormalizedStep, flow: FileValidation, sink: DiagnosticSink): void {
  if (flow.parsed?.kind !== 'flow') return;
  const declared: Record<string, FlowParam> = flow.parsed.data.params ?? {};
  const given = isMapping(step.params.with) ? step.params.with : {};
  const withPath: DataPath = [...step.actionPath, 'with'];
  for (const [name, value] of Object.entries(given)) {
    const param = declared[name];
    if (param === undefined) {
      sink.error(
        [...withPath, name],
        'UnknownParameter',
        `The flow "${flow.file}" has no parameter "${name}".`,
        didYouMeanHint(name, Object.keys(declared)) ??
          `Its parameters: ${Object.keys(declared).join(', ') || 'none'}.`,
        true,
      );
    } else if (
      !(typeof value === 'string' && value.includes('${')) &&
      typeof value !== param.type
    ) {
      sink.error(
        [...withPath, name],
        'InvalidParameterType',
        `The flow parameter "${name}" must be a ${param.type}.`,
      );
    }
  }
  for (const [name, param] of Object.entries(declared)) {
    if (param.default === undefined && !(name in given)) {
      sink.error(
        step.actionPath,
        'MissingParameter',
        `The flow "${flow.file}" needs the parameter "${name}".`,
        `Add it under "with:", for example: with: { ${name}: … }`,
      );
    }
  }
}

function checkPages(validation: FileValidation, raw: Raw, sink: DiagnosticSink): void {
  const declared = new Set<string>([
    'main',
    ...(isMapping(raw.pages) ? Object.keys(raw.pages) : []),
  ]);
  const opensAt = new Map<string, NormalizedStep>();
  for (const step of validation.steps) {
    if (step.opens !== undefined && !opensAt.has(step.opens)) opensAt.set(step.opens, step);
  }
  const opened = new Set<string>();
  for (const step of validation.steps) {
    if (step.page !== undefined && !declared.has(step.page) && !opened.has(step.page)) {
      const later = opensAt.get(step.page);
      if (later !== undefined) {
        const { line } = validation.source.positionOf(later.path);
        sink.error(
          [...step.path, 'page'],
          'PageNotYetOpened',
          `The page "${step.page}" is opened only by the step at line ${String(line)}.`,
          'Use a page only after the step that opens it.',
        );
      } else {
        sink.error(
          [...step.path, 'page'],
          'UnknownPage',
          `There is no page named "${step.page}".`,
          didYouMeanHint(step.page, [...declared, ...opened]) ??
            'Declare it under "pages:", or name the page a step opens with "opens:".',
        );
      }
    }
    if (step.opens !== undefined) {
      if (declared.has(step.opens) || opened.has(step.opens)) {
        sink.error(
          [...step.path, 'opens'],
          'PageNameTaken',
          `The page name "${step.opens}" is already in use in this test.`,
          'Give the new page a name of its own.',
        );
      } else {
        opened.add(step.opens);
      }
    }
  }
}

function checkLogins(raw: Raw, context: CheckContext, sink: DiagnosticSink): void {
  if (context.config === undefined) return;
  const logins = Object.keys(context.config.logins ?? {});
  const check = (name: unknown, path: DataPath): void => {
    if (typeof name === 'string' && !logins.includes(name)) {
      sink.error(
        path,
        'UnknownLogin',
        `There is no saved login named "${name}" in the config.`,
        didYouMeanHint(name, logins) ?? 'Declare it under "logins:" in the config.',
      );
    }
  };
  check(raw.login, ['login']);
  if (isMapping(raw.pages)) {
    for (const [page, value] of Object.entries(raw.pages)) {
      if (isMapping(value)) check(value.login, ['pages', page, 'login']);
    }
  }
}

function checkData(
  validation: FileValidation,
  raw: Raw,
  context: CheckContext,
  sink: DiagnosticSink,
): void {
  const data = raw.data;
  if (typeof data !== 'string') return;
  if (!/\.(csv|ya?ml)$/i.test(data)) {
    sink.error(
      ['data'],
      'UnsupportedDataFile',
      `"${data}" is not a CSV or YAML file.`,
      'Use a .csv file (first line is the header) or a .yaml file (a list of mappings).',
    );
    return;
  }
  const base = validation.file.split('/').slice(0, -1);
  const parts = [...base];
  for (const part of data.split(/[\\/]/)) {
    if (part === '..') parts.pop();
    else if (part !== '.' && part !== '') parts.push(part);
  }
  const resolved = parts.join('/');
  if (!context.exists(resolved)) {
    sink.error(
      ['data'],
      'DataFileNotFound',
      `The data file "${data}" does not exist (looked for ${resolved}).`,
      'Data file paths are relative to the test file.',
    );
  }
}

const ALLOWED: Record<FileValidation['kind'], readonly string[]> = {
  test: ['vars', 'env', 'secrets', 'row'],
  flow: ['vars', 'env', 'secrets', 'params'],
  targets: ['vars', 'env', 'secrets', 'row', 'params'],
  config: ['env', 'secrets'],
};

function checkInterpolation(
  validation: FileValidation,
  raw: Raw,
  context: CheckContext,
  sink: DiagnosticSink,
): void {
  const allowed = new Set(ALLOWED[validation.kind]);
  if (validation.kind === 'test' && raw.data === undefined) {
    allowed.delete('row');
  }
  const config = context.config;
  const flowParams =
    validation.kind === 'flow' && isMapping(raw.params) ? Object.keys(raw.params) : undefined;

  for (const ref of referencesInValue(raw, [])) {
    const { namespace, path } = ref;
    if (ref.unclosed === true) {
      sink.error(
        ref.at,
        'UnclosedInterpolation',
        'This value has "${" without a closing "}".',
        'Close it, as in ${vars.email}, or write $${ for a literal "${".',
      );
      continue;
    }
    if (!(NAMESPACES as readonly string[]).includes(namespace)) {
      sink.error(
        ref.at,
        'UnknownNamespace',
        `"\${${ref.expression}}" uses "${namespace}", which is not a namespace.`,
        `Use one of: ${[...allowed].join(', ')}.`,
      );
      continue;
    }
    if (!allowed.has(namespace)) {
      sink.error(
        ref.at,
        'NamespaceNotAvailable',
        `"\${${ref.expression}}" uses "${namespace}", which is not available in ${validation.kind === 'test' ? 'this test' : `a ${validation.kind} file`}.`,
        namespace === 'row'
          ? 'Only tests with "data:" have a current row.'
          : `Here you can use: ${[...allowed].join(', ')}.`,
      );
      continue;
    }
    const [first] = path;
    if (first === undefined || first === '') {
      sink.error(
        ref.at,
        'InvalidInterpolation',
        `"\${${ref.expression}}" needs a name after "${namespace}.".`,
      );
      continue;
    }
    if (namespace === 'secrets' && config !== undefined) {
      const declared = config.secrets ?? [];
      if (!declared.includes(first)) {
        sink.error(
          ref.at,
          'UndeclaredSecret',
          `"${first}" is not declared as a secret in the config.`,
          didYouMeanHint(first, declared) ?? `Add it to "secrets:" in the config.`,
        );
      }
    }
    if (namespace === 'env' && config !== undefined) {
      const missing = Object.entries(config.environments)
        .filter(
          ([, env]) => first !== 'name' && first !== 'baseUrl' && !(first in (env.values ?? {})),
        )
        .map(([name]) => name);
      if (missing.length > 0) {
        const all = new Set([
          'name',
          'baseUrl',
          ...Object.values(config.environments).flatMap((env) => Object.keys(env.values ?? {})),
        ]);
        sink.error(
          ref.at,
          'UnknownEnvValue',
          `"${first}" is not a value of the environment${missing.length > 1 ? 's' : ''} ${missing.join(', ')}.`,
          didYouMeanHint(first, all) ??
            'Add it under "values:" of every environment in the config.',
        );
      }
    }
    if (namespace === 'params' && flowParams !== undefined && !flowParams.includes(first)) {
      sink.error(
        ref.at,
        'UnknownFlowParam',
        `This flow has no parameter "${first}".`,
        didYouMeanHint(first, flowParams) ?? 'Declare it under "params:".',
      );
    }
  }
}
