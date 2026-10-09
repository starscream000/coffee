// Test helper: sends `startRun` to an engine and collects everything until
// `runFinished`, checking every message against the protocol's JSON Schema
// files on the way. Not part of the engine build.

import { checkMessage } from './protocol-schemas.js';
import type { EngineProcess } from './engine-process.js';

/** One event as a client receives it. */
export interface RunEvent {
  /** The event's name, such as `stepPassed`. */
  readonly method: string;
  /** Its fields. */
  readonly params: Record<string, unknown>;
}

/** What a run sent. */
export interface CollectedRun {
  /** The response to `startRun`. */
  readonly response: Record<string, unknown>;
  /** Every event, in order; empty when `startRun` failed. */
  readonly events: readonly RunEvent[];
  /** Every message that did not match its JSON Schema file; must stay empty. */
  readonly invalid: readonly string[];
}

/**
 * Starts a run and waits for `runFinished`.
 *
 * @param engine - An initialised engine with a project open.
 * @param id - The request id to use.
 * @param params - `startRun` parameters.
 * @param timeoutMs - How long to wait for each message.
 * @returns The response and the events.
 *
 * @example
 * ```ts
 * const run = await collectRun(app.engine, 2, { files: ['tests/frames.test.yaml'] });
 * ```
 */
export async function collectRun(
  engine: EngineProcess,
  id: number,
  params: Record<string, unknown>,
  timeoutMs = 60_000,
): Promise<CollectedRun> {
  engine.send({ jsonrpc: '2.0', id, method: 'startRun', params });
  const invalid: string[] = [];
  const events: RunEvent[] = [];
  let response: Record<string, unknown> | undefined;
  for (;;) {
    const message = await engine.next(timeoutMs);
    if (message.id === id) {
      invalid.push(...checkMessage(message, 'startRun'));
      response = message;
      if ('error' in message) break;
      continue;
    }
    invalid.push(...checkMessage(message));
    const event = {
      method: String(message.method),
      params: message.params as Record<string, unknown>,
    };
    events.push(event);
    if (event.method === 'runFinished') break;
  }
  if (response === undefined) throw new Error('The run finished before startRun was answered.');
  return { response, events, invalid };
}

/**
 * Picks the events of one kind.
 *
 * @param events - A run's events.
 * @param method - The event name.
 * @returns The params of each matching event, in order.
 */
export function eventsOf(events: readonly RunEvent[], method: string): Record<string, unknown>[] {
  return events.filter((event) => event.method === method).map((event) => event.params);
}
