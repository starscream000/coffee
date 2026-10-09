// Public entry point of the engine protocol package: message and event types
// shared by the engine and every client, and the product identity. Message
// types are added in Milestone 1 once docs/protocol.md is approved.

export { PRODUCT, PRODUCT_ID, PRODUCT_NAME } from './product.js';

/**
 * Version of the engine protocol described in `docs/protocol.md`.
 *
 * Clients send the version they support in `initialize`; the engine refuses a
 * client whose version is incompatible. `0.0.0` means "not yet released".
 *
 * @example
 * ```ts
 * import { PROTOCOL_VERSION } from '@test-tool/protocol';
 * console.log(`speaking protocol ${PROTOCOL_VERSION}`);
 * ```
 */
export const PROTOCOL_VERSION = '0.0.0';
