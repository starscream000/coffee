// Checks each step of a step list and brings it into its canonical long form
// (docs/step-format.md, "Steps"): exactly one action key, the common keys
// name / page / timeout / opens, and parameters checked against the action's
// spec. Bare and shorthand steps are normalised here and nowhere else.

import { z } from 'zod';
import { paramKeys, targetParamKeys, type ActionSpec } from '../actions/action-spec.js';
import type { NotLoaded } from '../actions/registry.js';
import { DurationSchema, PageNameSchema } from '../schema/common.js';
import { reportIssues } from '../schema/issues.js';
import { keysAt } from '../schema/shape.js';
import { didYouMeanHint, type DiagnosticSink } from './diagnostics.js';
import type { DataPath } from './source.js';

/** Keys a step may have besides its action. */
export const COMMON_STEP_KEYS = ['name', 'page', 'timeout', 'opens'] as const;

const CommonKeysSchema = z.strictObject({
  name: z.string().min(1, { error: 'must not be empty' }).optional(),
  page: PageNameSchema.optional(),
  timeout: DurationSchema.optional(),
  opens: PageNameSchema.optional(),
});

/** How a step was written. */
export type StepForm = 'bare' | 'shorthand' | 'long';

/**
 * One step in its canonical long form, with where it came from.
 */
export interface NormalizedStep {
  /** The action's name. */
  readonly action: string;
  /** Parameters in the long form; checked when `paramsValid` is true. */
  readonly params: Readonly<Record<string, unknown>>;
  /** Whether `params` passed the action's schema. */
  readonly paramsValid: boolean;
  /** How the step was written in the file. */
  readonly form: StepForm;
  /** Label shown in results, if the step sets one. */
  readonly name?: string | undefined;
  /** Page the step runs on, if not `main`. */
  readonly page?: string | undefined;
  /** Step timeout, if it overrides the default. */
  readonly timeout?: string | undefined;
  /** Name of the new page the step opens, if any. */
  readonly opens?: string | undefined;
  /** Data path of the step, such as `['steps', 3]`. */
  readonly path: DataPath;
  /** Data path of the action's value (or of the step, for a bare step). */
  readonly actionPath: DataPath;
}

