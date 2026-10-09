// Test engine for finding 4 of review 0004: starts the real stdio server,
// registers a secret, then fails with an internal error whose message contains
// it. The error logged on stderr must show the secret masked.
import { runStdioServer } from '../../dist/rpc/stdio-server.js';

const { secrets } = runStdioServer();
secrets.register('hunter2secret');
setTimeout(() => {
  throw new Error('internal failure with hunter2secret inside');
}, 0);
