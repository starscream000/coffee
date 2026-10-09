// One client session: parses each incoming line, enforces the handshake, checks
// parameters against the protocol schemas and dispatches requests to handlers
// (docs/protocol.md, "Handshake" and "Requests"). Requests are handled one at a
// time, in the order they arrive, so responses come back in that order.

import {
  InitializeParamsSchema,
  REQUESTS,
  isCompatibleProtocol,
  parseProtocolVersion,
  type InitializeResult,
  type RequestMethod,
  type RequestParams,
  type RequestResult,
} from '@cfe/protocol';
import type { EngineInfo } from '../engine-info.js';
import type { LineReaderItem } from './line-reader.js';
import type { MessageWriter } from './message-writer.js';
import { RpcError } from './rpc-error.js';

/** Exit code after a refused handshake (docs/protocol.md, "Engine exit codes"). */
export const EXIT_REFUSED = 3;

/** Exit code after `shutdown`. */
export const EXIT_OK = 0;

/**
 * Handles one request method. Receives parameters already checked against the
 * method's protocol schema; returns the result or throws an {@link RpcError}.
 */
export type RequestHandler<M extends RequestMethod> = (
  params: RequestParams<M>,
) => Promise<RequestResult<M>>;

/**
 * Options for {@link Session}.
 */
export interface SessionOptions {
  /** Identity of the engine, reported in `initialize`. */
  readonly engineInfo: EngineInfo;
  /** Browser names this engine can run (`capabilities.browsers`). */
  readonly browsers: readonly string[];
  /** Ends the process; called after the last response has been written. */
  readonly exit: (code: number) => void;
  /** Reports unexpected handler errors to developers (stderr). */
  readonly logError: (message: string) => void;
  /**
   * Called on `shutdown` before it is answered: cancels any run and closes
   * browsers (docs/protocol.md, "shutdown").
   */
  readonly beforeShutdown?: () => Promise<void>;
}

type JsonObject = Record<string, unknown>;
type AnyHandler = (params: unknown) => Promise<unknown>;

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readableId(message: unknown): string | number | null {
  if (isObject(message) && (typeof message.id === 'string' || Number.isInteger(message.id))) {
    return message.id as string | number;
  }
  return null;
}

/**
 * A client session over one input and one output stream.
 *
 * @example
 * ```ts
 * const session = new Session(writer, { engineInfo, browsers: [], exit, logError });
 * session.register('validate', async (params) => validate(params));
 * for (const item of reader.push(chunk)) session.receive(item);
 * ```
 */
export class Session {
  private readonly writer: MessageWriter;
  private readonly options: SessionOptions;
  private readonly handlers = new Map<RequestMethod, AnyHandler>();
  private initialized = false;
  private closing = false;
  private queue: Promise<void> = Promise.resolve();

  /**
   * @param writer - Where responses go.
   * @param options - Engine identity, capabilities and process hooks.
   */
  constructor(writer: MessageWriter, options: SessionOptions) {
    this.writer = writer;
    this.options = options;
    this.handlers.set('shutdown', async () => {
      await this.options.beforeShutdown?.();
      return null;
    });
  }

  /**
   * Sends a notification (an event) to the client.
   *
   * @param method - The event's name, such as `stepStarted`.
   * @param params - The event's fields.
   */
  notify(method: string, params: Record<string, unknown>): void {
    this.writer.send({ jsonrpc: '2.0', method, params }).catch((error: unknown) => {
      this.options.logError(
        `Could not send "${method}": ${error instanceof Error ? error.message : String(error)}`,
      );
    });
  }

  /**
   * Adds the handler for a request method. Methods without a handler answer
   * "method not found".
   *
   * @param method - A request method of the protocol (not `initialize`, which
   *   the session handles itself).
   * @param handler - Called with checked parameters.
   */
  register<M extends Exclude<RequestMethod, 'initialize' | 'shutdown'>>(
    method: M,
    handler: RequestHandler<M>,
  ): void {
    this.handlers.set(method, handler as AnyHandler);
  }

  /**
   * Queues one item from the line reader for handling.
   *
   * @param item - A complete line, or the report of an oversized line.
   * @returns A promise that resolves once this item (and every earlier one) has
   *   been answered.
   */
  receive(item: LineReaderItem): Promise<void> {
    this.queue = this.queue.then(() => this.handleItem(item));
    return this.queue;
  }

  /**
   * Waits until every item received so far has been answered.
   *
   * @returns A promise that resolves when the queue is empty.
   */
  async idle(): Promise<void> {
    let current = this.queue;
    await current;
    // Items may have been queued while waiting; wait for those too.
    while (current !== this.queue) {
      current = this.queue;
      await current;
    }
  }

  private async handleItem(item: LineReaderItem): Promise<void> {
    if (this.closing) {
      return;
    }
    if (item.kind === 'tooLarge') {
      await this.sendError(
        null,
        new RpcError(
          'MessageTooLarge',
          `A message of ${String(item.bytes)} bytes was discarded: the limit is 4 MiB per line. Send smaller messages.`,
        ),
      );
      return;
    }
    let message: unknown;
    try {
      message = JSON.parse(item.text);
    } catch {
      await this.sendStandardError(null, 'ParseError', 'The line is not valid JSON.');
      return;
    }
    await this.handleMessage(message);
  }

