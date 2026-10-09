// Public entry point of the engine package. Clients never import it: they start
// `dist/main.js --stdio` as a separate process and use the protocol
// (ADR 0009). It exists for the engine's own tests and later for the SDK.

export { getEngineInfo, type EngineInfo } from './engine-info.js';
