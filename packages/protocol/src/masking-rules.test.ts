// Every field of every protocol message must have a masking rule, so a new
// field cannot be masked or kept by accident (review 0004, finding 1).
import { describe, expect, it } from 'vitest';
import { z } from 'zod';
import { MASK_RULES, maskMessage, maskRuleFor } from './masking.js';
import { protocolSchemas } from './schema-files.js';

function fieldNames(node: unknown, found: Set<string>): Set<string> {
  if (Array.isArray(node)) {
    for (const item of node) fieldNames(item, found);
  } else if (typeof node === 'object' && node !== null) {
    const properties = (node as { properties?: Record<string, unknown> }).properties;
    if (properties !== undefined) {
      for (const name of Object.keys(properties)) found.add(name);
    }
    for (const value of Object.values(node)) fieldNames(value, found);
  }
  return found;
}

describe('MASK_RULES', () => {
  it('has a rule for every field of every protocol message', () => {
    const names = new Set<string>();
    for (const schema of protocolSchemas().values()) {
      fieldNames(z.toJSONSchema(schema), names);
    }
    const ruled = new Set(Object.keys(MASK_RULES).map((key) => key.split('.').at(-1)));
    const missing = [...names].filter((name) => !ruled.has(name)).sort();
    expect(missing, 'Add these fields to MASK_RULES in packages/protocol/src/masking.ts').toEqual(
      [],
    );
  });

  it('prefers a parent.field rule over the plain field rule', () => {
    expect(maskRuleFor('data', 'error')).toBe('walk');
    expect(maskRuleFor('data', 'params')).toBe('walk');
    expect(maskRuleFor('data')).toBe('mask');
    expect(maskRuleFor('reason', 'snapshot')).toBe('mask');
    expect(maskRuleFor('reason', 'params')).toBe('keep');
    expect(maskRuleFor('somethingNew')).toBe('mask');
  });
});

describe('maskMessage', () => {
  const mask = (text: string) => text.split('hunter22').join('•••');

  it('masks free text, keeps identifiers and never touches keys', () => {
    const message = {
      jsonrpc: '2.0',
      method: 'log',
      params: {
        runId: 'hunter22-run',
        seq: 1,
        level: 'warn',
        message: 'saw hunter22',
        data: { hunter22: 'hunter22' },
      },
    };
    expect(maskMessage(message, mask)).toEqual({
      jsonrpc: '2.0',
      method: 'log',
      params: {
        runId: 'hunter22-run',
        seq: 1,
        level: 'warn',
        message: 'saw •••',
        data: { hunter22: '•••' },
      },
    });
  });

  it("walks an error's data and masks a step's params", () => {
    expect(
      maskMessage(
        {
          jsonrpc: '2.0',
          id: 1,
          error: {
            code: -32005,
            message: 'hunter22',
            data: {
              name: 'StepFilesInvalid',
              diagnostics: [{ file: 'hunter22.test.yaml', message: 'hunter22' }],
            },
          },
        },
        mask,
      ),
    ).toEqual({
      jsonrpc: '2.0',
      id: 1,
      error: {
        code: -32005,
        message: '•••',
        data: {
          name: 'StepFilesInvalid',
          diagnostics: [{ file: 'hunter22.test.yaml', message: '•••' }],
        },
      },
    });
  });

  // Review 0005, finding 4.
  it("walks a log event's data: keeps known identifiers, masks every other field", () => {
    const warning = {
      jsonrpc: '2.0',
      method: 'log',
      params: {
        runId: 'r',
        seq: 3,
        level: 'warn',
        code: 'LocatorFallback',
        message: 'Target "hunter22.submit" was found by its candidate 1',
        data: { target: 'hunter22.submit', candidateIndex: 1, note: 'saw hunter22' },
      },
    };
    expect(maskMessage(warning, mask)).toEqual({
      jsonrpc: '2.0',
      method: 'log',
      params: {
        runId: 'r',
        seq: 3,
        level: 'warn',
        code: 'LocatorFallback',
        message: 'Target "•••.submit" was found by its candidate 1',
        data: { target: 'hunter22.submit', candidateIndex: 1, note: 'saw •••' },
      },
    });
  });
});
