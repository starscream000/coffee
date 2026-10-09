// Helpers for building validation diagnostics with tester-readable messages:
// a collector bound to one source file, and "did you mean" suggestions.

import type { Diagnostic } from '@cfe/protocol';
import type { DataPath, SourceFile } from './source.js';

/**
 * Collects diagnostics for one file, positioning each at a data path.
 *
 * @example
 * ```ts
 * const out = new DiagnosticSink(source);
 * out.error(['steps', 0], 'UnknownAction', '"clik" is not an action.', 'Did you mean "click"?');
 * ```
 */
export class DiagnosticSink {
  /** Every diagnostic collected so far, in the order found. */
  readonly items: Diagnostic[] = [];
  private readonly source: SourceFile;

  /**
   * @param source - The file the diagnostics are about.
   */
  constructor(source: SourceFile) {
    this.source = source;
  }

  /**
   * Adds an error at the value (or key) at `path`.
   *
   * @param path - Where in the file's data the problem is.
   * @param code - Stable machine-readable code, such as `UnknownAction`.
   * @param message - What is wrong, for a tester.
   * @param hint - What to do about it.
   * @param atKey - Point at the key instead of its value.
   */
  error(path: DataPath, code: string, message: string, hint?: string, atKey = false): void {
    this.add('error', path, code, message, hint, atKey);
  }

  /**
   * Adds a warning; same parameters as {@link DiagnosticSink.error}.
   *
   * @param path - Where in the file's data the problem is.
   * @param code - Stable machine-readable code.
   * @param message - What is wrong, for a tester.
   * @param hint - What to do about it.
   * @param atKey - Point at the key instead of its value.
   */
  warning(path: DataPath, code: string, message: string, hint?: string, atKey = false): void {
    this.add('warning', path, code, message, hint, atKey);
  }

  private add(
    severity: Diagnostic['severity'],
    path: DataPath,
    code: string,
    message: string,
    hint: string | undefined,
    atKey: boolean,
  ): void {
    const { line, column } = this.source.positionOf(path, atKey);
    this.items.push({
      file: this.source.file,
      line,
      column,
      severity,
      code,
      message,
      ...(hint === undefined ? {} : { hint }),
    });
  }
}

/**
 * Edit distance between two strings (insertions, deletions, substitutions and
 * swaps of neighbouring characters each cost 1).
 *
 * @param a - First string.
 * @param b - Second string.
 * @returns The number of edits that turn `a` into `b`.
 */
export function editDistance(a: string, b: string): number {
  const rows = a.length + 1;
  const cols = b.length + 1;
  const d: number[][] = Array.from({ length: rows }, (_, i) =>
    Array.from({ length: cols }, (_, j) => (i === 0 ? j : j === 0 ? i : 0)),
  );
  const at = (i: number, j: number): number => d[i]?.[j] ?? Number.POSITIVE_INFINITY;
  for (let i = 1; i < rows; i++) {
    for (let j = 1; j < cols; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      let best = Math.min(at(i - 1, j) + 1, at(i, j - 1) + 1, at(i - 1, j - 1) + cost);
      if (i > 1 && j > 1 && a[i - 1] === b[j - 2] && a[i - 2] === b[j - 1]) {
        best = Math.min(best, at(i - 2, j - 2) + 1);
      }
      const row = d[i];
      if (row !== undefined) {
        row[j] = best;
      }
    }
  }
  return at(a.length, b.length);
}

/**
 * Finds the closest known word to a misspelt one.
 *
 * @param word - What the user wrote.
 * @param known - The words that would have been valid.
 * @returns The closest known word when it is close enough to be a likely typo
 *   (at most a third of the word's length, and at most 3 edits), else
 *   `undefined`.
 *
 * @example
 * ```ts
 * didYouMean('exepct.text', ['expect.text', 'expect.url']); // "expect.text"
 * ```
 */
export function didYouMean(word: string, known: Iterable<string>): string | undefined {
  const limit = Math.min(3, Math.max(1, Math.floor(word.length / 3)));
  let best: string | undefined;
  let bestDistance = Number.POSITIVE_INFINITY;
  for (const candidate of known) {
    const distance = editDistance(word.toLowerCase(), candidate.toLowerCase());
    if (distance < bestDistance) {
      best = candidate;
      bestDistance = distance;
    }
  }
  return bestDistance <= limit ? best : undefined;
}

/**
 * Builds a "did you mean" hint, or `undefined` when nothing is close.
 *
 * @param word - What the user wrote.
 * @param known - The valid words.
 * @returns For example `Did you mean "expect.text"?`.
 */
export function didYouMeanHint(word: string, known: Iterable<string>): string | undefined {
  const match = didYouMean(word, known);
  return match === undefined ? undefined : `Did you mean "${match}"?`;
}