function isMapping(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Checks and normalises every step of a list.
 *
 * @param list - The raw list from the file.
 * @param listPath - Data path of the list, such as `['before']`.
 * @param actions - Every known action by name.
 * @param sink - Where problems go; every problem in the list is reported.
 * @returns The steps whose action is known, in order. Steps with an unknown
 *   action, or that are not a step at all, are left out.
 *
 * @example
 * ```ts
 * const steps = normalizeSteps(data.steps, ['steps'], BUILTIN_SPECS_BY_NAME, sink);
 * ```
 */
export function normalizeSteps(
  list: readonly unknown[],
  listPath: DataPath,
  actions: ReadonlyMap<string, ActionSpec>,
  sink: DiagnosticSink,
  notLoaded?: NotLoaded,
): NormalizedStep[] {
  const result: NormalizedStep[] = [];
  list.forEach((raw, index) => {
    const step = normalizeStep(raw, [...listPath, index], actions, sink, notLoaded);
    if (step !== undefined) {
      result.push(step);
    }
  });
  return result;
}

function normalizeStep(
  raw: unknown,
  path: DataPath,
  actions: ReadonlyMap<string, ActionSpec>,
  sink: DiagnosticSink,
  notLoaded?: NotLoaded,
): NormalizedStep | undefined {
  if (typeof raw === 'string') {
    const spec = actions.get(raw);
    if (spec === undefined) {
      reportUnknownAction(raw, path, actions, sink, false, notLoaded);
      return undefined;
    }
    return finish(spec, undefined, path, path, 'bare', {}, sink);
  }
  if (!isMapping(raw)) {
    sink.error(
      path,
      'InvalidStep',
      'A step must be an action name (such as "back") or a mapping with one action (such as "goto: /home").',
    );
    return undefined;
  }

  const common = new Set<string>(COMMON_STEP_KEYS);
  const others = Object.keys(raw).filter((key) => !common.has(key));
  const known = others.filter((key) => actions.has(key));
  if (others.length === 0) {
    sink.error(
      path,
      'MissingAction',
      'This step has no action.',
      'Add one action, such as "click: …".',
    );
    return undefined;
  }
  if (known.length > 1) {
    sink.error(
      [...path, known[1] ?? ''],
      'TwoActions',
      `A step calls exactly one action, but this one has ${known.map((key) => `"${key}"`).join(' and ')}.`,
      'Split it into separate steps.',
      true,
    );
    return undefined;
  }
  const unknown = others.filter((key) => !actions.has(key));
  if (known.length === 0) {
    const [first, ...rest] = unknown;
    reportUnknownAction(first ?? '', [...path, first ?? ''], actions, sink, true, notLoaded);
    for (const key of rest) {
      reportUnknownStepKey(key, [...path, key], actions, sink);
    }
    return undefined;
  }
  for (const key of unknown) {
    reportUnknownStepKey(key, [...path, key], actions, sink);
  }

  const actionKey = known[0] ?? '';
  const spec = actions.get(actionKey);
  if (spec === undefined) {
    return undefined;
  }
  const commonValues = Object.fromEntries(Object.entries(raw).filter(([key]) => common.has(key)));
  const checkedCommon = CommonKeysSchema.safeParse(commonValues, { reportInput: true });
  if (!checkedCommon.success) {
    reportIssues(checkedCommon.error.issues, path, sink, {
      subject: 'this step',
      missingCode: 'MissingKey',
    });
  }
  const value = raw[actionKey];
  const form: StepForm =
    value === null || value === undefined ? 'bare' : isMapping(value) ? 'long' : 'shorthand';
  return finish(
    spec,
    value,
    path,
    [...path, actionKey],
    form,
    checkedCommon.success ? checkedCommon.data : {},
    sink,
  );
}

function finish(
  spec: ActionSpec,
  value: unknown,
  path: DataPath,
  actionPath: DataPath,
  form: StepForm,
  common: z.infer<typeof CommonKeysSchema>,
  sink: DiagnosticSink,
): NormalizedStep {
  let params: Record<string, unknown>;
  if (form === 'long' && isMapping(value)) {
    params = value;
  } else if (form === 'shorthand') {
    if (spec.shorthand === undefined) {
      sink.error(
        actionPath,
        'NoShorthand',
        `"${spec.name}" has no shorthand, so its parameters must be written as a mapping.`,
        paramKeys(spec).length > 0
          ? `For example: ${spec.name}: { ${paramKeys(spec)
              .slice(0, 2)
              .map((key) => `${key}: …`)
              .join(', ')} }`
          : `Write it as "- ${spec.name}" without a value.`,
      );
      return {
        action: spec.name,
        params: {},
        paramsValid: false,
        form,
        ...common,
        path,
        actionPath,
      };
    }
    params = { [spec.shorthand]: value };
  } else {
    params = {};
  }

  const checked = spec.params.safeParse(params, { reportInput: true });
  if (!checked.success) {
    const issues =
      form === 'shorthand'
        ? checked.error.issues.map((issue) =>
            issue.path[0] === spec.shorthand ? { ...issue, path: issue.path.slice(1) } : issue,
          )
        : checked.error.issues;
    reportIssues(issues, actionPath, sink, {
      subject: `"${spec.name}"`,
      missingCode: 'MissingParameter',
      knownKeysAt: (issuePath) => keysAt(spec.params, issuePath.slice(actionPath.length)),
      unknownKeyHint: (key, issuePath) => {
        const [targetKey] = targetParamKeys(spec);
        const atTop = issuePath.length === actionPath.length;
        return atTop && targetKey !== undefined && ['candidates', 'frame', 'within'].includes(key)
          ? 'An inline target goes under "' +
              targetKey +
              ':", for example: ' +
              spec.name +
              ': { ' +
              targetKey +
              ': { candidates: [ … ] } }'
          : undefined;
      },
    });
  }
  return {
    action: spec.name,
    params: checked.success ? checked.data : params,
    paramsValid: checked.success,
    form,
    ...common,
    path,
    actionPath,
  };
}

function reportUnknownAction(
  name: string,
  path: DataPath,
  actions: ReadonlyMap<string, ActionSpec>,
  sink: DiagnosticSink,
  atKey: boolean,
  notLoaded?: NotLoaded,
): void {
  const rejected = notLoaded?.rejected.get(name);
  if (rejected !== undefined) {
    sink.error(
      path,
      'ActionNotLoaded',
      `The action "${name}" did not load: ${rejected.reason}`,
      `See ${rejected.location.file}:${String(rejected.location.line)}.`,
      atKey,
    );
    return;
  }
  const failed = notLoaded?.failedFiles ?? [];
  sink.error(
    path,
    'UnknownAction',
    `"${name}" is not an action.`,
    didYouMeanHint(name, actions.keys()) ??
      (failed.length > 0 && name.includes('.')
        ? `These action files failed to load, and the action may be in one of them: ${failed.join(', ')}.`
        : 'See docs/actions.md for the built-in actions.'),
    atKey,
  );
}

function reportUnknownStepKey(
  key: string,
  path: DataPath,
  actions: ReadonlyMap<string, ActionSpec>,
  sink: DiagnosticSink,
): void {
  sink.error(
    path,
    'UnknownKey',
    `"${key}" is not an action or a step key (${COMMON_STEP_KEYS.join(', ')}).`,
    didYouMeanHint(key, [...COMMON_STEP_KEYS, ...actions.keys()]),
    true,
  );
}
