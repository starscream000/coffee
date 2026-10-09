// Checks that every example message printed in docs/protocol.md (kept as JSON
// fixtures in test/fixtures/protocol-examples/) validates against its schema.
import { readFileSync, readdirSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { z } from 'zod';
import { protocolSchemas } from './schema-files.js';

const fixtureDir = new URL('../test/fixtures/protocol-examples/', import.meta.url);
const schemas = protocolSchemas();
const fixtures = readdirSync(fixtureDir).filter((name) => name.endsWith('.json'));

const EnvelopeWithData = z.looseObject({ error: z.looseObject({ data: z.unknown() }) });
const DataWithName = z.looseObject({ name: z.string() });
const NotificationShape = z.looseObject({ method: z.string(), params: z.unknown() });

function schemaFor(key: string): z.ZodType {
  const schema = schemas.get(key);
  if (schema === undefined) {
    throw new Error(`No protocol schema named "${key}".`);
  }
  return schema;
}

function expectValid(schema: z.ZodType, value: unknown, what: string): void {
  const result = schema.safeParse(value);
  expect(result.success, `${what}: ${result.error?.message ?? ''}`).toBe(true);
}

describe('examples from docs/protocol.md', () => {
  it('cover every message kind the document shows', () => {
    expect(fixtures.length).toBeGreaterThanOrEqual(16);
  });

  it.each(fixtures)('%s validates', (name) => {
    const key = name.replace(/\.json$/, '').split('--')[0] ?? '';
    const value: unknown = JSON.parse(readFileSync(new URL(name, fixtureDir), 'utf8'));
    expectValid(schemaFor(key), value, key);

    if (key === 'envelope.error-response') {
      const data = EnvelopeWithData.parse(value).error.data;
      const dataName = DataWithName.parse(data).name;
      expectValid(schemaFor(`error-data.${dataName}`), data, `error-data.${dataName}`);
    }
    if (key === 'envelope.notification') {
      const { method, params } = NotificationShape.parse(value);
      expectValid(schemaFor(`event.${method}`), params, `event.${method}`);
    }
  });

  it('rejects a message with a wrong field type', () => {
    const result = schemaFor('request.initialize.params').safeParse({
      protocolVersion: 1,
      client: { name: 'x', version: '1' },
    });
    expect(result.success).toBe(false);
  });
});
