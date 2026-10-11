// Candidates for a recorded element (docs/recording.md, "Candidates, and how
// they are checked"; ADR 0022, R5): proposed from what the page script saw
// and from Playwright's accessibility tree, then each one checked with the
// locator ctx.locate would build: it must match exactly one attached element,
// and that element must be the one the person touched.

import type { Locator } from 'playwright';
import { parse } from 'yaml';
import { candidateLocator, type SearchScope } from '../locate/candidates.js';
import type { Candidate } from '../schema/targets.js';

/** What the page script reports about an element's nearest container (a list item, row, form, …). */
export interface ContainerFacts {
  /** Its marker id. */
  readonly mark: string;
  /** Tag name, lower case. */
  readonly tag: string;
  /** Its first piece of text outside the element, such as a row's first cell. */
  readonly label?: string | undefined;
  /** Its visible text, whitespace collapsed. */
  readonly text?: string | undefined;
  /** The value of the project's test ID attribute. */
  readonly testId?: string | undefined;
}

/** What the page script reports about an element. */
export interface ElementFacts {
  /** The nearest list item, table row, form, dialog, section, fieldset or article around it. */
  readonly container?: ContainerFacts | undefined;
  /** Tag name, lower case. */
  readonly tag: string;
  /** The `type` attribute. */
  readonly type?: string | undefined;
  /** The `id` attribute. */
  readonly id?: string | undefined;
  /** The `placeholder` attribute. */
  readonly placeholder?: string | undefined;
  /** The value of the project's test ID attribute. */
  readonly testId?: string | undefined;
  /** Visible text, whitespace collapsed; absent for form fields. */
  readonly text?: string | undefined;
  /** Whether it is a form field (text field, checkbox, radio, select, file). */
  readonly field: boolean;
  /** A CSS selector for it: `#id`, or a path of tags with `:nth-of-type()`. */
  readonly css: string;
}

/** The kind of a candidate, as counted in {@link CheckStats}. */
export type CandidateKind = 'role' | 'label' | 'placeholder' | 'testId' | 'text' | 'css';

/** How many candidates of each kind were proposed and rejected by the check. */
export interface CheckStats {
  /** Proposed candidates by kind. */
  readonly proposed: Record<CandidateKind, number>;
  /** Proposed candidates that did not find exactly the touched element, by kind. */
  readonly rejected: Record<CandidateKind, number>;
}

/** The longest visible text proposed as a text candidate. */
const MAX_TEXT = 80;

/**
 * New, empty statistics.
 *
 * @returns Zero for every kind.
 */
export function emptyStats(): CheckStats {
  const zero = (): Record<CandidateKind, number> => ({
    role: 0,
    label: 0,
    placeholder: 0,
    testId: 0,
    text: 0,
    css: 0,
  });
  return { proposed: zero(), rejected: zero() };
}

/** A role and accessible name, as Playwright's accessibility tree gives them. */
export interface AriaIdentity {
  /** The ARIA role, such as `button`. */
  readonly role: string;
  /** The accessible name; empty when the element has none. */
  readonly name: string;
}

/**
 * Reads the role and name of an element from the first line of its
 * `Locator.ariaSnapshot()`, such as `- button "Add"` or
 * `- textbox "Card number": "4242"`.
 *
 * @param snapshot - The snapshot, a YAML list.
 * @returns The role and name, or undefined when the snapshot has no element line.
 */
export function parseAriaLine(snapshot: string): AriaIdentity | undefined {
  let parsed: unknown;
  try {
    parsed = parse(snapshot);
  } catch {
    return undefined;
  }
  const first: unknown = Array.isArray(parsed) ? parsed[0] : undefined;
  const line =
    typeof first === 'string'
      ? first
      : typeof first === 'object' && first !== null
        ? Object.keys(first)[0]
        : undefined;
  if (line === undefined) return undefined;
  const match = /^([a-z]+)(?: "((?:[^"\\]|\\.)*)")?/.exec(line);
  if (match?.[1] === undefined) return undefined;
  let name = '';
  if (match[2] !== undefined) {
    try {
      name = JSON.parse(`"${match[2]}"`) as string;
    } catch {
      name = match[2];
    }
  }
  return { role: match[1], name };
}

/**
 * The candidates to check, in the order of reliability: role and name, label,
 * placeholder, test ID, text, CSS.
 *
 * @param facts - What the page script saw.
 * @param aria - The element's role and name, when Playwright gives them.
 * @returns Candidates with the page's own text, not yet escaped for a step file.
 */
