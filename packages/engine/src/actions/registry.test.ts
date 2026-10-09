// Unit tests for the action registry and the naming rules of ADR 0016.
import { describe, expect, it } from 'vitest';
import { z } from 'zod';
import { BUILTIN_SPECS } from './builtin-specs.js';
import { ActionRegistry, RESERVED_NAMESPACES } from './registry.js';

const at = { file: 'actions/auth.ts', line: 4, column: 9 };
const action = (name: string, extra: Record<string, unknown> = {}) => ({
  name,
  description: 'Does something.',
  params: z.strictObject({ code: z.string() }),
  run: () => Promise.resolve(),
  ...extra,
});

describe('ActionRegistry', () => {
  it('holds the built-ins and adds a valid user action', () => {
    const registry = new ActionRegistry(BUILTIN_SPECS);
    expect(
      registry.addUserAction(action('auth.fillOtp', { shorthand: 'code' }), at),
    ).toBeUndefined();
    expect(registry.specs.get('auth.fillOtp')).toMatchObject({
      name: 'auth.fillOtp',
      shorthand: 'code',
    });
    expect(registry.list().at(-1)?.source).toEqual({ kind: 'file', ...at });
    expect(registry.specs.size).toBe(BUILTIN_SPECS.length + 1);
  });

  it('reserves expect, wait, api and the command name, from one list', () => {
    expect(RESERVED_NAMESPACES).toEqual(['expect', 'wait', 'api', 'cfe']);
    for (const namespace of RESERVED_NAMESPACES) {
      const registry = new ActionRegistry(BUILTIN_SPECS);
      expect(registry.addUserAction(action(`${namespace}.thing`), at)?.code).toBe(
        'ActionNamespaceReserved',
      );
    }
  });

  it('gives the documented messages, at the action file and line', () => {
    const registry = new ActionRegistry(BUILTIN_SPECS);
    expect(registry.addUserAction(action('fillOtp'), at)).toEqual({
      ...at,
      severity: 'error',
      code: 'ActionNameNotNamespaced',
      message:
        'Action "fillOtp" needs a namespace, for example "auth.fillOtp". Names without a dot are reserved for built-in actions.',
    });
    expect(
      registry.addUserAction(action('expect.priceFormat'), { ...at, file: 'actions/shop.ts' })
        ?.message,
    ).toBe(
      'Action "expect.priceFormat" uses the namespace "expect", which is reserved for built-in actions. Use your own namespace, for example "shop.expectPriceFormat".',
    );
    expect(registry.addUserAction(action('click'), at)?.message).toContain(
      '"click" is a built-in action',
    );
  });

  it('reports duplicates, bad names, bad shorthands and non-actions', () => {
    const registry = new ActionRegistry(BUILTIN_SPECS);
    registry.addUserAction(action('auth.login'), at);
    expect(
      registry.addUserAction(action('auth.login'), { ...at, file: 'actions/other.ts' }),
    ).toMatchObject({
      code: 'ActionNameTaken',
      message: 'Action "auth.login" is already defined in actions/auth.ts:4.',
    });
    expect(registry.addUserAction(action('Auth.login'), at)?.code).toBe('InvalidActionName');
    expect(registry.addUserAction(action('auth.otp', { shorthand: 'nope' }), at)?.code).toBe(
      'InvalidShorthand',
    );
    expect(registry.addUserAction({ name: 'auth.x' }, at)?.code).toBe('InvalidActionExport');
    expect(registry.addUserAction(undefined, at)?.code).toBe('InvalidActionExport');
  });

  it('remembers rejected actions and failed files for later messages', () => {
    const registry = new ActionRegistry(BUILTIN_SPECS);
    registry.addUserAction(action('fillOtp'), at);
    registry.addFailedFile('actions/broken.ts');
    expect(registry.rejected.get('fillOtp')?.location).toEqual(at);
    expect(registry.failedFiles).toEqual(['actions/broken.ts']);
    expect(registry.specs.has('fillOtp')).toBe(false);
  });
});
