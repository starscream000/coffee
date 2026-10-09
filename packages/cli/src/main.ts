#!/usr/bin/env node
// Process entry point of the `testtool` command.

import { runCli } from './cli.js';

process.exitCode = runCli(process.argv.slice(2), process.stdout, process.stderr);
