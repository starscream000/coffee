// Unit tests for the engine package entry point.
import { PROTOCOL_VERSION } from '@cfe/protocol';
import { describe, expect, it } from 'vitest';
import { getEngineInfo } from './index.js';

describe('getEngineInfo', () => {
  it('reports the package name, its version and the protocol version', () => {
    expect(getEngineInfo()).toEqual({
      name: '@cfe/engine',
      version: '0.0.0',
      protocolVersion: PROTOCOL_VERSION,
    });
  });
});
