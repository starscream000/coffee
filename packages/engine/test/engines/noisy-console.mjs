// Test engine for finding 1 of review 0003: starts the real stdio server, then
// calls every console method. stdout must still carry protocol messages only.
import { runStdioServer } from '../../dist/rpc/stdio-server.js';

runStdioServer();
console.log('log');
console.info('info');
console.debug('debug');
console.warn('warn');
console.error('error');
console.dir({ dir: 1 });
console.dirxml({ dirxml: 2 });
console.table([{ table: 3 }]);
console.group('group');
console.groupCollapsed('groupCollapsed');
console.groupEnd();
console.groupEnd();
console.count('count');
console.countReset('count');
console.time('time');
console.timeLog('time');
console.timeEnd('time');
console.trace('trace');
console.assert(false, 'assert');
console.timeStamp?.('timeStamp');
console.profile?.('profile');
console.profileEnd?.('profile');
