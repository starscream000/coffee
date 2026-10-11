// Unit tests for the protocol version and the compatibility rule.
import { describe, expect, it } from 'vitest';
import { PROTOCOL_VERSION, isCompatibleProtocol, parseProtocolVersion } from './version.js';

describe('PROTOCOL_VERSION', () => {
  it('is 0.1.1: recording and createProject were added compatibly (ADR 0011)', () => {
    expect(PROTOCOL_VERSION).toBe('0.1.1');
  });
});

describe('parseProtocolVersion', () => {
  it.each([
    ['0.1.0', { major: 0, minor: 1, patch: 0 }],
    ['12.34.56', { major: 12, minor: 34, patch: 56 }],
  ])('splits %s', (version, parts) => {
    expect(parseProtocolVersion(version)).toEqual(parts);
  });

  it.each(['', '1', '1.2', '1.2.3.4', '01.2.3', '1.2.3-beta', 'v1.2.3', ' 1.2.3', '1.x.0'])(
    'rejects %j',
    (version) => {
      expect(parseProtocolVersion(version)).toBeUndefined();
    },
  );
});

describe('isCompatibleProtocol', () => {
  it.each([
    // 0.x: major and minor must match; patch may differ.
    ['0.1.0', '0.1.0', true],
    ['0.1.9', '0.1.0', true],
    ['0.1.0', '0.1.9', true],
    ['0.2.0', '0.1.0', false],
    ['0.1.0', '0.2.0', false],
    ['0.0.1', '0.0.2', true],
    // From 1.0.0: major must match; minor and patch may differ.
    ['1.0.0', '1.0.0', true],
    ['1.4.0', '1.0.0', true],
    ['1.0.0', '1.9.3', true],
    ['2.0.0', '1.0.0', false],
    ['1.0.0', '2.0.0', false],
    // Across the 0.x / 1.x line.
    ['0.9.0', '1.0.0', false],
    ['1.0.0', '0.9.0', false],
    // Malformed versions are never compatible.
    ['0.1', '0.1.0', false],
    ['0.1.0', 'garbage', false],
    ['', '', false],
  ])('client %j with engine %j → %s', (client, engine, expected) => {
    expect(isCompatibleProtocol(client, engine)).toBe(expected);
  });
});
