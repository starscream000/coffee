// Browser tests for ctx.locate (ADR 0010) against a real headless Chromium,
// with static HTML set by page.setContent. Run by `pnpm test:integration`,
// never by `pnpm verify`.
import type { LocatorUse } from '@cfe/protocol';
import { chromium, type Browser, type Page } from 'playwright';
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest';
import type { InterpolationScope } from '../context/interpolate.js';
import { VariableStore } from '../context/variables.js';
import type { TargetValue } from '../schema/targets.js';
import {
  LocateError,
  locate,
  targetLookup,
  type LocateOptions,
  type LocatorFallbackWarning,
} from './locate.js';

let browser: Browser;
let page: Page;

beforeAll(async () => {
  browser = await chromium.launch();
});
afterAll(async () => {
  await browser.close();
});
beforeEach(async () => {
  page = await browser.newPage();
});
afterEach(async () => {
  await page.close();
});

class Reports {
  uses: LocatorUse[] = [];
  warnings: LocatorFallbackWarning[] = [];
  locatorUsed(use: LocatorUse): void {
    this.uses.push(use);
  }
  locatorFallback(warning: LocatorFallbackWarning): void {
    this.warnings.push(warning);
  }
}

const interpolationScope: InterpolationScope = {
  vars: new VariableStore({ buttonName: 'Pay now' }),
  env: { name: 'local', baseUrl: 'http://localhost', values: {} },
  secrets: { get: () => '' },
  row: { product: 'Chair' },
};

/** Overrides for {@link options}; `targets` are the file's own targets by name. */
type Overrides = Omit<Partial<LocateOptions>, 'targets'> & {
  targets?: Record<string, TargetValue>;
};

function options(reports: Reports, overrides: Overrides = {}): LocateOptions {
  const { targets, ...rest } = overrides;
  return {
    page,
    targets: targetLookup(targets, new Map()),
    scope: interpolationScope,
    testIdAttribute: 'data-testid',
    fallbackGraceMs: 0,
    timeoutMs: 2_000,
    signal: new AbortController().signal,
    reporter: reports,
    ...rest,
  };
}

async function idOf(target: TargetValue, extra: Overrides = {}): Promise<string | null> {
  const locator = await locate(target, options(new Reports(), extra));
  return locator.getAttribute('id');
}

