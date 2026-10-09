// Turns Zod validation issues into diagnostics with tester-readable messages,
// positioned through the YAML source (ADR 0006, pass 2).

import type { z } from 'zod';
import type { DiagnosticSink } from '../stepfile/diagnostics.js';
import { didYouMeanHint } from '../stepfile/diagnostics.js';
import type { DataPath } from '../stepfile/source.js';

type Issue = z.core.$ZodIssue;

/**
 * How issues of one validation are described.
 */
export interface IssueContext {
  /** What is being validated, used in messages: `"fill"`, `the test file`. */
  readonly subject: string;
  /** Code for a missing required key: `MissingParameter` or `MissingKey`. */
  readonly missingCode: string;
  /** Keys allowed in the object at `path`, for "did you mean" hints. */
  readonly knownKeysAt?: (path: DataPath) => readonly string[] | undefined;
}

const TYPE_NOUNS: Record<string, string> = {
  string: 'text',
  number: 'a number',
  int: 'a whole number',
  boolean: 'true or false',
  array: 'a list',
  object: 'a mapping',
  record: 'a mapping',
  null: 'empty',
};

function label(path: readonly PropertyKey[], subject: string): string {
  const last = path.at(-1);
  if (last === undefined) {
    return subject;
  }
  if (typeof last === 'number') {
    const parent = path.at(-2);
    return `item ${String(last + 1)}${parent === undefined ? '' : ` of "${String(parent)}"`}`;
  }
  return `"${String(last)}"`;
}

function toDataPath(path: readonly PropertyKey[]): DataPath {
  return path.map((segment) => (typeof segment === 'number' ? segment : String(segment)));
}

/** Picks the union branch the value was most likely meant for. */
function bestBranch(issue: Extract<Issue, { code: 'invalid_union' }>): Issue[] | undefined {
  const typeMatched = issue.errors.filter(
    (branch) => !branch.some((inner) => inner.code === 'invalid_type' && inner.path.length === 0),
  );
  if (typeMatched.length === 0) {
    return undefined;
  }
  return typeMatched.reduce((best, branch) => (branch.length < best.length ? branch : best));
}

/**
 * Adds one diagnostic per Zod issue to `sink`.
 *
 * @param issues - Issues from `safeParse(…, { reportInput: true })`.
 * @param base - Data path of the validated value within the file.
 * @param sink - Where the diagnostics go.
 * @param context - How to describe the validated value.
 *
 * @example
 * ```ts
 * const result = spec.params.safeParse(params, { reportInput: true });
 * if (!result.success) reportIssues(result.error.issues, ['steps', 3, 'fill'], sink, ctx);
 * ```
 */
export function reportIssues(
  issues: readonly Issue[],
  base: DataPath,
  sink: DiagnosticSink,
  context: IssueContext,
): void {
  for (const issue of issues) {
    reportIssue(issue, base, sink, context);
  }
}

function reportIssue(
  issue: Issue,
  base: DataPath,
  sink: DiagnosticSink,
  context: IssueContext,
): void {
  const relative = issue.path;
  const path = [...base, ...toDataPath(relative)];
  const what = label(relative, context.subject);
  switch (issue.code) {
    case 'invalid_union': {
      if (!('input' in issue) || issue.input === undefined) {
        reportMissing(relative, path, sink, context);
        return;
      }
      const branch = bestBranch(issue);
      if (branch === undefined) {
        sink.error(path, 'InvalidValue', `${what} has the wrong form.`, unionHint(issue));
        return;
      }
      for (const inner of branch) {
        reportIssue({ ...inner, path: [...relative, ...inner.path] }, base, sink, context);
      }
      return;
    }
    case 'unrecognized_keys': {
      const known = context.knownKeysAt?.(path) ?? [];
      for (const key of issue.keys) {
        sink.error(
          [...path, key],
          'UnknownKey',
          `"${key}" is not a key of ${relative.length === 0 ? context.subject : what}.`,
          didYouMeanHint(key, known) ??
            (known.length > 0 ? `Allowed keys: ${known.join(', ')}.` : undefined),
          true,
        );
      }
      return;
    }
    case 'invalid_type': {
      if (!('input' in issue) || issue.input === undefined) {
        reportMissing(relative, path, sink, context);
        return;
      }
      sink.error(
        path,
        'InvalidValue',
        `${what} must be ${TYPE_NOUNS[issue.expected] ?? issue.expected}.`,
      );
      return;
    }
    case 'invalid_value': {
      const values = issue.values.map((value) => JSON.stringify(value)).join(', ');
      sink.error(
        path,
        'InvalidValue',
        issue.values.length === 1
          ? `${what} must be ${values}.`
          : `${what} must be one of ${values}.`,
      );
      return;
    }
    case 'invalid_format':
    case 'too_small':
    case 'too_big':
      sink.error(path, 'InvalidValue', `${what} ${issue.message}.`);
      return;
    case 'invalid_key':
      sink.error(
        path,
        'InvalidName',
        `${what} is not a valid name: ${issue.issues[0]?.message ?? issue.message}.`,
        undefined,
        true,
      );
      return;
    case 'custom': {
      const params: unknown = issue.params;
      const code =
        typeof params === 'object' &&
        params !== null &&
        'diagnostic' in params &&
        typeof params.diagnostic === 'string'
          ? params.diagnostic
          : 'InvalidValue';
      sink.error(path, code, `${issue.message}.`);
      return;
    }
    default:
      sink.error(path, 'InvalidValue', `${what}: ${issue.message}.`);
  }
}

function reportMissing(
  relative: readonly PropertyKey[],
  path: DataPath,
  sink: DiagnosticSink,
  context: IssueContext,
): void {
  const key = relative.at(-1);
  const owner =
    relative.length <= 1 ? context.subject : label(relative.slice(0, -1), context.subject);
  const name = typeof key === 'number' ? `item ${String(key + 1)}` : `"${String(key)}"`;
  sink.error(path, context.missingCode, `${owner} needs ${name}.`, `Add ${name}.`);
}

function unionHint(issue: Extract<Issue, { code: 'invalid_union' }>): string | undefined {
  const expected = issue.errors
    .flatMap((branch) =>
      branch.filter((inner) => inner.code === 'invalid_type' && inner.path.length === 0),
    )
    .map((inner) =>
      inner.code === 'invalid_type' ? (TYPE_NOUNS[inner.expected] ?? inner.expected) : '',
    )
    .filter((noun, index, all) => noun !== '' && all.indexOf(noun) === index);
  return expected.length > 0 ? `Write it as ${expected.join(' or ')}.` : undefined;
}
