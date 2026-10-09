// Checks that the committed JSON Schema files in packages/protocol/schema/ are
// exactly what the generator produces now, so they cannot go stale, and that
// every request and event has its files.
import { readFileSync, readdirSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { EVENT_NAMES } from './events.js';
import { REQUEST_METHODS } from './requests.js';
import { protocolSchemas, schemaFileContents } from './schema-files.js';

const schemaDir = new URL('../schema/', import.meta.url);
const REGENERATE = 'Run "pnpm generate:schemas" and commit packages/protocol/schema/.';

describe('committed JSON Schema files', () => {
  const generated = schemaFileContents();
  const committed = readdirSync(schemaDir).filter((name) => name.endsWith('.json'));

  it('are exactly the files the generator produces', () => {
    expect([...committed].sort(), REGENERATE).toEqual([...generated.keys()].sort());
  });

  it.each([...generated.entries()])('%s matches the generator', (name, text) => {
    const onDisk = readFileSync(new URL(name, schemaDir), 'utf8').replace(/\r\n/g, '\n');
    expect(onDisk, REGENERATE).toBe(text);
  });
});

describe('protocolSchemas', () => {
  const keys = [...protocolSchemas().keys()];

  it('has params and result for every request', () => {
    for (const method of REQUEST_METHODS) {
      expect(keys).toContain(`request.${method}.params`);
      expect(keys).toContain(`request.${method}.result`);
    }
  });

  it('has every event', () => {
    for (const name of EVENT_NAMES) {
      expect(keys).toContain(`event.${name}`);
    }
  });

  it('accepts unknown fields, as the protocol requires', () => {
    const schema = protocolSchemas().get('request.initialize.params');
    const result = schema?.safeParse({
      protocolVersion: '0.1.0',
      client: { name: 'x', version: '1.0.0', newField: true },
      newTopLevelField: 1,
    });
    expect(result?.success).toBe(true);
  });
});
