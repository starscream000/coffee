// Unit tests for the error code table.
import { describe, expect, it } from 'vitest';
import { ERROR_CODES, ERROR_NAMES, errorNameOf } from './errors.js';

describe('ERROR_CODES', () => {
  it('uses every code once', () => {
    const codes = Object.values(ERROR_CODES);
    expect(new Set(codes).size).toBe(codes.length);
  });

  it('uses every name once', () => {
    expect(new Set(ERROR_NAMES).size).toBe(ERROR_NAMES.length);
  });

  it('holds the standard JSON-RPC codes and the protocol codes of docs/protocol.md', () => {
    expect(ERROR_CODES).toEqual({
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
    });
  });
});

describe('errorNameOf', () => {
  it('finds the name of a known code', () => {
    expect(errorNameOf(-32002)).toBe('IncompatibleProtocol');
    expect(errorNameOf(-32601)).toBe('MethodNotFound');
  });

  it('returns undefined for an unknown code', () => {
    expect(errorNameOf(-32099)).toBeUndefined();
  });
});
