// Unit tests for redirecting console output away from stdout.
import { describe, expect, it } from 'vitest';
import { redirectConsoleToStderr } from './console.js';

describe('redirectConsoleToStderr', () => {
  it('sends every console method to the sink', () => {
    const written: string[] = [];
    const target = {
      log: console.log,
      info: console.info,
      debug: console.debug,
      warn: console.warn,
      error: console.error,
      trace: console.trace,
    };
    redirectConsoleToStderr(target, { write: (text: string) => written.push(text) });
    target.log('a %s', 'b');
    target.info('info');
    target.debug('debug');
    target.warn('warn');
    target.error('error', { x: 1 });
    target.trace('trace');
    expect(written).toEqual([
      'a b\n',
      'info\n',
      'debug\n',
      'warn\n',
      'error { x: 1 }\n',
      'trace\n',
    ]);
  });
});