describe('ctx.locate in Chromium', () => {
  it('maps each candidate kind to its Playwright call', async () => {
    await page.setContent(`
      <button id="role">Save</button>
      <label for="label">Email address</label><input id="label">
      <input id="placeholder" placeholder="Search">
      <p id="text">Order confirmed</p>
      <span id="testid" data-testid="cart-count">1</span>
      <div id="css" class="box"></div>
      <span id="custom" data-qa="total">9</span>`);
    expect(await idOf([{ role: 'button', name: 'Save' }])).toBe('role');
    expect(await idOf([{ label: 'Email address' }])).toBe('label');
    expect(await idOf([{ placeholder: 'Search' }])).toBe('placeholder');
    expect(await idOf([{ text: 'Order confirmed' }])).toBe('text');
    expect(await idOf([{ testId: 'cart-count' }])).toBe('testid');
    expect(await idOf([{ css: 'div.box' }])).toBe('css');
    expect(await idOf([{ testId: 'total' }], { testIdAttribute: 'data-qa' })).toBe('custom');
  });

  it('matches text exactly unless exact is false, and nth picks one of several', async () => {
    await page.setContent(`
      <p id="long">Order confirmed</p>
      <li id="a" class="item">A</li><li id="b" class="item">B</li>`);
    expect(await idOf([{ text: 'Order', exact: false }])).toBe('long');
    await expect(idOf([{ text: 'Order' }], { timeoutMs: 200 })).rejects.toMatchObject({
      code: 'TargetNotFound',
    });
    expect(await idOf([{ css: '.item', nth: 1 }])).toBe('b');
  });

  it('skips a first candidate that matches two elements and warns about the fallback', async () => {
    await page.setContent(`
      <button>Save</button><button>Save</button>
      <button id="real" data-testid="save">Save</button>`);
    const reports = new Reports();
    const locator = await locate('save', {
      ...options(reports, {
        targets: { save: [{ css: 'button:not([data-testid])' }, { testId: 'save' }] },
      }),
    });
    expect(await locator.getAttribute('id')).toBe('real');
    expect(reports.uses).toEqual([
      { param: 'target', target: 'save', candidateIndex: 1, candidate: { testId: 'save' } },
    ]);
    expect(reports.warnings).toEqual([
      expect.objectContaining({
        code: 'LocatorFallback',
        data: { target: 'save', candidateIndex: 1 },
      }),
    ]);
  });

  it('counts hidden elements, so a hidden copy makes a candidate ambiguous', async () => {
    await page.setContent(`
      <button>Save</button><button style="display:none">Save</button>
      <button id="real" data-testid="save">Save</button>`);
    expect(await idOf([{ css: 'button:not([data-testid])' }, { testId: 'save' }])).toBe('real');
  });

  it('tries fallbacks only after the grace period, then warns', async () => {
    await page.setContent('<button id="real" data-testid="save">Save</button>');
    const reports = new Reports();
    const started = performance.now();
    const locator = await locate(
      [{ role: 'button', name: 'Store' }, { testId: 'save' }],
      options(reports, { fallbackGraceMs: 400 }),
    );
    expect(performance.now() - started).toBeGreaterThanOrEqual(390);
    expect(await locator.getAttribute('id')).toBe('real');
    expect(reports.warnings).toEqual([
      expect.objectContaining({ code: 'LocatorFallback', data: { candidateIndex: 1 } }),
    ]);
  });

  it('does not fall back while the first candidate appears within the grace period', async () => {
    // A placeholder matches the fallback at once; the real button comes 300 ms later.
    await page.setContent(`
      <div class="btn" id="placeholder">Loading…</div>
      <script>
        setTimeout(() => {
          const button = document.createElement('button');
          button.id = 'real';
          button.textContent = 'Check out';
          document.body.append(button);
        }, 300);
      </script>`);
    const reports = new Reports();
    const locator = await locate(
      [{ role: 'button', name: 'Check out' }, { css: '.btn' }],
      options(reports, { fallbackGraceMs: 1_000 }),
    );
    expect(await locator.getAttribute('id')).toBe('real');
    expect(reports.uses[0]?.candidateIndex).toBe(0);
    expect(reports.warnings).toEqual([]);
  });

  it('resolves a frame inside a frame from the outside in, and reports every level', async () => {
    const inner = '<button id="pay">Pay now</button>';
    const middle = `<iframe id="inner" srcdoc='${inner}'></iframe><button>Pay now</button>`;
    await page.setContent(
      `<iframe id="outer" srcdoc="${middle.replace(/"/g, '&quot;')}"></iframe><button>Pay now</button>`,
    );
    const reports = new Reports();
    const locator = await locate(
      'payButton',
      options(reports, {
        targets: {
          outerFrame: [{ css: 'iframe#outer' }],
          innerFrame: { frame: 'outerFrame', candidates: [{ css: '#inner' }] },
          payButton: {
            frame: 'innerFrame',
            candidates: [{ role: 'button', name: '${vars.buttonName}' }],
          },
        },
      }),
    );
    expect(await locator.getAttribute('id')).toBe('pay');
    expect(reports.uses).toEqual([
      {
        param: 'target',
        target: 'payButton',
        candidateIndex: 0,
        candidate: { role: 'button', name: 'Pay now' },
        frame: {
          param: 'frame',
          target: 'innerFrame',
          candidateIndex: 0,
          candidate: { css: '#inner' },
          frame: {
            param: 'frame',
            target: 'outerFrame',
            candidateIndex: 0,
            candidate: { css: 'iframe#outer' },
          },
        },
      },
    ]);
  });

  it('does not accept an element that is not an iframe as a frame', async () => {
    await page.setContent('<div id="outer"><button>Pay</button></div>');
    await expect(
      locate(
        { frame: [{ css: '#outer' }], candidates: [{ role: 'button', name: 'Pay' }] },
        options(new Reports(), { timeoutMs: 200 }),
      ),
    ).rejects.toThrow(/0 elements/);
  });

  it('searches within an element found with interpolation', async () => {
    await page.setContent(`
      <div data-testid="product-Lamp"><button id="lamp">Delete</button></div>
      <div data-testid="product-Chair"><button id="chair">Delete</button></div>`);
    const reports = new Reports();
    const locator = await locate(
      'deleteProduct',
      options(reports, {
        targets: {
          productCard: [{ testId: 'product-${row.product}' }],
          deleteProduct: {
            within: 'productCard',
            candidates: [{ role: 'button', name: 'Delete' }],
          },
        },
      }),
    );
    expect(await locator.getAttribute('id')).toBe('chair');
    expect(reports.uses[0]?.within).toEqual({
      param: 'within',
      target: 'productCard',
      candidateIndex: 0,
      candidate: { testId: 'product-Chair' },
    });
  });

  it('stops within 200 ms when the signal is aborted during a wait', async () => {
    await page.setContent('<p>Nothing to find</p>');
    const controller = new AbortController();
    const reports = new Reports();
    const result = locate(
      [{ role: 'button', name: 'Never' }],
      options(reports, { timeoutMs: 30_000, signal: controller.signal }),
    );
    await new Promise((resolve) => setTimeout(resolve, 300));
    const abortedAt = performance.now();
    controller.abort(new Error('cancelled by the test'));
    await expect(result).rejects.toThrow('cancelled by the test');
    expect(performance.now() - abortedAt).toBeLessThan(200);
    expect(reports.uses).toEqual([{ param: 'target', candidateIndex: null, candidate: null }]);
  });

  it('throws TargetNotFound listing every candidate at every level with its count', async () => {
    await page.setContent(`
      <section class="cart"><button>Save</button><button>Save</button></section>`);
    const reports = new Reports();
    let error: unknown;
    try {
      await locate(
        'save',
        options(reports, {
          timeoutMs: 300,
          targets: {
            cart: [{ css: 'section.cart' }],
            save: {
              within: 'cart',
              candidates: [{ role: 'button', name: 'Save' }, { testId: 'save' }],
            },
          },
        }),
      );
    } catch (caught) {
      error = caught;
    }
    expect(error).toBeInstanceOf(LocateError);
    const located = error as LocateError;
    expect(located.code).toBe('TargetNotFound');
    expect(located.report).toEqual({
      param: 'target',
      target: 'save',
      candidates: [
        { candidate: { role: 'button', name: 'Save' }, matches: 2 },
        { candidate: { testId: 'save' }, matches: 0 },
      ],
      within: {
        param: 'within',
        target: 'cart',
        candidates: [{ candidate: { css: 'section.cart' }, matches: 1 }],
      },
    });
    expect(located.message).toBe(
      [
        'Target "save" was not found within 300ms: no candidate matched exactly one element. Last match counts:',
        '  within "cart":',
        '    0: css="section.cart" → 1 element',
        '  0: role=button name="Save" → 2 elements',
        '  1: testId="save" → 0 elements',
      ].join('\n'),
    );
    expect(reports.uses).toEqual([
      {
        param: 'target',
        target: 'save',
        candidateIndex: null,
        candidate: null,
        within: {
          param: 'within',
          target: 'cart',
          candidateIndex: 0,
          candidate: { css: 'section.cart' },
        },
      },
    ]);
  });

  // Review 0005, finding 2.
  it('tries every candidate before giving up when the timeout is shorter than the grace period', async () => {
    await page.setContent('<button id="real" data-testid="pay">Pay</button>');
    const reports = new Reports();
    const locator = await locate(
      'payButton',
      options(reports, {
        timeoutMs: 400,
        fallbackGraceMs: 1_000,
        targets: { payButton: [{ role: 'button', name: 'Pay now' }, { testId: 'pay' }] },
      }),
    );
    expect(await locator.getAttribute('id')).toBe('real');
    expect(reports.uses[0]?.candidateIndex).toBe(1);
    expect(reports.warnings).toEqual([
      expect.objectContaining({
        code: 'LocatorFallback',
        data: { target: 'payButton', candidateIndex: 1 },
      }),
    ]);
  });

  // Review 0005, finding 3.
  it('fails at once with InvalidSelector for a candidate Playwright rejects', async () => {
    await page.setContent('<div>x</div>');
    const started = performance.now();
    let error: unknown;
    try {
      await locate(
        'broken',
        options(new Reports(), {
          timeoutMs: 10_000,
          targets: { broken: [{ css: 'div[[' }, { testId: 'x' }] },
        }),
      );
    } catch (caught) {
      error = caught;
    }
    expect(performance.now() - started).toBeLessThan(1_000);
    expect(error).toBeInstanceOf(LocateError);
    expect(error).toMatchObject({ code: 'InvalidSelector' });
    expect((error as LocateError).message).toMatch(
      /^The candidate css="div\[\[" of target "broken" is not a valid selector: Unexpected token/,
    );
  });

  it('leaves a closed page to the caller instead of calling it a selector error', async () => {
    await page.setContent('<div>x</div>');
    const closing = await browser.newPage();
    await closing.close();
    await expect(
      locate([{ css: 'div' }], { ...options(new Reports()), page: closing }),
    ).rejects.toThrow(/has been closed/);
  });
});
