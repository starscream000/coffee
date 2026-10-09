// Data rows of a test (docs/step-format.md, "Data rows"; ADR 0013): inline
// rows, or a CSV or YAML file relative to the test file. CSV is read as
// spreadsheets write it (RFC 4180, owner decision of 2026-10-10): a value may
// be wrapped in double quotes, inside which a comma or a line break is part of
// the value and a quote is written twice. Unquoted values are trimmed, quoted
// values are kept exactly, blank lines are skipped, and lines may end in LF or
// CRLF. No dependency.

import { parse } from 'yaml';

/** One data row: column (or key) names to values. */
export type DataRow = Readonly<Record<string, unknown>>;

/** The rows of a test, or why they cannot be read (with the line, when known). */
export type DataRows =
  | { readonly rows: readonly DataRow[]; readonly error?: undefined; readonly line?: undefined }
  | { readonly rows?: undefined; readonly error: string; readonly line?: number | undefined };

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

/** One CSV record: its values and the line it starts on. */
interface CsvRecord {
  readonly values: string[];
  readonly line: number;
}

/** Splits CSV text into records, or reports the first broken rule with its line. */
function csvRecords(text: string): { records: CsvRecord[] } | { error: string; line: number } {
  const records: CsvRecord[] = [];
  let line = 1;
  let index = 0;
  const atEnd = (): boolean => index >= text.length;
  const atLineEnd = (): boolean => text[index] === '\n' || text.startsWith('\r\n', index);
  const skipLineEnd = (): void => {
    index += text[index] === '\r' ? 2 : 1;
    line += 1;
  };

  while (!atEnd()) {
    const start = line;
    const values: string[] = [];
    let blank = true;
    for (;;) {
      // Spaces and tabs around a value are not part of it.
      while (text[index] === ' ' || text[index] === '\t') index += 1;
      let value: string;
      if (text[index] === '"') {
        blank = false;
        const opened = line;
        index += 1;
        value = '';
        for (;;) {
          if (atEnd()) {
            return {
              error: `The quote opened on line ${String(opened)} is never closed.`,
              line: opened,
            };
          }
          const char = text[index] ?? '';
          if (char === '"') {
            if (text[index + 1] === '"') {
              value += '"';
              index += 2;
              continue;
            }
            index += 1;
            break;
          }
          if (char === '\n') line += 1;
          value += char;
          index += 1;
        }
        while (text[index] === ' ' || text[index] === '\t') index += 1;
        if (!atEnd() && text[index] !== ',' && !atLineEnd()) {
          return {
            error: `Line ${String(line)} has text after a closing quote; a quote inside a quoted value is written twice ("").`,
            line,
          };
        }
      } else {
        let end = index;
        while (
          end < text.length &&
          text[end] !== ',' &&
          text[end] !== '\n' &&
          !text.startsWith('\r\n', end)
        ) {
          end += 1;
        }
        value = text.slice(index, end).trim();
        if (value.includes('"')) {
          return {
            error: `Line ${String(line)} has a double quote inside a value that does not start with one; wrap the value in quotes and write the quote twice ("").`,
            line,
          };
        }
        if (value !== '') blank = false;
        index = end;
      }
      values.push(value);
      if (text[index] === ',') {
        blank = false;
        index += 1;
        continue;
      }
      break;
    }
    if (!atEnd()) skipLineEnd();
    if (!blank) records.push({ values, line: start });
  }
  return { records };
}

/**
 * Reads a CSV data file (RFC 4180): the first record is the header, each
 * other record is one row, and values are strings.
 *
 * @param text - The file's text.
 * @returns The rows, each a mapping from header names to values, or the first
 *   problem found with its line.
 *
 * @example
 * ```ts
 * parseCsvRows('name,city\n"Smith, J.",Paris\n'); // { rows: [{ name: 'Smith, J.', city: 'Paris' }] }
 * ```
 */
export function parseCsvRows(text: string): DataRows {
  const parsed = csvRecords(text);
  if ('error' in parsed) return parsed;
  const [header, ...body] = parsed.records;
  if (header === undefined) {
    return { error: 'The CSV file is empty; its first line must be the header.', line: 1 };
  }
  const names = header.values;
  const empty = names.findIndex((name) => name === '');
  if (empty !== -1) {
    return { error: `Column ${String(empty + 1)} of the header has no name.`, line: header.line };
  }
  const duplicate = names.find((name, index) => names.indexOf(name) !== index);
  if (duplicate !== undefined) {
    return { error: `The header names the column "${duplicate}" twice.`, line: header.line };
  }
  if (body.length === 0) {
    return { error: 'The CSV file has a header but no rows.', line: header.line };
  }
  const rows: DataRow[] = [];
  for (const record of body) {
    if (record.values.length !== names.length) {
      return {
        error: `Line ${String(record.line)} has ${String(record.values.length)} values, but the header has ${String(names.length)} columns.`,
        line: record.line,
      };
    }
    rows.push(Object.fromEntries(names.map((name, index) => [name, record.values[index]])));
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
  if (text === undefined) {
    return { error: `The data file "${data}" does not exist (looked for ${file}).` };
  }
  return /\.csv$/i.test(file) ? parseCsvRows(text) : parseYamlRows(text);
}
