// Test engine for finding 2 of review 0003: the real stdio server with a slow
// handler for "listTests", so a test can close stdin while a request is still
// being answered and check that the answer still arrives.
import { runStdioServer } from '../../dist/rpc/stdio-server.js';

const { session } = runStdioServer();
session.register('listTests', async () => {
  await new Promise((resolve) => setTimeout(resolve, 400));
  return { tests: [] };
});
