// The protocol's error codes and names, as one table (docs/protocol.md, "Error
// codes"). Error responses carry the name in `error.data.name`.

/**
 * Every error code the protocol uses, with its name. The JSON-RPC standard
 * codes come first, then the protocol's own.
 *
 * @example
 * ```ts
 * const code = ERROR_CODES.NotInitialized; // -32001
 * ```
 */
export const ERROR_CODES = {
  ParseError: -32700,
  InvalidRequest: -32600,
  MethodNotFound: -32601,
  InvalidParams: -32602,
  InternalError: -32603,
  NotInitialized: -32001,
  IncompatibleProtocol: -32002,
  ProjectNotOpen: -32003,
  ProjectInvalid: -32004,
  StepFilesInvalid: -32005,
  RunInProgress: -32006,
  RunNotFound: -32007,
  SnapshotNotFound: -32008,
  MessageTooLarge: -32009,
  SnapshotUnavailable: -32010,
  RecordingInProgress: -32011,
  RecordingNotFound: -32012,
  FileExists: -32013,
  FolderNotEmpty: -32014,
} as const;

/**
 * Name of a protocol error, such as `"NotInitialized"`.
 */
export type ErrorName = keyof typeof ERROR_CODES;

/**
 * Numeric code of a protocol error, such as `-32001`.
 */
export type ErrorCode = (typeof ERROR_CODES)[ErrorName];

/**
 * Every error name, in table order.
 */
export const ERROR_NAMES = Object.keys(ERROR_CODES) as readonly ErrorName[];

/**
 * Finds the name that belongs to an error code.
 *
 * @param code - A numeric error code received in an error response.
 * @returns The error's name, or `undefined` for a code the protocol does not
 *   define (clients must accept unknown codes from newer engines).
 *
 * @example
 * ```ts
 * errorNameOf(-32002); // "IncompatibleProtocol"
 * ```
 */
export function errorNameOf(code: number): ErrorName | undefined {
  return ERROR_NAMES.find((name) => ERROR_CODES[name] === code);
}
