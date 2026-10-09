// A protocol error a request handler can throw; the session turns it into a
// JSON-RPC error response with `data.name` (docs/protocol.md, "Error codes").

import { ERROR_CODES, type ErrorName } from '@cfe/protocol';

/**
 * An error that becomes a JSON-RPC error response. The message is shown to the
 * user, so it says what went wrong and what to do.
 *
 * @example
 * ```ts
 * throw new RpcError('ProjectNotOpen', 'Open a project with "openProject" before validating.');
 * ```
 */
export class RpcError extends Error {
  /** The protocol error name, also sent as `error.data.name`. */
  readonly errorName: ErrorName;
  /** Extra fields for `error.data`, next to `name`. */
  readonly data: Readonly<Record<string, unknown>>;

  /**
   * @param errorName - One of the protocol's error names.
   * @param message - What went wrong and what to do, for the user.
   * @param data - Extra fields for `error.data`.
   */
  constructor(errorName: ErrorName, message: string, data: Record<string, unknown> = {}) {
    super(message);
    this.name = 'RpcError';
    this.errorName = errorName;
    this.data = data;
  }

  /** The numeric JSON-RPC code of {@link RpcError.errorName}. */
  get code(): number {
    return ERROR_CODES[this.errorName];
  }

  /**
   * The `error` member of the response.
   *
   * @returns Code, message and data with the error's name.
   */
  toJson(): { code: number; message: string; data: Record<string, unknown> } {
    return { code: this.code, message: this.message, data: { ...this.data, name: this.errorName } };
  }
}
