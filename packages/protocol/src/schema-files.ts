// Builds the JSON Schema files in packages/protocol/schema/ from the Zod
// schemas (ADR 0020). The generator script writes them; a unit test checks the
// committed files still equal what this module produces.

import { z } from 'zod';
import { EVENTS, EVENT_NAMES } from './events.js';
import {
  ErrorDataSchema,
  IncompatibleProtocolDataSchema,
  JsonRpcErrorResponseSchema,
  JsonRpcNotificationSchema,
  JsonRpcRequestSchema,
  JsonRpcSuccessResponseSchema,
  SnapshotUnavailableDataSchema,
  StepFilesInvalidDataSchema,
} from './jsonrpc.js';
import { REQUESTS, REQUEST_METHODS } from './requests.js';
import {
  DiagnosticSchema,
  ErrorInfoSchema,
  LocationSchema,
  LocatorUseSchema,
  SnapshotStatusSchema,
} from './shared.js';
import { PROTOCOL_VERSION } from './version.js';

/**
 * Every protocol schema under the key its JSON Schema file is named after, for
 * example `request.initialize.params` or `event.stepPassed`.
 *
 * @returns A new map from key to Zod schema, in a stable order.
 *
 * @example
 * ```ts
 * protocolSchemas().get('event.stepFailed')?.parse(notification.params);
 * ```
 */
export function protocolSchemas(): Map<string, z.ZodType> {
  const schemas = new Map<string, z.ZodType>([
    ['envelope.request', JsonRpcRequestSchema],
    ['envelope.notification', JsonRpcNotificationSchema],
    ['envelope.success-response', JsonRpcSuccessResponseSchema],
    ['envelope.error-response', JsonRpcErrorResponseSchema],
    ['error-data.ErrorData', ErrorDataSchema],
    ['error-data.IncompatibleProtocol', IncompatibleProtocolDataSchema],
    ['error-data.StepFilesInvalid', StepFilesInvalidDataSchema],
    ['error-data.SnapshotUnavailable', SnapshotUnavailableDataSchema],
    ['type.Location', LocationSchema],
    ['type.Diagnostic', DiagnosticSchema],
    ['type.LocatorUse', LocatorUseSchema],
    ['type.SnapshotStatus', SnapshotStatusSchema],
    ['type.ErrorInfo', ErrorInfoSchema],
  ]);
  for (const method of REQUEST_METHODS) {
    schemas.set(`request.${method}.params`, REQUESTS[method].params);
    schemas.set(`request.${method}.result`, REQUESTS[method].result);
  }
  for (const name of EVENT_NAMES) {
    schemas.set(`event.${name}`, EVENTS[name]);
  }
  return schemas;
}

/**
 * Renders every protocol schema as the text of its JSON Schema file.
 *
 * @returns A map from file name (`<key>.json`) to file contents: JSON with
 *   two-space indentation and a final newline, each with a `title` naming the
 *   key and the protocol version.
 *
 * @example
 * ```ts
 * for (const [name, text] of schemaFileContents()) {
 *   writeFileSync(join(outDir, name), text);
 * }
 * ```
 */
export function schemaFileContents(): Map<string, string> {
  const files = new Map<string, string>();
  for (const [key, schema] of protocolSchemas()) {
    const jsonSchema = z.toJSONSchema(schema, { target: 'draft-2020-12' });
    const document = { title: `${key} (protocol ${PROTOCOL_VERSION})`, ...jsonSchema };
    files.set(`${key}.json`, `${JSON.stringify(document, null, 2)}\n`);
  }
  return files;
}
