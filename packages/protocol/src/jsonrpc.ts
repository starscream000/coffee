// JSON-RPC 2.0 envelopes that every protocol message travels in
// (docs/protocol.md, "Transport"), and the `data` objects of error responses.

import { z } from 'zod';
import { DiagnosticSchema } from './shared.js';

/**
 * A request id: a string or an integer, chosen by the client.
 */
export const JsonRpcIdSchema = z.union([z.string(), z.number().int()]);

/**
 * A request id. See {@link JsonRpcIdSchema}.
 */
export type JsonRpcId = z.infer<typeof JsonRpcIdSchema>;

/**
 * A request from the client. `params` is checked against the method's own
 * schema once the method is known.
 */
export const JsonRpcRequestSchema = z.looseObject({
  jsonrpc: z.literal('2.0'),
  id: JsonRpcIdSchema,
  method: z.string(),
  params: z.unknown().optional(),
});

/**
 * A request envelope. See {@link JsonRpcRequestSchema}.
 */
export type JsonRpcRequest = z.infer<typeof JsonRpcRequestSchema>;

/**
 * A notification: a message without an id. The engine sends every event as a
 * notification.
 */
export const JsonRpcNotificationSchema = z.looseObject({
  jsonrpc: z.literal('2.0'),
  method: z.string(),
  params: z.unknown().optional(),
});

/**
 * A notification envelope. See {@link JsonRpcNotificationSchema}.
 */
export type JsonRpcNotification = z.infer<typeof JsonRpcNotificationSchema>;

/**
 * A successful response to a request.
 */
export const JsonRpcSuccessResponseSchema = z.looseObject({
  jsonrpc: z.literal('2.0'),
  id: JsonRpcIdSchema,
  result: z.unknown(),
});

/**
 * A success envelope. See {@link JsonRpcSuccessResponseSchema}.
 */
export type JsonRpcSuccessResponse = z.infer<typeof JsonRpcSuccessResponseSchema>;

/**
 * The `error` member of an error response.
 */
export const JsonRpcErrorSchema = z.looseObject({
  code: z.number().int(),
  message: z.string(),
  data: z.unknown().optional(),
});

/**
 * An error object. See {@link JsonRpcErrorSchema}.
 */
export type JsonRpcError = z.infer<typeof JsonRpcErrorSchema>;

/**
 * An error response. `id` is `null` when the request's id could not be read
 * (invalid JSON, or a line over the size limit).
 */
export const JsonRpcErrorResponseSchema = z.looseObject({
  jsonrpc: z.literal('2.0'),
  id: JsonRpcIdSchema.nullable(),
  error: JsonRpcErrorSchema,
});

/**
 * An error envelope. See {@link JsonRpcErrorResponseSchema}.
 */
export type JsonRpcErrorResponse = z.infer<typeof JsonRpcErrorResponseSchema>;

/**
 * The `data` every protocol error response carries: at least the error's name.
 */
export const ErrorDataSchema = z.looseObject({
  name: z.string(),
});

/**
 * Common error data. See {@link ErrorDataSchema}.
 */
export type ErrorData = z.infer<typeof ErrorDataSchema>;

/**
 * `data` of `IncompatibleProtocol`: both versions, so the user knows which side
 * to update.
 */
export const IncompatibleProtocolDataSchema = ErrorDataSchema.extend({
  name: z.literal('IncompatibleProtocol'),
  clientProtocolVersion: z.string(),
  engineProtocolVersion: z.string(),
  engineVersion: z.string(),
});

/**
 * Data of `IncompatibleProtocol`. See {@link IncompatibleProtocolDataSchema}.
 */
export type IncompatibleProtocolData = z.infer<typeof IncompatibleProtocolDataSchema>;

/**
 * `data` of `StepFilesInvalid`: every problem found, so nothing runs.
 */
export const StepFilesInvalidDataSchema = ErrorDataSchema.extend({
  name: z.literal('StepFilesInvalid'),
  diagnostics: z.array(DiagnosticSchema),
});

/**
 * Data of `StepFilesInvalid`. See {@link StepFilesInvalidDataSchema}.
 */
export type StepFilesInvalidData = z.infer<typeof StepFilesInvalidDataSchema>;

/**
 * `data` of `SnapshotUnavailable`: the step's screenshot, to show instead.
 */
export const SnapshotUnavailableDataSchema = ErrorDataSchema.extend({
  name: z.literal('SnapshotUnavailable'),
  screenshot: z.string(),
});

/**
 * Data of `SnapshotUnavailable`. See {@link SnapshotUnavailableDataSchema}.
 */
export type SnapshotUnavailableData = z.infer<typeof SnapshotUnavailableDataSchema>;
