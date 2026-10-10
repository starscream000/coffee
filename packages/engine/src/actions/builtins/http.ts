// The HTTP actions (docs/actions.md, "HTTP" and "Waiting"): api, mock,
// wait.response and expect.response. api sends requests with ctx.request, so
// they carry the page's cookies; mock answers matching requests in the step's
// browser context until the test ends; wait.response and expect.response read
// the test's response log from the start of the previous step on. Whenever a
// response's Cookie, Set-Cookie or Authorization header is stored or read, its
// values are registered as secrets (ADR 0014).

import { dirname, resolve } from 'node:path';
import type { Response } from 'playwright';
import { internalsOf, remainingMs, stepFileOf } from '../../runner/context.js';
import { StepError } from '../../runner/errors.js';
import type { ActionContext } from '../../sdk/context.js';
import { defineAction } from '../../sdk/define-action.js';
import { AssertionError } from '../../sdk/errors.js';
import { poll, pollBudget, specOf } from './shared.js';
import { urlMatcher } from './url-pattern.js';

type Text = string | number | boolean;

/** What `as` stores from a response. */
interface StoredResponse {
  status: number;
  headers: Record<string, string>;
  json: unknown;
  text: string;
}

const SENSITIVE_HEADERS = new Set(['cookie', 'set-cookie', 'authorization']);

/**
 * The values a sensitive header carries: for Cookie and Set-Cookie each
 * cookie's value; for Authorization the whole value and, after a scheme such
 * as `Bearer`, the credential alone.
 *
 * @param name - The header name, in lower case.
 * @param value - Its value; several Set-Cookie headers are joined by newlines.
 * @returns The values to mask.
 * @example
 * ```ts
 * sensitiveValues('authorization', 'Bearer abc123'); // ['Bearer abc123', 'abc123']
 * ```
 */
export function sensitiveValues(name: string, value: string): string[] {
  if (name === 'authorization') {
    const token = value.split(/\s+/).slice(1).join(' ');
    return token === '' ? [value] : [value, token];
  }
  const cookies =
    name === 'set-cookie'
      ? value.split('\n').map((line) => line.split(';')[0] ?? '')
      : value.split(';');
  return cookies
    .map((cookie) => cookie.trim())
    .filter((cookie) => cookie.includes('='))
    .map((cookie) => cookie.slice(cookie.indexOf('=') + 1));
}

/**
 * Registers the values of a response's Cookie, Set-Cookie and Authorization
 * headers as secrets for the rest of the run. A value shorter than 4
 * characters cannot be masked; a warning names the header, never the value.
 *
 * @param ctx - The step's context.
 * @param headers - Response headers, names in lower case.
 */
export function registerSensitiveHeaders(
  ctx: ActionContext,
  headers: Record<string, string>,
): void {
  const { registerSecret } = internalsOf(ctx);
  for (const [name, value] of Object.entries(headers)) {
    const header = name.toLowerCase();
    if (!SENSITIVE_HEADERS.has(header)) continue;
    for (const secret of sensitiveValues(header, value)) {
      if (!registerSecret(secret)) {
        ctx.log.warn(
          `A value in the ${name} header is shorter than 4 characters, so it is not masked.`,
        );
      }
    }
  }
}

/** What `as` stores: status, headers, the body as text and, when it is JSON, parsed. */
function storedResponse(
  status: number,
  headers: Record<string, string>,
  body: string | undefined,
): StoredResponse {
  return { status, headers, json: parseJson(body ?? ''), text: body ?? '' };
}

/** The body parsed as JSON, or null when it is not JSON. */
function parseJson(body: string): unknown {
  try {
    return JSON.parse(body) as unknown;
  } catch {
    return null;
  }
}

/**
 * A browser response's body, or undefined when it has not finished loading
 * when the step's time is almost up. Chromium never finishes loading the body
 * of a fetch the page does not read, and Playwright then waits for it forever.
 */
