// Review 0004, finding 1: masking must never damage a protocol message. For
// every example message of docs/protocol.md, and for a secret taken in turn
// from every key and every string value in it, the line the writer produces
// parses, validates against its schema, keeps every protected field unchanged,
// and shows the secret in no masked field.
import { readFileSync, readdirSync } from 'node:fs';
import { maskRuleFor, protocolSchemas, type MaskRule } from '@cfe/protocol';
import { describe, expect, it } from 'vitest';
import type { z } from 'zod';
import { MIN_SECRET_LENGTH, SecretRegistry } from '../context/mask.js';
import { MessageWriter, type LineSink } from './message-writer.js';

const fixtureDir = new URL('../../../protocol/test/fixtures/protocol-examples/', import.meta.url);
const schemas = protocolSchemas();

class NullSink implements LineSink {
  write(): boolean {
    return true;
  }
}

function schemaFor(key: string): z.ZodType {
  const schema = schemas.get(key);
  if (schema === undefined) throw new Error(`No protocol schema named "${key}".`);
  return schema;
}

type Json = Record<string, unknown>;

/** An example, wrapped in the message it travels in, with the schemas it must match. */
interface Example {
  name: string;
  message: Json;
  /** Each schema with a function that picks the part of the message it checks. */
  checks: [string, (message: Json) => unknown][];
}

function loadExample(file: string): Example {
  const key = file.replace(/\.json$/, '').split('--')[0] ?? '';
  const value = JSON.parse(readFileSync(new URL(file, fixtureDir), 'utf8')) as Json;
  const parts = /^request\.(\w+)\.(params|result)$/.exec(key);
  if (parts !== null) {
    const [, method = '', part] = parts;
    return part === 'params'
      ? {
          name: file,
          message: { jsonrpc: '2.0', id: 1, method, params: value },
          checks: [
            ['envelope.request', (m) => m],
            [key, (m) => m.params],
          ],
        }
      : {
          name: file,
          message: { jsonrpc: '2.0', id: 1, result: value },
          checks: [
            ['envelope.success-response', (m) => m],
            [key, (m) => m.result],
          ],
        };
  }
  if (key === 'envelope.notification') {
    return {
      name: file,
      message: value,
      checks: [
        [key, (m) => m],
        [`event.${String(value.method)}`, (m) => m.params],
      ],
    };
  }
  if (key === 'envelope.error-response') {
    const data = (value.error as Json).data as Json;
    return {
      name: file,
      message: value,
      checks: [
        [key, (m) => m],
        [`error-data.${String(data.name)}`, (m) => (m.error as Json).data],
      ],
    };
  }
  throw new Error(`Unknown example kind "${key}".`);
}

/** Every key and every string value in a value. */
function keysAndStrings(value: unknown, found: Set<string>): Set<string> {
  if (typeof value === 'string') {
    found.add(value);
  } else if (Array.isArray(value)) {
    for (const item of value) keysAndStrings(item, found);
  } else if (typeof value === 'object' && value !== null) {
    for (const [key, item] of Object.entries(value)) {
      found.add(key);
      keysAndStrings(item, found);
    }
  }
  return found;
}

type Path = (string | number)[];

/**
 * Walks a message as the masking rules do and reports each field that is kept
 * (protected) and each string that is masked, by path.
 */
function classify(message: Json): { kept: Path[]; masked: Path[] } {
  const kept: Path[] = [];
  const masked: Path[] = [];
  const strings = (value: unknown, path: Path): void => {
    if (typeof value === 'string') {
      masked.push(path);
    } else if (Array.isArray(value)) {
      for (const [i, item] of value.entries()) strings(item, [...path, i]);
    } else if (typeof value === 'object' && value !== null) {
      for (const [key, item] of Object.entries(value)) strings(item, [...path, key]);
    }
  };
  const visit = (value: unknown, rule: MaskRule, name: string, path: Path): void => {
    if (rule === 'keep') {
      kept.push(path);
    } else if (rule === 'mask') {
      strings(value, path);
    } else if (Array.isArray(value)) {
      for (const [i, item] of value.entries()) visit(item, rule, name, [...path, i]);
    } else if (typeof value === 'object' && value !== null) {
      for (const [key, item] of Object.entries(value)) {
        visit(item, maskRuleFor(key, name), key, [...path, key]);
      }
    } else {
      strings(value, path);
    }
  };
  for (const [key, item] of Object.entries(message)) {
    visit(item, maskRuleFor(key, 'envelope'), key, [key]);
  }
  return { kept, masked };
}

function at(value: unknown, path: Path): unknown {
  let current = value;
  for (const step of path) {
    current = (current as Record<string | number, unknown> | undefined)?.[step];
  }
  return current;
}

const examples = readdirSync(fixtureDir)
  .filter((file) => file.endsWith('.json'))
  .map(loadExample);

describe('masking keeps every protocol example message intact', () => {
  it('has examples to check', () => {
    expect(examples.length).toBeGreaterThanOrEqual(16);
  });

  it.each(examples)('$name, with a secret taken from each key and string', (example) => {
    const { kept, masked } = classify(example.message);
    const secrets = [...keysAndStrings(example.message, new Set())].filter(
      (text) => text.length >= MIN_SECRET_LENGTH,
    );
    expect(secrets.length).toBeGreaterThan(0);
    for (const secret of secrets) {
      const registry = new SecretRegistry();
      registry.register(secret);
      const writer = new MessageWriter(new NullSink(), { mask: (text) => registry.mask(text) });
      const line = writer.render(example.message);
      const what = `${example.name} with secret ${JSON.stringify(secret)}`;

      const parsed = JSON.parse(line) as Json;
      for (const [schema, pick] of example.checks) {
        const result = schemaFor(schema).safeParse(pick(parsed));
        expect(result.success, `${what}: ${schema}: ${result.error?.message ?? ''}`).toBe(true);
      }
      for (const path of kept) {
        expect(at(parsed, path), `${what}: ${path.join('.')}`).toEqual(at(example.message, path));
      }
      for (const path of masked) {
        expect(String(at(parsed, path)), `${what}: ${path.join('.')}`).not.toContain(secret);
      }
    }
  });
});
