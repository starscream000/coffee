// Unit tests for the session: handshake, dispatch, parameter checks and errors.
import { PROTOCOL_VERSION } from '@cfe/protocol';
import { describe, expect, it } from 'vitest';
import type { LineSink } from './message-writer.js';
import { MessageWriter } from './message-writer.js';
import { RpcError } from './rpc-error.js';
import { Session } from './session.js';

class MemorySink implements LineSink {
  readonly lines: Record<string, unknown>[] = [];

  write(text: string, callback?: (error?: Error | null) => void): boolean {
    this.lines.push(JSON.parse(text) as Record<string, unknown>);
    callback?.();
    return true;
  }
}

function setup() {
  const sink = new MemorySink();
  const exits: number[] = [];
  const errors: string[] = [];
  const session = new Session(new MessageWriter(sink), {
    engineInfo: { name: '@cfe/engine', version: '9.9.9', protocolVersion: PROTOCOL_VERSION },
    browsers: [],
    exit: (code) => exits.push(code),
    logError: (message) => errors.push(message),
  });
  const send = (message: unknown) =>
    session.receive({ kind: 'line', text: JSON.stringify(message) });
  const initialize = () =>
    send({
      jsonrpc: '2.0',
      id: 0,
      method: 'initialize',
      params: { protocolVersion: PROTOCOL_VERSION, client: { name: 't', version: '1' } },
    });
  return { sink, exits, errors, session, send, initialize };
}

describe('Session handshake', () => {
  it('answers initialize with the engine identity and capabilities', async () => {
    const { sink, initialize } = setup();
    await initialize();
    expect(sink.lines).toEqual([
      {
        jsonrpc: '2.0',
        id: 0,
        result: {
          protocolVersion: PROTOCOL_VERSION,
          engine: { name: '@cfe/engine', version: '9.9.9' },
          capabilities: { browsers: [] },
        },
      },
    ]);
  });

  it('refuses any request before initialize with NotInitialized', async () => {
    const { sink, send } = setup();
    await send({ jsonrpc: '2.0', id: 1, method: 'openProject', params: { root: '.' } });
    await send({ jsonrpc: '2.0', id: 2, method: 'noSuchMethod' });
    expect(sink.lines.map((line) => line.error)).toEqual([
      expect.objectContaining({ code: -32001, data: { name: 'NotInitialized' } }),
      expect.objectContaining({ code: -32001, data: { name: 'NotInitialized' } }),
    ]);
  });

  it('refuses an incompatible client, names both versions and exits with 3', async () => {
    const { sink, exits, send } = setup();
    await send({
      jsonrpc: '2.0',
      id: 1,
      method: 'initialize',
      params: { protocolVersion: '0.2.0', client: { name: 't', version: '1' } },
    });
    expect(sink.lines[0]).toEqual({
      jsonrpc: '2.0',
      id: 1,
      error: {
        code: -32002,
        message: `This client speaks protocol 0.2.0 but the engine speaks ${PROTOCOL_VERSION}. Update the engine to a version that supports protocol 0.2.`,
        data: {
          name: 'IncompatibleProtocol',
          clientProtocolVersion: '0.2.0',
          engineProtocolVersion: PROTOCOL_VERSION,
          engineVersion: '9.9.9',
        },
      },
    });
    expect(exits).toEqual([3]);
  });

  it('tells an older client to update itself', async () => {
    const { sink, send } = setup();
    await send({
      jsonrpc: '2.0',
      id: 1,
      method: 'initialize',
      params: { protocolVersion: '0.0.5', client: { name: 't', version: '1' } },
    });
    expect((sink.lines[0]?.error as { message: string }).message).toContain('Update the client');
  });

  it('rejects a second initialize with invalid request', async () => {
    const { sink, initialize } = setup();
    await initialize();
    await initialize();
    expect(sink.lines[1]?.error).toMatchObject({ code: -32600, data: { name: 'InvalidRequest' } });
  });
});

