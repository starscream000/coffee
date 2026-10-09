// Integration test of the demo app harness (instruction 0005, task 19): starts
// the demo server and the engine through the harness, then loads each page in
// Chromium and finds the elements the samples will use with ctx.locate.
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { PRODUCT, type LocatorUse } from '@cfe/protocol';
import { chromium, type Browser, type Locator, type Page } from 'playwright';
import { afterAll, beforeAll, beforeEach, afterEach, describe, expect, it } from 'vitest';
import { VariableStore } from '../context/variables.js';
import {
  locate,
  targetLookup,
  type LocateOptions,
  type LocatorFallbackWarning,
} from '../locate/locate.js';
import type { TargetValue } from '../schema/targets.js';
import { startDemoApp, type DemoApp } from './demo-app.js';

let app: DemoApp;
let browser: Browser;
let page: Page;

beforeAll(async () => {
  app = await startDemoApp({ env: { DEMO_PASSWORD: 'demo-password-1234' } });
  browser = await chromium.launch();
});
afterAll(async () => {
  await browser.close();
  await app.close();
});
beforeEach(async () => {
  page = await browser.newPage();
});
afterEach(async () => {
  await page.close();
});

/** What locate reported for one call. */
interface Reported {
  uses: LocatorUse[];
  warnings: LocatorFallbackWarning[];
}

async function find(
  target: TargetValue,
  overrides: Partial<LocateOptions> = {},
): Promise<{ locator: Locator; id: string | null; text: string; reported: Reported }> {
  const reported: Reported = { uses: [], warnings: [] };
  const locator = await locate(target, {
    page,
    targets: targetLookup(undefined, new Map()),
    scope: {
      vars: new VariableStore(),
      env: { name: 'local', baseUrl: app.url, values: {} },
      secrets: { get: () => '' },
    },
    testIdAttribute: 'data-testid',
    fallbackGraceMs: 1_000,
    timeoutMs: 10_000,
    signal: new AbortController().signal,
    reporter: {
      locatorUsed: (use) => reported.uses.push(use),
      locatorFallback: (warning) => reported.warnings.push(warning),
    },
    ...overrides,
  });
  return {
    locator,
    id: await locator.getAttribute('id'),
    text: (await locator.textContent()) ?? '',
    reported,
  };
}

async function api(path: string, init?: RequestInit): Promise<unknown> {
  const response = await fetch(`${app.url}${path}`, init);
  expect(response.ok).toBe(true);
  return response.json();
}

describe('the demo app harness', () => {
  it('opens the copied demo project with the server as the base URL', () => {
    expect(app.diagnostics).toEqual([]);
    expect(app.url).toMatch(/^http:\/\/127\.0\.0\.1:\d+$/);
    const config = readFileSync(join(app.root, PRODUCT.configFile), 'utf8');
    expect(config).toContain(`baseUrl: ${app.url}`);
    expect(config).not.toContain('localhost:4310');
  });

  it('S13, F1, F2, F3, F7: the to-do page', async () => {
    await page.goto(`${app.url}/todos`);
    await (await find([{ label: 'New to-do' }, { testId: 'new-todo' }])).locator.fill('Buy milk');
    await (await find([{ label: 'Done' }])).locator.check();
    await (await find([{ role: 'button', name: 'Add' }])).locator.click();
    await expect.poll(async () => (await find([{ testId: 'todo-count' }])).text).toBe('1');
    expect((await find([{ testId: 'todo-item' }])).text).toBe('Buy milk (done)');
    expect((await find([{ role: 'list', name: 'To-dos' }])).reported.uses[0]?.candidateIndex).toBe(
      0,
    );
  });

  it('S10, F1, F3: the reset endpoint empties the list and counts resets', async () => {
    const before = (await api('/api/state')) as { resets: number };
    await api('/api/todos', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ title: 'Walk the dog' }),
    });
    expect(await api('/api/reset', { method: 'POST' })).toEqual({
      todos: [],
      resets: before.resets + 1,
    });
    expect(await api('/api/state')).toEqual({ todos: [], resets: before.resets + 1 });
  });

  it('S11: the first candidate no longer matches, so the test ID is used with a warning', async () => {
    await page.goto(`${app.url}/fallback`);
    const found = await find([{ role: 'button', name: 'Place order' }, { testId: 'place-order' }], {
      fallbackGraceMs: 300,
    });
    expect(found.text).toBe('Submit order');
    expect(found.reported.uses[0]?.candidateIndex).toBe(1);
    expect(found.reported.warnings).toHaveLength(1);
  });

  it('S12: the real button wins over the placeholder that matches the fallback at once', async () => {
    await page.goto(`${app.url}/slow-render`);
    const found = await find([{ role: 'button', name: 'Check out' }, { css: '.checkout-button' }]);
    expect(found.text).toBe('Check out');
    expect(found.reported.uses[0]?.candidateIndex).toBe(0);
    expect(found.reported.warnings).toEqual([]);
  });

  it('S14: the card number field inside a frame inside a frame', async () => {
    await page.goto(`${app.url}/frames`);
    const found = await find('cardNumber', {
      targets: targetLookup(
        {
          checkoutFrame: [{ css: 'iframe#checkout' }],
          paymentFrame: {
            frame: 'checkoutFrame',
            candidates: [{ css: 'iframe[title="Secure payment"]' }],
          },
          cardNumber: { frame: 'paymentFrame', candidates: [{ label: 'Card number' }] },
        },
        new Map(),
      ),
    });
    expect(found.id).toBe('card-number');
    expect(found.reported.uses[0]?.frame?.frame?.target).toBe('checkoutFrame');
  });

  it('close stops the server and the engine and deletes the copy, even twice', async () => {
    const other = await startDemoApp({ env: { DEMO_PASSWORD: 'demo-password-1234' } });
    expect(existsSync(other.root)).toBe(true);
    await other.close();
    await other.close();
    expect(existsSync(other.root)).toBe(false);
    expect(other.engine.child.exitCode !== null || other.engine.child.signalCode !== null).toBe(
      true,
    );
    await expect(fetch(`${other.url}/`)).rejects.toThrow();
  });
});
