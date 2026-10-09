// Review 0006, finding 1: an event is in the run folder before it is sent.
// The run manager runs in this process with an event channel that looks at
// the run folder at the moment each event is sent: events.ndjson must already
// end with that event, and when runFinished is sent, run.json and every
// test.json must be complete.
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { BUILTIN_ACTIONS } from '../actions/builtins/index.js';
import { SecretRegistry } from '../context/mask.js';
import { Project } from '../project/project.js';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { RunManager } from './run.js';

let app: DemoApp;

beforeAll(async () => {
  app = await startDemoApp({ env: { DEMO_PASSWORD: 'demo-password-1234' } });
});
afterAll(async () => {
  await app.close();
});

type Json = Record<string, unknown>;

function readJson(path: string): Json {
  return JSON.parse(readFileSync(path, 'utf8')) as Json;
}

describe('the run folder is written before each event is sent', () => {
  it('holds every event, and is complete when runFinished is sent', async () => {
    const project = await Project.open(app.root, BUILTIN_ACTIONS, {
      secrets: new SecretRegistry(),
      environment: { DEMO_PASSWORD: 'demo-password-1234' },
    });
    let resultsDir = '';
    const problems: string[] = [];
    let sent = 0;
    let finished: () => void = () => undefined;
    const done = new Promise<void>((resolve) => {
      finished = resolve;
    });

    const runs = new RunManager(
      {
        render: (method, params) => JSON.stringify({ jsonrpc: '2.0', method, params }),
        send: (line) => {
          sent += 1;
          const events = join(resultsDir, 'events.ndjson');
          const lines = existsSync(events)
            ? readFileSync(events, 'utf8').trimEnd().split('\n')
            : [];
          if (lines.at(-1) !== line) {
            problems.push(`event ${String(sent)} was sent before events.ndjson held it`);
          }
          const message = JSON.parse(line) as { method: string };
          if (message.method !== 'runFinished') return;
          const run = readJson(join(resultsDir, 'run.json'));
          if (run.status === 'running' || run.totals === undefined) {
            problems.push(`run.json was not complete: status ${String(run.status)}`);
          }
          for (const test of run.tests as { folder: string }[]) {
            const status = readJson(join(resultsDir, test.folder, 'test.json')).status;
            if (status === 'running' || status === 'pending') {
              problems.push(`${test.folder}/test.json was not complete: status ${status}`);
            }
          }
          finished();
        },
      },
      { engineVersion: '0.0.0', protocolVersion: '0.1.0' },
    );
    const result = await runs.start(project, {
      files: ['tests/data-rows.test.yaml', 'tests/skipped.test.yaml'],
    });
    resultsDir = result.resultsDir;
    await done;
    expect(sent).toBeGreaterThan(20);
    expect(problems).toEqual([]);
  });
});
