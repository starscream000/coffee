// Integration tests of saved logins (plan branch 10, part 2; ADR 0018) through
// the demo app harness: two logins on two pages and the cache reused on the
// next run (S8), freshLogin (S18), refreshLogins, maxAge, the files under
// .cfe/logins/ (I11), and clean-up at the start of a run. The demo app counts
// successful sign-ins at GET /api/logins, which shows whether a flow ran.
import { mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { startDemoApp, type DemoApp } from '../testing/demo-app.js';
import { collectRun, eventsOf, type CollectedRun } from '../testing/run.js';
import { HELD_LOGIN_FILE_VARIABLE } from './logins.js';

const PASSWORD = 'demo-password-1234';

async function run(
  app: DemoApp,
  id: number,
  params: Record<string, unknown>,
): Promise<CollectedRun> {
  const collected = await collectRun(app.engine, id, params);
  expect(collected.invalid).toEqual([]);
  expect(collected.response).toHaveProperty('result');
  return collected;
}

async function loginCount(app: DemoApp): Promise<number> {
  const response = await fetch(`${app.url}/api/logins`);
  return ((await response.json()) as { logins: number }).logins;
}

function loginsDir(app: DemoApp): string {
  return join(app.root, '.cfe', 'logins');
}

function stateFiles(app: DemoApp): string[] {
  return readdirSync(loginsDir(app))
    .filter((name) => name.endsWith('.json') && !name.endsWith('.meta.json'))
    .sort();
}

function status(collected: CollectedRun): unknown {
  return eventsOf(collected.events, 'runFinished')[0]?.status;
}

describe('saved logins', () => {
  let app: DemoApp;
  let nextId = 700;

  beforeAll(async () => {
    app = await startDemoApp({ env: { DEMO_PASSWORD: PASSWORD } });
  });
  afterAll(async () => {
    await app.close();
  });

  it('S8: two logins on two pages sign in once; the next run reuses the saved states', async () => {
    const before = await loginCount(app);
    const first = await run(app, nextId++, { files: ['tests/multi-user.test.yaml'] });
    expect(status(first)).toBe('passed');
    expect(await loginCount(app)).toBe(before + 2);
    expect(eventsOf(first.events, 'log').map((event) => event.message)).toEqual([
      'Signed in with the login "customer" and saved it for 12h.',
      'Signed in with the login "admin" and saved it for 2h.',
    ]);

    const second = await run(app, nextId++, { files: ['tests/multi-user.test.yaml'] });
    expect(status(second)).toBe('passed');
    expect(await loginCount(app)).toBe(before + 2);
    expect(eventsOf(second.events, 'log').map((event) => event.message)).toEqual([
      expect.stringMatching(/^Reused the saved login "customer" from /),
      expect.stringMatching(/^Reused the saved login "admin" from /),
    ]);
  });

  it('I11: the cache files are named by hashes and hold no login parameter value', () => {
    const names = readdirSync(loginsDir(app)).sort();
    expect(names).toContain('.key');
    const states = stateFiles(app);
    expect(states).toHaveLength(2);
    for (const name of states) {
      expect(name).toMatch(/^[0-9a-f]{64}\.json$/);
      expect(names).toContain(name.replace(/\.json$/, '.meta.json'));
    }
    for (const name of names) {
      const text = readFileSync(join(loginsDir(app), name), 'utf8');
      for (const value of ['alice', 'ada', PASSWORD]) {
        expect(text, `${name} holds "${value}"`).not.toContain(value);
      }
    }
  });

  it('S18: freshLogin signs in again and neither reads nor changes the saved state', async () => {
    const files = stateFiles(app).map((name) => join(loginsDir(app), name));
    const saved = files.map((file) => ({
      text: readFileSync(file, 'utf8'),
      mtime: statSync(file).mtimeMs,
    }));
    const before = await loginCount(app);
    const collected = await run(app, nextId++, { files: ['tests/fresh-login.test.yaml'] });
    expect(status(collected)).toBe('passed');
    expect(await loginCount(app)).toBe(before + 1);
    expect(eventsOf(collected.events, 'log').map((event) => event.message)).toEqual([
      'Signed in with the login "customer" for this test only (freshLogin).',
    ]);
    expect(stateFiles(app).map((name) => join(loginsDir(app), name))).toEqual(files);
    expect(
      files.map((file) => ({ text: readFileSync(file, 'utf8'), mtime: statSync(file).mtimeMs })),
    ).toEqual(saved);
  });

  it('refreshLogins ignores the saved states for one run and replaces them', async () => {
    const before = await loginCount(app);
    const collected = await run(app, nextId++, {
      files: ['tests/multi-user.test.yaml'],
      options: { refreshLogins: true },
    });
    expect(status(collected)).toBe('passed');
    expect(await loginCount(app)).toBe(before + 2);
    expect(stateFiles(app)).toHaveLength(2);
  });

  it('I11: after maxAge the flow runs again', async () => {
    const config = join(app.root, 'cfe.config.yaml');
    const original = readFileSync(config, 'utf8');
    writeFileSync(config, original.replace('    maxAge: 2h\n', '    maxAge: 1s\n'));
    expect(await app.engine.request(nextId++, 'openProject', { root: app.root })).toHaveProperty(
      'result',
    );
    try {
      await run(app, nextId++, { files: ['tests/multi-user.test.yaml'] });
      await new Promise((resolve) => setTimeout(resolve, 1_200));
      const before = await loginCount(app);
      const collected = await run(app, nextId++, { files: ['tests/multi-user.test.yaml'] });
      expect(status(collected)).toBe('passed');
      // admin (maxAge 1s) signs in again; customer (12h) is reused.
      expect(await loginCount(app)).toBe(before + 1);
    } finally {
      writeFileSync(config, original);
      await app.engine.request(nextId++, 'openProject', { root: app.root });
    }
  });

  it('a failing login flow fails the step that needs it, with the flow step error and location', async () => {
    const other = await startDemoApp({ env: { DEMO_PASSWORD: PASSWORD } });
    try {
      // An unknown user cannot sign in, so the flow's last check fails.
      const config = join(other.root, 'cfe.config.yaml');
      writeFileSync(
        config,
        readFileSync(config, 'utf8')
          .replace('user: alice', 'user: mallory')
          .replace('defaults:\n', 'defaults:\n  timeout: 2s\n'),
      );
      writeFileSync(
        join(other.root, 'tests', 'bad-login.test.yaml'),
        'version: 1\nname: Bad login\nlogin: customer\nsteps:\n  - goto: /account\n',
      );
      expect(await other.engine.request(2, 'openProject', { root: other.root })).toHaveProperty(
        'result',
      );
      const collected = await run(other, 3, { files: ['tests/bad-login.test.yaml'] });
      const failed = eventsOf(collected.events, 'stepFailed')[0];
      expect(failed).toMatchObject({
        stepId: 'steps.0',
        error: {
          code: 'TargetNotFound',
          location: { file: 'flows/login.flow.yaml', line: 12, column: 5 },
        },
      });
      expect((failed?.error as { message: string }).message).toMatch(
        /^The saved login "customer" could not sign in: /,
      );
      // Nothing is saved for a login that failed.
      const saved = join(other.root, '.cfe', 'logins');
      expect(
        readdirSync(join(other.root, '.cfe')).includes('logins')
          ? readdirSync(saved).filter((name) => name !== '.key')
          : [],
      ).toEqual([]);
    } finally {
      await other.close();
    }
  });
});

describe('clean-up of saved logins at the start of a run', () => {
  it('deletes stale states, leaves current ones, and reports one it cannot delete', async () => {
    const app = await startDemoApp({
      env: { DEMO_PASSWORD: PASSWORD, [HELD_LOGIN_FILE_VARIABLE]: 'held.json' },
    });
    try {
      const dir = loginsDir(app);
      mkdirSync(dir, { recursive: true });
      const meta = (login: string, env: string, createdAt: string): string =>
        JSON.stringify({ createdAt, env, login, engineVersion: '0.0.0' });
      const now = new Date().toISOString();
      const old = new Date(Date.now() - 3 * 3_600_000).toISOString();
      const files: Record<string, string> = {
        '.key': 'a'.repeat(64),
        'current.json': '{}',
        'current.meta.json': meta('customer', 'local', now),
        'expired.json': '{}',
        'expired.meta.json': meta('admin', 'local', old), // admin's maxAge is 2h
        'removed-login.json': '{}',
        'removed-login.meta.json': meta('ghost', 'local', now),
        'removed-env.json': '{}',
        'removed-env.meta.json': meta('customer', 'staging', now),
        'no-meta.json': '{}',
        'held.json': '{}',
      };
      for (const [name, text] of Object.entries(files)) writeFileSync(join(dir, name), text);

      const collected = await run(app, 2, { files: ['tests/skipped.test.yaml'] });
      expect(status(collected)).toBe('passed');
      expect(readdirSync(dir).sort()).toEqual([
        '.key',
        'current.json',
        'current.meta.json',
        'held.json',
      ]);
      expect(eventsOf(collected.events, 'log')).toEqual([
        expect.objectContaining({
          level: 'warn',
          code: 'LoginCleanupFailed',
          message: expect.stringContaining('held.json could not be deleted') as string,
        }),
      ]);
    } finally {
      await app.close();
    }
  });
});
