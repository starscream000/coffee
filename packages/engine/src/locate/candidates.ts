// Turns one locator candidate into a Playwright locator (docs/step-format.md,
// "Candidates"), and describes candidates for messages. Text-like fields match
// exactly unless the candidate says `exact: false`; `nth` picks one match.

import type { Frame, FrameLocator, Locator, Page } from 'playwright';
import type { Candidate } from '../schema/targets.js';

/** Where a candidate is searched: the page, a frame, a frame's content or an element. */
export type SearchScope = Page | Frame | FrameLocator | Locator;

/** The role names Playwright's `getByRole` accepts. */
type AriaRole = Parameters<Page['getByRole']>[0];

/**
 * Builds the Playwright locator for one candidate, after interpolation.
 *
 * @param scope - Where to search.
 * @param candidate - A candidate whose `${…}` have been resolved.
 * @param testIdAttribute - The attribute `testId` candidates match, such as
 *   `data-testid`.
 * @returns A locator; it may match any number of elements.
 *
 * @example
 * ```ts
 * candidateLocator(page, { role: 'button', name: 'Save' }, 'data-testid');
 * // page.getByRole('button', { name: 'Save', exact: true })
 * ```
 */
export function candidateLocator(
  scope: SearchScope,
  candidate: Candidate,
  testIdAttribute: string,
): Locator {
  const exact = candidate.exact ?? true;
  let locator: Locator;
  if (candidate.role !== undefined) {
    locator = scope.getByRole(
      candidate.role as AriaRole,
      candidate.name === undefined ? {} : { name: candidate.name, exact },
    );
  } else if (candidate.label !== undefined) {
    locator = scope.getByLabel(candidate.label, { exact });
  } else if (candidate.placeholder !== undefined) {
    locator = scope.getByPlaceholder(candidate.placeholder, { exact });
  } else if (candidate.text !== undefined) {
    locator = scope.getByText(candidate.text, { exact });
  } else if (candidate.testId !== undefined) {
    // An attribute selector instead of getByTestId: Playwright's test ID
    // attribute is one global setting, while each project may name its own.
    locator = scope.locator(`[${testIdAttribute}=${JSON.stringify(candidate.testId)}]`);
  } else {
    locator = scope.locator(candidate.css ?? '');
  }
  return candidate.nth === undefined ? locator : locator.nth(candidate.nth);
}

/**
 * Describes a candidate in one line for messages, such as
 * `role=button name="Save"` or `css="#cart .btn" nth=1`.
 *
 * @param candidate - A candidate.
 * @returns A short description.
 */
export function describeCandidate(candidate: Candidate): string {
  const parts: string[] = [];
  for (const [key, value] of Object.entries(candidate) as [
    string,
    string | number | boolean | undefined,
  ][]) {
    if (value === undefined) continue;
    parts.push(
      typeof value === 'string' && key !== 'role'
        ? `${key}=${JSON.stringify(value)}`
        : `${key}=${String(value)}`,
    );
  }
  return parts.join(' ');
}
