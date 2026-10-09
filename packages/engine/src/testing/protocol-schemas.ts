// Test helper: checks messages against the committed JSON Schema files in
// packages/protocol/schema/, the files clients generate code from, rather than
// against the Zod schemas they were generated from. Zod reads the files with
// z.fromJSONSchema, so no validator dependency is needed. Not part of the
// engine build.

import { readFileSync } from 'node:fs';
import { z } from 'zod';

const SCHEMA_DIR = new URL('../../../protocol/schema/', import.meta.url);
const cache = new Map<string, z.ZodType>();

/**
 * The schema of one committed JSON Schema file.
 *
 * @param name - The file name without `.json`, such as `event.stepPassed`.
 * @returns A Zod schema read from the file.
 */
export function jsonSchema(name: string): z.ZodType {
  let schema = cache.get(name);
  if (schema === undefined) {
    const text = readFileSync(new URL(`${name}.json`, SCHEMA_DIR), 'utf8');
    schema = z.fromJSONSchema(JSON.parse(text) as Parameters<typeof z.fromJSONSchema>[0]);
    cache.set(name, schema);
  }
  return schema;
}

function problems(name: string, value: unknown): string[] {
  const result = jsonSchema(name).safeParse(value);
  return result.success ? [] : [`${name}: ${result.error.message}`];
}

/**
 * Checks one message from the engine against its JSON Schema files: the
 * envelope, then an event's params, a response's result for `method`, or an
 * error's data.
 *
 * @param message - A parsed line from the engine's stdout.
 * @param method - For a response, the method of the request it answers.
 * @returns Every problem found; empty when the message is valid.
 */
export function checkMessage(message: Record<string, unknown>, method?: string): string[] {
  if (typeof message.method === 'string' && !('id' in message)) {
    return [
      ...problems('envelope.notification', message),
      ...problems(`event.${message.method}`, message.params),
    ];
  }
  if ('error' in message) {
    const data = (message.error as { data?: { name?: string } } | undefined)?.data;
    return [
      ...problems('envelope.error-response', message),
      ...(data?.name === undefined ? [] : problems('error-data.ErrorData', data)),
    ];
  }
  return [
    ...problems('envelope.success-response', message),
    ...(method === undefined ? [] : problems(`request.${method}.result`, message.result)),
  ];
}