function bodyOf(ctx: ActionContext, response: Response): Promise<string | undefined> {
  return new Promise((resolve, reject) => {
    const onAbort = (): void => {
      clearTimeout(timer);
      reject(ctx.signal.reason instanceof Error ? ctx.signal.reason : new Error('Aborted'));
    };
    const timer = setTimeout(() => {
      ctx.signal.removeEventListener('abort', onAbort);
      resolve(undefined);
    }, pollBudget(ctx));
    ctx.signal.addEventListener('abort', onAbort, { once: true });
    response.text().then(
      (text) => {
        clearTimeout(timer);
        ctx.signal.removeEventListener('abort', onAbort);
        resolve(text);
      },
      () => {
        clearTimeout(timer);
        ctx.signal.removeEventListener('abort', onAbort);
        resolve('');
      },
    );
  });
}

const UNREAD_BODY =
  'its body did not finish loading (a page that never reads a response body never loads it)';

function stringHeaders(headers: Record<string, Text> | undefined): Record<string, string> {
  return Object.fromEntries(
    Object.entries(headers ?? {}).map(([name, value]) => [name, String(value)]),
  );
}

/**
 * `api`: sends an HTTP request with ctx.request (signed in like the page),
 * checks its status (by default any 2xx or 3xx) and optionally stores the
 * response in a variable.
 */
export const api = defineAction({
  ...specOf('api'),
  async run(ctx, params) {
    const {
      method = 'GET',
      url,
      headers,
      query,
      json,
      form,
      body,
      as,
      status,
    } = params as unknown as {
      method?: string;
      url: string;
      headers?: Record<string, Text>;
      query?: Record<string, Text>;
      json?: unknown;
      form?: Record<string, Text>;
      body?: string;
      as?: string;
      status?: number;
    };
    const response = await ctx.request.fetch(url, {
      method,
      headers: stringHeaders(headers),
      ...(query === undefined ? {} : { params: stringHeaders(query) }),
      ...(json !== undefined ? { data: json } : body !== undefined ? { data: body } : {}),
      ...(form === undefined ? {} : { form: stringHeaders(form) }),
      failOnStatusCode: false,
      timeout: remainingMs(ctx),
    });
    const actual = response.status();
    if (as !== undefined) {
      const headersOf = response.headers();
      registerSensitiveHeaders(ctx, headersOf);
      ctx.vars.set(as, storedResponse(actual, headersOf, await response.text()));
    }
    const fits = status === undefined ? actual >= 200 && actual < 400 : actual === status;
    if (!fits) {
      throw new AssertionError(
        `${method} ${url} answered with status ${String(actual)}, expected ${status === undefined ? 'a 2xx or 3xx status' : String(status)}.`,
        { expected: status ?? '2xx or 3xx', actual },
      );
    }
  },
});

/** `mock`: answers matching requests in the step's browser context with a fixed response. */
export const mock = defineAction({
  ...specOf('mock'),
  async run(ctx, params) {
    const { url, method, status, headers, json, body, file, times } = params as unknown as {
      url: string;
      method?: string;
      status?: number;
      headers?: Record<string, Text>;
      json?: unknown;
      body?: string;
      file?: string;
      times?: number;
    };
    const matches = urlMatcher(url, ctx.env.baseUrl);
    const stepFile = stepFileOf(ctx);
    if (file !== undefined && stepFile === undefined) {
      throw new StepError(
        'MockUnavailable',
        'This step has no file to resolve the mock file from.',
      );
    }
    const path =
      file === undefined || stepFile === undefined ? undefined : resolve(dirname(stepFile), file);
    let answered = 0;
    await ctx.page.context().route(
      (address) => matches(address.href),
      async (route) => {
        const request = route.request();
        if (
          (method !== undefined && request.method() !== method) ||
          (times !== undefined && answered >= times)
        ) {
          await route.fallback();
          return;
        }
        answered += 1;
        await route.fulfill({
          status: status ?? 200,
          headers: stringHeaders(headers),
          ...(json !== undefined ? { json } : path !== undefined ? { path } : { body: body ?? '' }),
        });
      },
    );
  },
});

interface ResponseQuery {
  url: string;
  method?: string;
  status?: number;
}

/** The responses since the previous step started that match a URL pattern and method. */
function matching(ctx: ActionContext, query: ResponseQuery, withStatus: boolean): Response[] {
  const { responses, responsesSince } = internalsOf(ctx);
  if (responses === undefined) {
    throw new StepError('ResponsesUnavailable', 'This step has no response log to read.');
  }
  const matches = urlMatcher(query.url, ctx.env.baseUrl);
  return responses
    .since(responsesSince)
    .map((entry) => entry.response)
    .filter(
      (response) =>
        matches(response.url()) &&
        (query.method === undefined || response.request().method() === query.method) &&
        (!withStatus || query.status === undefined || response.status() === query.status),
    );
}

