// Unit tests for the protocol package entry point.
import { describe, expect, it } from 'vitest';
import { PROTOCOL_VERSION } from './index.js';

describe('PROTOCOL_VERSION', () => {
  it('is a semantic version string', () => {
    expect(PROTOCOL_VERSION).toMatch(/^\d+\.\d+\.\d+$/);
  });
});
