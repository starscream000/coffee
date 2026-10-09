// The run folder of ADR 0015: `events.ndjson` (every event of the run, exactly
// as sent), `run.json` (written when the run starts and after each test) and
// one `tests/<nn>-<slug>/test.json` per test instance (written after each
// step). The JSON files are built from the masked events, so they are masked
// exactly as the protocol messages are, and no event carries file contents.

import { appendFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

/** Everything the run folder needs to know that no event carries. */
export interface RunFolderInfo {
  /** The engine's version. */
  readonly engineVersion: string;
  /** The protocol version it speaks. */
  readonly protocolVersion: string;
}

type Json = Record<string, unknown>;

interface TestRecord {
  readonly folder: string;
  readonly summary: Json;
  readonly result: Json & { steps: Json[]; logs: Json[] };
}

/**
 * The folder name of a test instance: its order in the run, then its file
 * name without extension, plus the row for tests with data rows.
 *
 * @param order - 1-based position in the run.
 * @param file - The test file's project-relative path.
 * @param row - The data row index, for tests with `data`.
 * @returns Such as `03-guest-checkout-row1`: only `[a-z0-9.~-]`, at most 40 characters.
 */
export function testFolderName(order: number, file: string, row?: number): string {
  const base = (file.split('/').at(-1) ?? file).replace(/\.test\.ya?ml$/i, '');
  const slug = `${base}${row === undefined ? '' : `-row${String(row)}`}`
    .toLowerCase()
    .replace(/[^a-z0-9.~-]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return `${String(order).padStart(2, '0')}-${slug}`.slice(0, 40).replace(/-+$/, '');
}

/**
 * Writes one run's folder as its events arrive.
 *
 * @example
 * ```ts
 * const folder = new RunFolder(resultsDir, { engineVersion, protocolVersion });
 * folder.record(session.notify('runStarted', params));
 * ```
 */
export class RunFolder {
  private readonly dir: string;
  private readonly info: RunFolderInfo;
  private run: Json = {};
  private readonly tests = new Map<string, TestRecord>();

  /**
   * @param dir - The run's folder; it must exist.
   * @param info - Versions for `run.json`.
   */
  constructor(dir: string, info: RunFolderInfo) {
    this.dir = dir;
    this.info = info;
  }

  /**
   * Records one event: appends its line to `events.ndjson` and updates the
   * JSON files it changes.
   *
   * @param line - The event exactly as sent to the client.
   */
  record(line: string): void {
    appendFileSync(join(this.dir, 'events.ndjson'), `${line}\n`);
    const message = JSON.parse(line) as { method?: unknown; params?: unknown };
    if (typeof message.method !== 'string' || typeof message.params !== 'object') return;
    this.apply(message.method, message.params as Json);
  }

  private apply(method: string, params: Json): void {
    const fields = omit(params, ['runId', 'seq']);
    const test = typeof params.testId === 'string' ? this.tests.get(params.testId) : undefined;
    switch (method) {
      case 'runStarted': {
        const tests = (fields.tests as Json[] | undefined) ?? [];
        this.run = {
          runId: params.runId,
          engineVersion: this.info.engineVersion,
          protocolVersion: this.info.protocolVersion,
          env: fields.env,
          browser: fields.browser,
          settings: fields.settings,
          startedAt: fields.startedAt,
          status: 'running',
          tests: tests.map((summary, index) => {
            const folder = `tests/${testFolderName(index + 1, String(summary.file), summary.row as number | undefined)}`;
            const record: TestRecord = {
              folder,
              summary: { ...summary, status: 'pending', folder },
              result: { ...summary, status: 'pending', steps: [], logs: [] },
            };
            this.tests.set(String(summary.testId), record);
            return record.summary;
          }),
        };
        this.writeRun();
        return;
      }
      case 'testStarted':
      case 'testSkipped':
      case 'testFinished':
        if (test === undefined) return;
        if (method === 'testStarted') {
          test.result.status = 'running';
          test.result.startedAt = fields.startedAt;
        } else if (method === 'testSkipped') {
          test.result.status = 'skipped';
          test.result.reason = fields.reason;
        } else {
          test.result.status = fields.status;
          test.result.durationMs = fields.durationMs;
        }
        test.summary.status = test.result.status;
        this.writeTest(test);
        if (method !== 'testStarted') this.writeRun();
        return;
      case 'stepStarted':
        if (test === undefined) return;
        test.result.steps.push({ ...withoutTest(fields), status: 'running' });
        this.writeTest(test);
        return;
      case 'stepPassed':
      case 'stepFailed': {
        const step = test?.result.steps.findLast((candidate) => candidate.stepId === fields.stepId);
        if (test === undefined || step === undefined) return;
        Object.assign(step, withoutTest(fields), {
          status: method === 'stepPassed' ? 'passed' : 'failed',
        });
        this.writeTest(test);
        return;
      }
      case 'stepSkipped':
        if (test === undefined) return;
        test.result.steps.push({ ...withoutTest(fields), status: 'skipped' });
        this.writeTest(test);
        return;
      case 'log':
        if (test === undefined) {
          const logs = (this.run.logs as Json[] | undefined) ?? [];
          this.run.logs = [...logs, fields];
          this.writeRun();
        } else {
          test.result.logs.push(withoutTest(fields));
          this.writeTest(test);
        }
        return;
      case 'runFinished':
        Object.assign(this.run, {
          status: fields.status,
          durationMs: fields.durationMs,
          totals: fields.totals,
          finishedAt: new Date().toISOString(),
        });
        this.writeRun();
        return;
      default:
        return;
    }
  }

  private writeRun(): void {
    writeFileSync(join(this.dir, 'run.json'), `${JSON.stringify(this.run, null, 2)}\n`);
  }

  private writeTest(test: TestRecord): void {
    const folder = join(this.dir, test.folder);
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, 'test.json'), `${JSON.stringify(test.result, null, 2)}\n`);
  }
}

/** A copy of an object without some keys. */
function omit(fields: Json, keys: readonly string[]): Json {
  return Object.fromEntries(Object.entries(fields).filter(([key]) => !keys.includes(key)));
}

/** An event's fields without the testId every test-scoped event repeats. */
function withoutTest(fields: Json): Json {
  return omit(fields, ['testId']);
}