/**
 * `wait.response`: waits for a response whose URL matches, also one that
 * arrived since the previous step started; with `as`, stores it.
 */
export const waitResponse = defineAction({
  ...specOf('wait.response'),
  async run(ctx, params) {
    const query = params as unknown as ResponseQuery & { as?: string };
    let found: Response | undefined;
    const arrived = await poll(ctx, () => {
      found = matching(ctx, query, true)[0];
      return Promise.resolve(found !== undefined);
    });
    if (!arrived || found === undefined) {
      ctx.signal.throwIfAborted();
      throw new StepError(
        'ActionTimeout',
        `No response to ${query.method ?? 'any method'} "${query.url}"${query.status === undefined ? '' : ` with status ${String(query.status)}`} arrived in time.`,
      );
    }
    const response = found;
    const headers = await response.allHeaders();
    registerSensitiveHeaders(ctx, headers);
    if (query.as !== undefined) {
      const body = await bodyOf(ctx, response);
      if (body === undefined) {
        ctx.log.warn(
          `The response to "${query.url}" arrived, but ${UNREAD_BODY}, so "${query.as}" has no text or json.`,
        );
      }
      ctx.vars.set(query.as, storedResponse(response.status(), headers, body));
    }
  },
});

/**
 * Whether `actual` contains everything in `expected`: objects key by key,
 * lists element by element (same length), other values equal.
 *
 * @param expected - The partial value the step gives.
 * @param actual - The response's JSON.
 * @returns True when `actual` matches.
 */
export function partialMatch(expected: unknown, actual: unknown): boolean {
  if (Array.isArray(expected)) {
    return (
      Array.isArray(actual) &&
      actual.length === expected.length &&
      expected.every((item, index) => partialMatch(item, actual[index]))
    );
  }
  if (typeof expected === 'object' && expected !== null) {
    if (typeof actual !== 'object' || actual === null || Array.isArray(actual)) return false;
    return Object.entries(expected).every(
      ([key, item]) =>
        Object.hasOwn(actual, key) && partialMatch(item, (actual as Record<string, unknown>)[key]),
    );
  }
  return Object.is(expected, actual);
}

/**
 * `expect.response`: a response whose URL matches has the expected status,
 * contains the expected part of its JSON, or the expected text in its body.
 */
export const expectResponse = defineAction({
  ...specOf('expect.response'),
  async run(ctx, params) {
    const query = params as unknown as ResponseQuery & { json?: unknown; contains?: string };
    const needsBody = query.json !== undefined || query.contains !== undefined;
    let last: StoredResponse | undefined;
    // Changed from the poll's callback, so kept in an object the type checker does not narrow.
    const seen = { unread: false };
    const passed = await poll(ctx, async () => {
      for (const response of matching(ctx, query, false)) {
        const headers = await response.allHeaders();
        registerSensitiveHeaders(ctx, headers);
        const body = needsBody ? await bodyOf(ctx, response) : '';
        seen.unread = body === undefined;
        const stored = storedResponse(response.status(), headers, body);
        last = stored;
        const fits =
          (query.status === undefined || stored.status === query.status) &&
          (query.json === undefined || partialMatch(query.json, stored.json)) &&
          (query.contains === undefined || stored.text.includes(query.contains));
        if (fits) return true;
      }
      return false;
    });
    if (passed) return;
    ctx.signal.throwIfAborted();
    const expected = {
      ...(query.status === undefined ? {} : { status: query.status }),
      ...(query.json === undefined ? {} : { json: query.json }),
      ...(query.contains === undefined ? {} : { contains: query.contains }),
    };
    if (last === undefined) {
      throw new AssertionError(
        `No response to "${query.url}" arrived since the previous step started.`,
        {
          expected,
          actual: null,
        },
      );
    }
    throw new AssertionError(
      `The response to "${query.url}" is not as expected${seen.unread ? `: ${UNREAD_BODY}` : ''}.`,
      {
        expected,
        actual: { status: last.status, json: last.json, text: last.text },
      },
    );
  },
});