export function proposeCandidates(
  facts: ElementFacts,
  aria: AriaIdentity | undefined,
): Candidate[] {
  const proposals: Candidate[] = [];
  if (aria !== undefined && aria.name !== '') proposals.push({ role: aria.role, name: aria.name });
  if (facts.field && aria !== undefined && aria.name !== '') proposals.push({ label: aria.name });
  if (facts.placeholder !== undefined) proposals.push({ placeholder: facts.placeholder });
  if (facts.testId !== undefined) proposals.push({ testId: facts.testId });
  if (!facts.field && facts.text !== undefined && facts.text.length <= MAX_TEXT) {
    proposals.push({ text: facts.text });
  }
  if (facts.css !== '') proposals.push({ css: facts.css });
  return proposals;
}

/**
 * The candidates to check for a container: its role with its label (a part of
 * its name, as a row's name holds every cell), its role and full name, its
 * label as text, its full text, its test ID.
 *
 * @param container - What the page script saw.
 * @param aria - The container's role and name, when Playwright gives them.
 * @returns Candidates with the page's own text, not yet escaped.
 */
export function proposeContainerCandidates(
  container: ContainerFacts,
  aria: AriaIdentity | undefined,
): Candidate[] {
  const proposals: Candidate[] = [];
  const named = aria !== undefined && aria.role !== 'generic' && aria.role !== 'none';
  if (named && container.label !== undefined && aria.name.includes(container.label)) {
    proposals.push({ role: aria.role, name: container.label, exact: false });
  }
  if (named && aria.name !== '' && aria.name.length <= MAX_TEXT) {
    proposals.push({ role: aria.role, name: aria.name });
  }
  if (container.label !== undefined) proposals.push({ text: container.label, exact: false });
  if (container.text !== undefined && container.text.length <= MAX_TEXT) {
    proposals.push({ text: container.text });
  }
  if (container.testId !== undefined) proposals.push({ testId: container.testId });
  return proposals;
}

/**
 * The kind of a candidate.
 *
 * @param candidate - A candidate.
 * @returns `role`, `label`, `placeholder`, `testId`, `text` or `css`.
 */
export function kindOf(candidate: Candidate): CandidateKind {
  if (candidate.role !== undefined) return 'role';
  if (candidate.label !== undefined) return 'label';
  if (candidate.placeholder !== undefined) return 'placeholder';
  if (candidate.testId !== undefined) return 'testId';
  if (candidate.text !== undefined) return 'text';
  return 'css';
}

/**
 * Keeps the candidates that find exactly the touched element: the locator
 * ctx.locate would build matches one attached element, and that element
 * carries the touched element's marker.
 *
 * @param scope - Where to search: the element's frame, or a container's locator.
 * @param marked - A locator for the touched element (its marker attribute).
 * @param proposals - Candidates in order.
 * @param testIdAttribute - The project's test ID attribute.
 * @param stats - Counts proposals and rejections by kind.
 * @returns The candidates that passed, in order.
 */
export async function checkCandidates(
  scope: SearchScope,
  marked: Locator,
  proposals: readonly Candidate[],
  testIdAttribute: string,
  stats: CheckStats,
): Promise<Candidate[]> {
  const passed: Candidate[] = [];
  for (const candidate of proposals) {
    const kind = kindOf(candidate);
    stats.proposed[kind] += 1;
    let ok: boolean;
    try {
      const locator = candidateLocator(scope, candidate, testIdAttribute);
      ok = (await locator.count()) === 1 && (await locator.and(marked).count()) === 1;
    } catch {
      // A selector Playwright rejects finds nothing.
      ok = false;
    }
    if (ok) passed.push(candidate);
    else stats.rejected[kind] += 1;
  }
  return passed;
}

/**
 * The candidates a target gets: the first two that are not CSS, then CSS when
 * it passed.
 *
 * @param passed - The candidates that passed, in order.
 * @returns The candidates to write, and whether only CSS identifies the element.
 */
export function chooseCandidates(passed: readonly Candidate[]): {
  candidates: Candidate[];
  cssOnly: boolean;
} {
  const others = passed.filter((candidate) => candidate.css === undefined).slice(0, 2);
  const css = passed.find((candidate) => candidate.css !== undefined);
  return {
    candidates: [...others, ...(css === undefined ? [] : [css])],
    cssOnly: others.length === 0 && css !== undefined,
  };
}
