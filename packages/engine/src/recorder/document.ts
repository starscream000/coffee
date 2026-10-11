// The recorded test file (docs/recording.md): what has been recorded so far,
// and its text in the canonical form a person would write. Steps use their
// action's shorthand when they have only that parameter, other parameters
// sit in a flow mapping on one line, and a step marked for review has a
// `# review:` comment above it.

import { Document, isMap, isSeq } from 'yaml';
import { BUILTIN_SPECS_BY_NAME } from '../actions/builtin-specs.js';
import type { Candidate } from '../schema/targets.js';

/** A target the recorder wrote: its candidates and, inside a frame, the frame's target. */
export interface RecordedTarget {
  /** The candidates that passed the check, in order. */
  readonly candidates: readonly Candidate[];
  /** The name of the frame's target, for an element inside a frame. */
  readonly frame?: string | undefined;
  /** The name of the target of a container the candidates are searched in. */
  readonly within?: string | undefined;
}

/** One recorded step. */
export interface RecordedStep {
  /** The action, such as `click`. */
  readonly action: string;
  /** Its parameters in the long form, text already escaped. */
  readonly params: Readonly<Record<string, unknown>>;
  /** The page it acts on, when not `main`. */
  readonly page?: string | undefined;
  /** The name of the tab it opens. */
  readonly opens?: string | undefined;
  /** Why it is marked for review. */
  readonly review?: string | undefined;
}

/** Everything a recording holds. */
export interface Recording {
  /** The test's name. */
  readonly name: string;
  /** The saved login of the main page. */
  readonly login?: string | undefined;
  /** Placeholder variables, declared empty. */
  readonly vars: ReadonlyMap<string, string>;
  /** The targets, in the order they were made. */
  readonly targets: ReadonlyMap<string, RecordedTarget>;
  /** The steps. */
  readonly steps: readonly RecordedStep[];
}

/** A target as the file holds it: a list of candidates, or the long form with `frame` or `within`. */
function targetValue(target: RecordedTarget): unknown {
  const candidates = target.candidates.map((candidate) => ({ ...candidate }));
  if (target.within !== undefined) return { within: target.within, candidates };
  return target.frame === undefined ? candidates : { frame: target.frame, candidates };
}

/**
 * A step in the canonical form: `{ click: name }` when only the shorthand
 * parameter is given, otherwise the parameters as a mapping; then `page` and
 * `opens`.
 *
 * @param step - A recorded step.
 * @returns The step as a plain value for the YAML document.
 */
export function stepValue(step: RecordedStep): Record<string, unknown> {
  const shorthand = BUILTIN_SPECS_BY_NAME.get(step.action)?.shorthand;
  const keys = Object.keys(step.params);
  const short = shorthand !== undefined && keys.length === 1 && keys[0] === shorthand;
  return {
    [step.action]: short ? step.params[shorthand] : { ...step.params },
    ...(step.page === undefined ? {} : { page: step.page }),
    ...(step.opens === undefined ? {} : { opens: step.opens }),
  };
}

/**
 * The text of the recorded test file.
 *
 * @param recording - What has been recorded.
 * @returns A YAML document, ending with a newline.
 */
export function renderRecording(recording: Recording): string {
  const doc = new Document({
    version: 1,
    name: recording.name,
    ...(recording.login === undefined ? {} : { login: recording.login }),
    ...(recording.vars.size === 0 ? {} : { vars: Object.fromEntries(recording.vars) }),
    ...(recording.targets.size === 0
      ? {}
      : {
          targets: Object.fromEntries(
            [...recording.targets].map(([name, target]) => [name, targetValue(target)]),
          ),
        }),
    steps: recording.steps.map(stepValue),
  });
  const steps = doc.get('steps', true);
  if (isSeq(steps)) {
    steps.items.forEach((item, index) => {
      if (!isMap(item)) return;
      const step = recording.steps[index];
      // The parameters of a step stay on one line, as people write them.
      const params = item.items[0]?.value;
      if (isMap(params)) params.flow = true;
      if (step?.review !== undefined) item.commentBefore = ` review: ${step.review}`;
    });
  }
  return doc.toString({ lineWidth: 0, flowCollectionPadding: true, singleQuote: true });
}
