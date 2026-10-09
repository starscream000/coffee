// Data rows of a test (docs/step-format.md, "Data rows"; ADR 0013): inline
// rows, or a CSV or YAML file relative to the test file. CSV is read exactly as
// the document defines it, and no further: the first line is the header, each
// other line is one row, values are separated by commas and are strings.
// Quoted values are not defined there, so a CSV file with a double quote is
// rejected instead of being guessed at.

import { parse } from 'yaml';

/** One data row: column (or key) names to values. */
export type DataRow = Readonly<Record<string, unknown>>;

/** The rows of a test, or why they cannot be read. */
export type DataRows =
  | { readonly rows: readonly DataRow[]; readonly error?: undefined }
  | { readonly rows?: undefined; readonly error: string };

function isMapping(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * The project-relative path of a data file named in a test file.
 *
 * @param testFile - The test file's project-relative path.
 * @param data - The `data` value: a path relative to the test file.
 * @returns The data file's project-relative path, with forward slashes.
 */
export function dataFilePath(testFile: string, data: string): string {
  const parts = testFile.split('/').slice(0, -1);
  for (const part of data.split(/[\\/]/)) {
    if (part === '..') parts.pop();
    else if (part !== '.' && part !== '') parts.push(part);
  }
  return parts.join('/');
}

/**
 * Reads CSV text as step-format.md defines it.
 *
 * @param text - The file's text.
 * @returns The rows, each a mapping from header names to string values, or
 *   the first problem found.
 *
 * @example
 * ```ts
 * parseCsvRows('sku,price\ndesk-lamp,25\n'); // { rows: [{ sku: 'desk-lamp', price: '25' }] }
 * ```
 */
export function parseCsvRows(text: string): DataRows {
  if (text.includes('"')) {
    return {
      error:
        'Quoted CSV values are not supported: a value may not contain a double quote, a comma or a line break. Use a YAML data file for such values.',
    };
  }
  const lines = text
    .split(/\r?\n/)
    .map((line, index) => ({ line, number: index + 1 }))
    .filter(({ line }) => line.trim() !== '');
  const [header, ...body] = lines;
  if (header === undefined)
    return { error: 'The CSV file is empty; its first line must be the header.' };
  const names = header.line.split(',').map((name) => name.trim());
  const empty = names.findIndex((name) => name === '');
  if (empty !== -1) return { error: `Column ${String(empty + 1)} of the header has no name.` };
  const duplicate = names.find((name, index) => names.indexOf(name) !== index);
  if (duplicate !== undefined)
    return { error: `The header names the column "${duplicate}" twice.` };
  if (body.length === 0) return { error: 'The CSV file has a header but no rows.' };
  const rows: DataRow[] = [];
  for (const { line, number } of body) {
    const values = line.split(',').map((value) => value.trim());
    if (values.length !== names.length) {
      return {
        error: `Line ${String(number)} has ${String(values.length)} values, but the header has ${String(names.length)} columns.`,
      };
    }
    rows.push(Object.fromEntries(names.map((name, index) => [name, values[index]])));
  }
  return { rows };
}

/**
 * Reads a YAML data file: a list of mappings.
 *
 * @param text - The file's text.
 * @returns The rows, keeping their YAML types, or the first problem found.
 */
export function parseYamlRows(text: string): DataRows {
  let data: unknown;
  try {
    data = parse(text);
  } catch (error) {
    return {
      error: `The YAML is not valid: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
  if (!Array.isArray(data) || data.length === 0 || !data.every(isMapping)) {
    return { error: 'A YAML data file must be a list of at least one mapping, one per row.' };
  }
  return { rows: data };
}

/**
 * The data rows of a test.
 *
 * @param data - The test's `data`: inline rows, or a path relative to the test file.
 * @param testFile - The test file's project-relative path.
 * @param readText - Reads a project-relative file; `undefined` when it is missing.
 * @returns The rows (one empty row when the test has no `data`), or why they
 *   cannot be read.
 */
export function readDataRows(
  data: string | readonly Record<string, unknown>[] | undefined,
  testFile: string,
  readText: (file: string) => string | undefined,
): DataRows {
  if (data === undefined) return { rows: [{}] };
  if (typeof data !== 'string') return { rows: data };
  const file = dataFilePath(testFile, data);
  const text = readText(file);
  if (text === undefined)
    return { error: `The data file "${data}" does not exist (looked for ${file}).` };
  return /\.csv$/i.test(file) ? parseCsvRows(text) : parseYamlRows(text);
}
