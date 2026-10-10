// The response log of a test (docs/actions.md, "Waiting"): every response of
// the test's browser contexts, with the time it arrived. `wait.response` and
// `expect.response` look back to the start of the previous step, so "click,
// then wait for the response it caused" cannot miss a response that arrived
// during the click.

import type { Response } from 'playwright';

/** One response, as the log keeps it. */
export interface LoggedResponse {
  /** When it arrived, on the `performance.now()` clock. */
  readonly at: number;
  /** Playwright's response. */
  readonly response: Response;
}

/**
 * The responses of one test instance.
 *
 * @example
 * ```ts
 * const log = new ResponseLog();
 * context.on('response', (response) => log.add(response));
 * const recent = log.since(previousStepStart);
 * ```
 */
export class ResponseLog {
  private readonly entries: LoggedResponse[] = [];

  /**
   * Records a response.
   *
   * @param response - A response of one of the test's pages.
   */
  add(response: Response): void {
    this.entries.push({ at: performance.now(), response });
  }

  /**
   * The responses that arrived at or after a time, oldest first.
   *
   * @param time - A time on the `performance.now()` clock.
   * @returns The responses since then.
   */
  since(time: number): readonly LoggedResponse[] {
    return this.entries.filter((entry) => entry.at >= time);
  }
}