  private async handleMessage(message: unknown): Promise<void> {
    const id = readableId(message);
    if (
      !isObject(message) ||
      message.jsonrpc !== '2.0' ||
      typeof message.method !== 'string' ||
      ('id' in message && id === null)
    ) {
      await this.sendStandardError(
        id,
        'InvalidRequest',
        'The message is not a JSON-RPC 2.0 request: it needs "jsonrpc": "2.0", a string "method" and a string or integer "id".',
      );
      return;
    }
    if (id === null) {
      return; // A notification from the client: the protocol defines none, so it is ignored.
    }
    const method = message.method;
    const params = message.params ?? {};

    if (method === 'initialize') {
      await this.initialize(id, params);
      return;
    }
    if (!this.initialized) {
      await this.sendError(
        id,
        new RpcError(
          'NotInitialized',
          `"${method}" was sent before "initialize". Send "initialize" first.`,
        ),
      );
      return;
    }
    const handler = this.handlers.get(method as RequestMethod);
    if (handler === undefined || !(method in REQUESTS)) {
      await this.sendStandardError(
        id,
        'MethodNotFound',
        `This engine does not handle "${method}".`,
      );
      return;
    }
    const checked = REQUESTS[method as RequestMethod].params.safeParse(params);
    if (!checked.success) {
      await this.sendStandardError(
        id,
        'InvalidParams',
        `The parameters of "${method}" are invalid: ${checked.error.issues
          .map((issue) =>
            issue.path.length === 0 ? issue.message : `${issue.path.join('.')}: ${issue.message}`,
          )
          .join('; ')}.`,
      );
      return;
    }
    await this.runHandler(id, method, handler, checked.data);
  }

  private async initialize(id: string | number, params: unknown): Promise<void> {
    if (this.initialized) {
      await this.sendStandardError(
        id,
        'InvalidRequest',
        'The session is already initialized; send "initialize" only once.',
      );
      return;
    }
    const checked = InitializeParamsSchema.safeParse(params);
    if (!checked.success) {
      await this.sendStandardError(
        id,
        'InvalidParams',
        'The parameters of "initialize" are invalid: it needs "protocolVersion" and "client" with "name" and "version".',
      );
      return;
    }
    const { engineInfo } = this.options;
    const clientVersion = checked.data.protocolVersion;
    if (!isCompatibleProtocol(clientVersion, engineInfo.protocolVersion)) {
      this.closing = true;
      await this.sendError(
        id,
        new RpcError(
          'IncompatibleProtocol',
          incompatibleMessage(clientVersion, engineInfo.protocolVersion),
          {
            clientProtocolVersion: clientVersion,
            engineProtocolVersion: engineInfo.protocolVersion,
            engineVersion: engineInfo.version,
          },
        ),
      );
      this.options.exit(EXIT_REFUSED);
      return;
    }
    this.initialized = true;
    const result: InitializeResult = {
      protocolVersion: engineInfo.protocolVersion,
      engine: { name: engineInfo.name, version: engineInfo.version },
      capabilities: { browsers: [...this.options.browsers] },
    };
    await this.writer.send({ jsonrpc: '2.0', id, result });
  }

  private async runHandler(
    id: string | number,
    method: string,
    handler: AnyHandler,
    params: unknown,
  ): Promise<void> {
    let result: unknown;
    try {
      result = await handler(params);
    } catch (error) {
      if (error instanceof RpcError) {
        await this.sendError(id, error);
      } else {
        this.options.logError(
          `Internal error in "${method}": ${error instanceof Error ? (error.stack ?? error.message) : String(error)}`,
        );
        await this.sendStandardError(
          id,
          'InternalError',
          `The engine failed while handling "${method}". Details are on the engine's stderr.`,
        );
      }
      return;
    }
    await this.writer.send({ jsonrpc: '2.0', id, result: result ?? null });
    if (method === 'shutdown') {
      this.closing = true;
      this.options.exit(EXIT_OK);
    }
  }

  private sendStandardError(
    id: string | number | null,
    name: 'ParseError' | 'InvalidRequest' | 'MethodNotFound' | 'InvalidParams' | 'InternalError',
    message: string,
  ): Promise<void> {
    return this.sendError(id, new RpcError(name, message));
  }

  private sendError(id: string | number | null, error: RpcError): Promise<void> {
    return this.writer.send({ jsonrpc: '2.0', id, error: error.toJson() });
  }
}

function incompatibleMessage(clientVersion: string, engineVersion: string): string {
  const client = parseProtocolVersion(clientVersion);
  const engine = parseProtocolVersion(engineVersion);
  if (client === undefined) {
    return `"${clientVersion}" is not a protocol version: expected major.minor.patch, such as "${engineVersion}".`;
  }
  const series = (v: { major: number; minor: number }): string =>
    v.major === 0 ? `${String(v.major)}.${String(v.minor)}` : String(v.major);
  const speaks = `This client speaks protocol ${clientVersion} but the engine speaks ${engineVersion}.`;
  if (
    engine === undefined ||
    client.major > engine.major ||
    (client.major === engine.major && client.minor > engine.minor)
  ) {
    return `${speaks} Update the engine to a version that supports protocol ${series(client)}.`;
  }
  return `${speaks} Update the client to a version that speaks protocol ${series(engine)}.`;
}
