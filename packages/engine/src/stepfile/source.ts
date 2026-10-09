// Parses a YAML step file while keeping the position of every node
// (ADR 0006, pass 1), and finds the node behind a data path so that every
// problem can be reported with file, line and column.

import {
  LineCounter,
  isMap,
  isPair,
  isScalar,
  isSeq,
  parseDocument,
  type Document,
  type Node,
} from 'yaml';
import type { Diagnostic } from '@cfe/protocol';

/** A path into a parsed document: map keys and sequence indexes. */
export type DataPath = readonly (string | number)[];

/** A 1-based line and column. */
export interface Position {
  /** 1-based line. */
  readonly line: number;
  /** 1-based column. */
  readonly column: number;
}

/**
 * One parsed YAML file: its data as plain values, and the document it came
 * from, so positions can be found later.
 */
export interface SourceFile {
  /** Project-relative path with forward slashes, as used in diagnostics. */
  readonly file: string;
  /** The parsed data as plain JavaScript values (`undefined` for an empty file). */
  readonly data: unknown;
  /** YAML syntax problems; when not empty, `data` must not be trusted. */
  readonly syntaxErrors: readonly Diagnostic[];
  /** Finds the position of the value (or, with `key`, the key) at `path`. */
  positionOf(path: DataPath, key?: boolean): Position;
}

const START: Position = { line: 1, column: 1 };

/**
 * Parses YAML text and keeps every node's position.
 *
 * @param file - Project-relative path, used in diagnostics.
 * @param text - The file's text.
 * @returns The parsed file; YAML syntax errors are returned as diagnostics
 *   with code `YamlSyntax`, never thrown.
 *
 * @example
 * ```ts
 * const source = parseSource('tests/a.test.yaml', text);
 * source.positionOf(['steps', 2, 'fill']); // { line: 9, column: 7 }
 * ```
 */
export function parseSource(file: string, text: string): SourceFile {
  const lineCounter = new LineCounter();
  const doc = parseDocument(text, { lineCounter, prettyErrors: false, uniqueKeys: true });
  const toPosition = (offset: number): Position => {
    const { line, col } = lineCounter.linePos(offset);
    return { line, column: col };
  };
  const syntaxErrors: Diagnostic[] = doc.errors.map((error) => {
    const { line, column } = toPosition(error.pos[0]);
    return {
      file,
      line,
      column,
      severity: 'error',
      code: 'YamlSyntax',
      message: `This is not valid YAML: ${firstSentence(error.message)}`,
      hint: 'Check the indentation and the brackets or quotes around this position.',
    };
  });
  return {
    file,
    data: syntaxErrors.length === 0 ? doc.toJS({ maxAliasCount: 100 }) : undefined,
    syntaxErrors,
    positionOf(path, key = false) {
      const node = findNode(doc, path, key);
      return node?.range ? toPosition(node.range[0]) : START;
    },
  };
}

function firstSentence(message: string): string {
  const line = message.split('\n')[0] ?? message;
  return line.replace(/ at line \d+, column \d+:?$/, '').replace(/[.:]?$/, '.');
}

/**
 * Finds the node at `path`. When the full path does not exist (for example a
 * missing key), returns the deepest node that does, so a diagnostic still
 * points at the right place.
 */
function findNode(doc: Document, path: DataPath, key: boolean): Node | undefined {
  let node: unknown = doc.contents;
  let found: Node | undefined = isNodeLike(node) ? node : undefined;
  for (let i = 0; i < path.length; i++) {
    const segment = path[i];
    const last = i === path.length - 1;
    if (isMap(node)) {
      const pair = node.items.find(
        (item) => isPair(item) && isScalar(item.key) && String(item.key.value) === String(segment),
      );
      if (pair === undefined) {
        return found;
      }
      if (last && key && isNodeLike(pair.key)) {
        return pair.key;
      }
      node = pair.value;
      // A key with no value (`- back:`) points at the key itself.
      found =
        isNodeLike(node) && !isEmptyScalar(node) ? node : isNodeLike(pair.key) ? pair.key : found;
    } else if (isSeq(node) && typeof segment === 'number') {
      node = node.items[segment];
      if (!isNodeLike(node)) {
        return found;
      }
      found = node;
    } else {
      return found;
    }
  }
  return found;
}

function isEmptyScalar(node: Node): boolean {
  return (
    isScalar(node) &&
    node.value === null &&
    node.range !== undefined &&
    node.range !== null &&
    node.range[0] === node.range[1]
  );
}

function isNodeLike(value: unknown): value is Node {
  return (
    isMap(value) ||
    isSeq(value) ||
    isScalar(value) ||
    (typeof value === 'object' && value !== null && 'range' in value)
  );
}
