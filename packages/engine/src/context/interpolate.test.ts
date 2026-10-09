// Unit tests for variables, run-time interpolation and environment profiles.
import { describe, expect, it } from 'vitest';
import type { ConfigFile } from '../schema/files.js';
import { selectEnvironment, UnknownEnvironmentError } from './environment.js';
import {
  InterpolationError,
  interpolate,
  unsetVariables,
  type InterpolationScope,
} from './interpolate.js';
import { SecretRegistry } from './mask.js';
import { VariableStore } from './variables.js';

function scope(overrides: Partial<InterpolationScope> = {}): InterpolationScope {
  return {
    vars: new VariableStore({
      n: 3,
      order: { id: 'A-1', items: [{ name: 'Lamp' }, { name: 'Chair' }] },
      flag: true,
      nothing: null,
    }),
    env: {
      name: 'local',
      baseUrl: 'http://localhost:1',
      values: { apiUrl: 'http://localhost:1/api' },
    },
    secrets: { get: (name: string) => (name === 'TOKEN' ? 'tok-1234' : '') },
    row: { sku: 'desk-lamp' },
    step: 'steps.2 (fill)',
    ...overrides,
  };
}

describe('interpolate', () => {
  it('keeps the type of a value that is exactly one ${…}', () => {
    expect(interpolate('${vars.n}', scope())).toBe(3);
    expect(interpolate('${vars.flag}', scope())).toBe(true);
    expect(interpolate('${vars.order}', scope())).toEqual({
      id: 'A-1',
      items: [{ name: 'Lamp' }, { name: 'Chair' }],
    });
  });

  it('turns anything else into text', () => {
    expect(interpolate('n=${vars.n}, flag=${vars.flag}, nothing=${vars.nothing}', scope())).toBe(
      'n=3, flag=true, nothing=null',
    );
    expect(interpolate('/p/${row.sku}?o=${vars.order.id}', scope())).toBe('/p/desk-lamp?o=A-1');
  });

  it('follows dotted paths into objects and lists', () => {
    expect(interpolate('${vars.order.items.1.name}', scope())).toBe('Chair');
    expect(() => interpolate('${vars.order.items.5.name}', scope())).toThrow(InterpolationError);
  });

  it('writes $${ as a literal ${', () => {
    expect(interpolate('cost: $${price} for ${vars.n}', scope())).toBe('cost: ${price} for 3');
  });

  it('resolves env, secrets, row and params, and walks lists and mappings', () => {
    expect(
      interpolate(
        {
          url: '${env.apiUrl}/orders',
          who: '${env.name}',
          auth: 'Bearer ${secrets.TOKEN}',
          list: ['${row.sku}', 2],
        },
        scope(),
      ),
    ).toEqual({
      url: 'http://localhost:1/api/orders',
      who: 'local',
      auth: 'Bearer tok-1234',
      list: ['desk-lamp', 2],
    });
    expect(interpolate('${params.email}', scope({ params: { email: 'a@b.c' } }))).toBe('a@b.c');
  });

  it('names the step and lists the existing variables for an unset variable', () => {
    let error: unknown;
    try {
      interpolate('${vars.orderNumber}', scope());
    } catch (caught) {
      error = caught;
    }
    expect(error).toBeInstanceOf(InterpolationError);
    expect(error).toMatchObject({
      code: 'VariableNotSet',
      variable: 'orderNumber',
      message:
        'The variable "orderNumber" was never set in step steps.2 (fill). Variables that exist: flag, n, nothing, order.',
    });
  });

  // Review 0004, finding 5.
  it.each([
    '${vars.order.constructor}',
    '${vars.order.toString}',
    '${vars.order.__proto__}',
    '${vars.order.items.length}',
    '${vars.order.items.5}',
    '${env.constructor}',
    '${env.toString}',
    '${row.constructor}',
  ])('reaches own properties only: %s is PathNotFound', (expression) => {
    expect(() => interpolate(expression, scope())).toThrow(
      expect.objectContaining({ code: 'PathNotFound' }) as Error,
    );
  });

  it('reaches own properties only for params', () => {
    expect(() => interpolate('${params.hasOwnProperty}', scope({ params: {} }))).toThrow(
      expect.objectContaining({ code: 'PathNotFound' }) as Error,
    );
  });

  it('reports a namespace that is not available here', () => {
    expect(() => interpolate('${params.x}', scope())).toThrow(/not available here/);
    expect(() => interpolate('${env.missing}', scope())).toThrow(/has no value "missing"/);
  });
});

describe('unsetVariables', () => {
  it('lists the unset variables a value uses, once each, for the runner', () => {
    const vars = new VariableStore({ email: 'a@b.c' });
    expect(
      unsetVariables(
        {
          url: '/o/${vars.orderNumber}',
          x: ['${vars.email}', '${vars.orderNumber}', '${vars.total.x}'],
        },
        vars,
      ),
    ).toEqual(['orderNumber', 'total']);
  });
});

describe('selectEnvironment', () => {
  const config = {
    version: 1,
    defaults: { environment: 'staging', timeout: '20s', locale: 'fr-FR' },
    environments: {
      local: { baseUrl: 'http://localhost:1' },
      staging: {
        baseUrl: 'https://s.example.com',
        values: { apiUrl: 'https://s/api' },
        locale: 'de-DE',
        snapshots: 'off',
      },
    },
  } as ConfigFile;

  it('selects by name or by the default, and resolves environment, then defaults, then built-ins', () => {
    expect(selectEnvironment(config)).toEqual({
      name: 'staging',
      baseUrl: 'https://s.example.com',
      values: { apiUrl: 'https://s/api' },
      settings: {
        timeout: '20s',
        fallbackGrace: '1s',
        snapshots: 'off',
        viewport: { width: 1280, height: 720 },
        locale: 'de-DE',
        timezone: 'UTC',
      },
    });
    expect(selectEnvironment(config, 'local').settings).toMatchObject({
      locale: 'fr-FR',
      snapshots: 'always',
    });
  });

  it('falls back to the first environment and rejects unknown names', () => {
    expect(selectEnvironment({ ...config, defaults: {} }).name).toBe('local');
    expect(() => selectEnvironment(config, 'prod')).toThrow(UnknownEnvironmentError);
    expect(() => selectEnvironment(config, 'prod')).toThrow(
      'There is no environment "prod". The config has: local, staging.',
    );
  });
});

describe('VariableStore with secrets', () => {
  it('registers a secret read through a real store for masking', () => {
    const registry = new SecretRegistry();
    registry.register('tok-1234');
    expect(registry.mask(String(interpolate('Bearer ${secrets.TOKEN}', scope())))).toBe(
      'Bearer •••',
    );
  });
});
