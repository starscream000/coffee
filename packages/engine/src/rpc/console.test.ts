// Unit tests for the stderr-only console.
import { PassThrough } from 'node:stream';
import { describe, expect, it } from 'vitest';
import { createStderrConsole } from './console.js';

describe('createStderrConsole', () => {
  it('sends the output of every console method to the one stream', () => {
    const stream = new PassThrough();
    let written = '';
    stream.on('data', (chunk: Buffer) => {
      written += chunk.toString('utf8');
    });
    const out = createStderrConsole(stream);
    out.log('log %s', 'x');
    out.info('info');
    out.debug('debug');
    out.warn('warn');
    out.error('error');
    out.dir({ dir: 1 });
    out.dirxml({ dirxml: 2 });
    out.table([{ table: 3 }]);
    out.group('group');
    out.groupEnd();
    out.count('count');
    out.time('time');
    out.timeLog('time');
    out.timeEnd('time');
    out.trace('trace');
    out.assert(false, 'assert');
    for (const text of [
      'log x',
      'info',
      'debug',
      'warn',
      'error',
      '{ dir: 1 }',
      '{ dirxml: 2 }',
      'table',
      'group',
      'count: 1',
      'time:',
      'trace',
      'assert',
    ]) {
      expect(written).toContain(text);
    }
  });
});
