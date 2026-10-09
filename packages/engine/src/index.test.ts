// Unit tests for the engine package entry point.
import { PROTOCOL_VERSION } from '@cfe/protocol';
import { describe, expect, it } from 'vitest';
import { getEngineInfo } from './index.js';

describe('getEngineInfo', () => {
  it('reports the protocol version from the protocol package', () => {
    expect(getEngineInfo()).toEqual({
      name: '@cfe/engine',
      protocolVersion: PROTOCOL_VERSION,
    });
  });
});
