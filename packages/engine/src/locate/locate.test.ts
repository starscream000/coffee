// Unit tests for the parts of ctx.locate that need no browser: target lookup,
// candidate descriptions, and broken target definitions. The browser behaviour
// is tested in locate.integration.test.ts.
import type { Page } from 'playwright';
import { describe, expect, it } from 'vitest';
import { VariableStore } from '../context/variables.js';
import { describeCandidate } from './candidates.js';
import { locate, targetLookup, type LocateOptions } from './locate.js';

/** A page that fails the test if anything touches it. */
const untouchablePage = new Proxy(
  {},
  {
    get(): never {
      throw new Error('the page must not be used');
    },
  },
) as Page;

function options(targets: LocateOptions['targets']): LocateOptions {
  return {
    page: untouchablePage,
    targets,
    scope: {
      vars: new VariableStore(),
      env: { name: 'local', baseUrl: 'http://localhost', values: {} },
      secrets: { get: () => '' },
    },
    testIdAttribute: 'data-testid',
    fallbackGraceMs: 0,
    timeoutMs: 1_000,
    signal: new AbortController().signal,
    reporter: { locatorUsed: () => undefined, locatorFallback: () => undefined },
  };
}

describe('targetLookup', () => {
  it("looks in the file's own targets first, then in the shared targets", () => {
    const lookup = targetLookup(
      { save: [{ testId: 'local' }] },
      new Map([
        ['save', [{ testId: 'shared' }]],
        ['cart', [{ testId: 'cart' }]],
      ]),
    );
    expect(lookup('save')).toEqual([{ testId: 'local' }]);
    expect(lookup('cart')).toEqual([{ testId: 'cart' }]);
    expect(lookup('constructor')).toBeUndefined();
    expect(targetLookup(undefined, new Map())('save')).toBeUndefined();
  });
});

describe('describeCandidate', () => {
  it('writes each field in one line, quoting text', () => {
    expect(describeCandidate({ role: 'button', name: 'Save', exact: false })).toBe(
      'role=button name="Save" exact=false',
    );
    expect(describeCandidate({ css: '#cart .btn', nth: 1 })).toBe('css="#cart .btn" nth=1');
  });
});

describe('locate with a broken target', () => {
  it('fails at once for a name that is not defined', async () => {
    await expect(
      locate('missing', options(targetLookup(undefined, new Map()))),
    ).rejects.toMatchObject({
      code: 'UnknownTarget',
      message: 'There is no target named "missing".',
    });
  });

  it('fails at once for targets that refer to each other in a cycle', async () => {
    const lookup = targetLookup(
      {
        a: { within: 'b', candidates: [{ css: 'a' }] },
        b: { within: 'a', candidates: [{ css: 'b' }] },
      },
      new Map(),
    );
    await expect(locate('a', options(lookup))).rejects.toMatchObject({
      code: 'TargetCycle',
      message: 'The targets refer to each other in a cycle: a → b → a.',
    });
  });

  it('fails at once when the signal is already aborted', async () => {
    const controller = new AbortController();
    controller.abort(new Error('stopped'));
    await expect(
      locate([{ css: 'a' }], {
        ...options(targetLookup(undefined, new Map())),
        signal: controller.signal,
      }),
    ).rejects.toThrow('stopped');
  });
});
