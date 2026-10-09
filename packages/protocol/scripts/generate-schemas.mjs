// Writes one JSON Schema file per protocol message into packages/protocol/schema/
// from the built package (run `tsc -b` first; `pnpm generate:schemas` does both).
// Files that no longer correspond to a message are deleted.

import { mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { schemaFileContents } from '../dist/schema-files.js';

const outDir = new URL('../schema/', import.meta.url);
mkdirSync(outDir, { recursive: true });

const files = schemaFileContents();
for (const name of readdirSync(outDir)) {
  if (name.endsWith('.json') && !files.has(name)) {
    rmSync(new URL(name, outDir));
  }
}
for (const [name, text] of files) {
  writeFileSync(new URL(name, outDir), text);
}
process.stdout.write(`Wrote ${String(files.size)} schema files to packages/protocol/schema/\n`);