describe('Session dispatch', () => {
  it('answers invalid JSON with a parse error and id null', async () => {
    const { sink, session } = setup();
    await session.receive({ kind: 'line', text: '{not json' });
    expect(sink.lines[0]).toMatchObject({
      id: null,
      error: { code: -32700, data: { name: 'ParseError' } },
    });
  });

  it('answers a malformed envelope with invalid request', async () => {
    const { sink, send } = setup();
    await send({ jsonrpc: '1.0', id: 3, method: 'initialize' });
    await send([1, 2]);
    expect(sink.lines.map((line) => [line.id, (line.error as { code: number }).code])).toEqual([
      [3, -32600],
      [null, -32600],
    ]);
  });

  it('answers an oversized line with MessageTooLarge and id null', async () => {
    const { sink, session } = setup();
    await session.receive({ kind: 'tooLarge', bytes: 5_000_000 });
    expect(sink.lines[0]).toMatchObject({
      id: null,
      error: { code: -32009, data: { name: 'MessageTooLarge' } },
    });
  });

  it('ignores notifications from the client', async () => {
    const { sink, send } = setup();
    await send({ jsonrpc: '2.0', method: 'somethingHappened' });
    expect(sink.lines).toEqual([]);
  });

  it('answers unknown and not-yet-implemented methods with method not found', async () => {
    const { sink, send, initialize } = setup();
    await initialize();
    await send({ jsonrpc: '2.0', id: 1, method: 'noSuchMethod' });
    await send({ jsonrpc: '2.0', id: 2, method: 'startRun', params: {} });
    expect(sink.lines.slice(1).map((line) => (line.error as { code: number }).code)).toEqual([
      -32601, -32601,
    ]);
  });

  it('checks parameters against the protocol schema', async () => {
    const { sink, send, session, initialize } = setup();
    session.register('cancelRun', () => Promise.resolve(null));
    await initialize();
    await send({ jsonrpc: '2.0', id: 1, method: 'cancelRun', params: { runId: 42 } });
    expect(sink.lines[1]?.error).toMatchObject({ code: -32602, data: { name: 'InvalidParams' } });
    expect((sink.lines[1]?.error as { message: string }).message).toContain('runId');
  });

  it('sends a handler result, and an RpcError as its protocol error', async () => {
    const { sink, send, session, initialize } = setup();
    session.register('cancelRun', (params) =>
      params.runId === 'known'
        ? Promise.resolve(null)
        : Promise.reject(new RpcError('RunNotFound', 'No run "x".')),
    );
    await initialize();
    await send({ jsonrpc: '2.0', id: 'a', method: 'cancelRun', params: { runId: 'known' } });
    await send({ jsonrpc: '2.0', id: 'b', method: 'cancelRun', params: { runId: 'x' } });
    expect(sink.lines[1]).toEqual({ jsonrpc: '2.0', id: 'a', result: null });
    expect(sink.lines[2]).toMatchObject({
      id: 'b',
      error: { code: -32007, message: 'No run "x".', data: { name: 'RunNotFound' } },
    });
  });

  it('turns an unexpected handler error into an internal error and logs it', async () => {
    const { sink, send, session, initialize, errors } = setup();
    session.register('cancelRun', () => Promise.reject(new Error('boom')));
    await initialize();
    await send({ jsonrpc: '2.0', id: 1, method: 'cancelRun', params: { runId: 'r' } });
    expect(sink.lines[1]?.error).toMatchObject({ code: -32603, data: { name: 'InternalError' } });
    expect(errors[0]).toContain('boom');
  });

  it('answers shutdown with null, then exits with 0 and ignores later input', async () => {
    const { sink, send, exits, initialize } = setup();
    await initialize();
    await send({ jsonrpc: '2.0', id: 1, method: 'shutdown' });
    await send({ jsonrpc: '2.0', id: 2, method: 'shutdown' });
    expect(sink.lines[1]).toEqual({ jsonrpc: '2.0', id: 1, result: null });
    expect(sink.lines).toHaveLength(2);
    expect(exits).toEqual([0]);
  });
});

describe('review 0003 fixes', () => {
  it('finding 6: validate with neither files nor content says it needs one of them', async () => {
    const { sink, send, session, initialize } = setup();
    session.register('validate', () => Promise.resolve({ diagnostics: [] }));
    await initialize();
    await send({ jsonrpc: '2.0', id: 1, method: 'validate', params: {} });
    expect(sink.lines[1]?.error).toMatchObject({ code: -32602 });
    expect((sink.lines[1]?.error as { message: string }).message).toBe(
      'The parameters of "validate" are invalid: it needs "files" (a list of paths) or "content" (a file name and its text).',
    );
  });
});
