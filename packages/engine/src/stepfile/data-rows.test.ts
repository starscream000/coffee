// Unit tests for data rows: inline rows, CSV read as RFC 4180, and YAML data
// files.
import { describe, expect, it } from 'vitest';
import { dataFilePath, parseCsvRows, parseYamlRows, readDataRows } from './data-rows.js';

describe('parseCsvRows (RFC 4180)', () => {
  it('reads the header and one row per line, values as strings, trimmed, blank lines skipped', () => {
    expect(parseCsvRows('sku,price\r\ndesk-lamp, 25\n\nchair,120\n')).toEqual({
      rows: [
        { sku: 'desk-lamp', price: '25' },
        { sku: 'chair', price: '120' },
      ],
    });
  });

  it('reads quoted values: commas, line breaks and doubled quotes inside, kept exactly', () => {
    const text = 'name,note,city\r\n"Smith, J.","said ""hi""\nthen left", Paris\n" padded ",,"x"\n';
    expect(parseCsvRows(text)).toEqual({
      rows: [
        { name: 'Smith, J.', note: 'said "hi"\nthen left', city: 'Paris' },
        { name: ' padded ', note: '', city: 'x' },
      ],
    });
  });

  it('reports a broken file with the line where the problem is', () => {
    expect(parseCsvRows('a,b\n1,2\n"open,3\n4,5\n')).toEqual({
      error: 'The quote opened on line 3 is never closed.',
      line: 3,
    });
    expect(parseCsvRows('a,b\n1,"2"x\n')).toMatchObject({ line: 2 });
    expect(parseCsvRows('a,b\nsay "hi",2\n')).toMatchObject({ line: 2 });
    expect(parseCsvRows('a,b\n1,2\n"multi\nline",2,3\n')).toEqual({
      error: 'Line 3 has 3 values, but the header has 2 columns.',
      line: 3,
    });
    expect(parseCsvRows('a,b\n1\n')).toMatchObject({
      error: 'Line 2 has 1 values, but the header has 2 columns.',
    });
    expect(parseCsvRows('a,,c\n1,2,3\n').error).toBe('Column 2 of the header has no name.');
    expect(parseCsvRows('a,a\n1,2\n').error).toBe('The header names the column "a" twice.');
    expect(parseCsvRows('a,b\n').error).toBe('The CSV file has a header but no rows.');
    expect(parseCsvRows('').error).toBe(
      'The CSV file is empty; its first line must be the header.',
    );
  });
});

describe('parseYamlRows', () => {
  it('reads a list of mappings and keeps their types', () => {
    expect(parseYamlRows('- { sku: lamp, qty: 2, gift: true }\n')).toEqual({
      rows: [{ sku: 'lamp', qty: 2, gift: true }],
    });
  });

  it('rejects anything else', () => {
    expect(parseYamlRows('sku: lamp\n').error).toBe(
      'A YAML data file must be a list of at least one mapping, one per row.',
    );
    expect(parseYamlRows('- [unclosed\n').error).toMatch(/^The YAML is not valid/);
  });
});

describe('readDataRows', () => {
  const files: Record<string, string> = {
    'tests/data/products.csv': 'sku\nlamp\nchair\n',
    'tests/rows.yaml': '- { user: alice }\n',
  };
  const read = (file: string): string | undefined => files[file];

  it('gives one empty row to a test without data, and inline rows as they are', () => {
    expect(readDataRows(undefined, 'tests/a.test.yaml', read)).toEqual({ rows: [{}] });
    expect(readDataRows([{ a: 1 }], 'tests/a.test.yaml', read)).toEqual({ rows: [{ a: 1 }] });
  });

  it('reads files relative to the test file', () => {
    expect(dataFilePath('tests/checkout/a.test.yaml', '../data/x.csv')).toBe('tests/data/x.csv');
    expect(readDataRows('./data/products.csv', 'tests/a.test.yaml', read).rows).toHaveLength(2);
    expect(readDataRows('rows.yaml', 'tests/a.test.yaml', read).rows).toEqual([{ user: 'alice' }]);
    expect(readDataRows('missing.csv', 'tests/a.test.yaml', read).error).toBe(
      'The data file "missing.csv" does not exist (looked for tests/missing.csv).',
    );
  });
});
