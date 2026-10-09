// Public entry point of the engine protocol package: every message as a Zod
// schema with its inferred type (ADR 0020), the version rule, the size limits,
// the error codes and the product identity. Shared by the engine and clients.

export { PRODUCT } from './product.js';
export {
  PROTOCOL_VERSION,
  isCompatibleProtocol,
  parseProtocolVersion,
  type ProtocolVersionParts,
} from './version.js';
export { MAX_MESSAGE_BYTES, TRUNCATED_FIELD_BYTES, truncationMarker } from './limits.js';
export { ERROR_CODES, ERROR_NAMES, errorNameOf, type ErrorCode, type ErrorName } from './errors.js';
export * from './shared.js';
export * from './jsonrpc.js';
export * from './requests.js';
export * from './events.js';
export { protocolSchemas, schemaFileContents } from './schema-files.js';
export { MASK_RULES, maskMessage, maskRuleFor, type MaskRule } from './masking.js';
