// I13: calls every console method at load time. stdout must still carry
// protocol messages only.
import { defineAction, z } from '@cfe/engine/sdk';

console.log('log');
console.info('info');
console.debug('debug');
console.warn('warn');
console.error('error');
console.dir({ dir: 1 });
console.dirxml({ dirxml: 2 });
console.table([{ table: 3 }]);
console.group('group');
console.groupEnd();
console.count('count');
console.time('time');
console.timeLog('time');
console.timeEnd('time');
console.trace('trace');
console.assert(false, 'assert');

export default defineAction({
  name: 'noisy.nothing',
  description: 'Does nothing; its file is noisy.',
  params: z.strictObject({}),
  run: () => Promise.resolve(),
});
