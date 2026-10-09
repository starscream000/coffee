#!/usr/bin/env node
// Process entry point of the engine: `node dist/main.js --stdio` starts one
// protocol session on stdin and stdout (docs/protocol.md, "Transport").

import { runStdioServer } from './rpc/stdio-server.js';

if (process.argv.includes('--stdio')) {
  runStdioServer();
} else {
  process.stderr.write(
    'The engine speaks its protocol on stdin and stdout. Start it with: node dist/main.js --stdio\n',
  );
  process.exitCode = 1;
}
