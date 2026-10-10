// Unit tests for createProject's files: the config, the .gitignore and the
// folders, a folder that is not empty, values the config would not accept,
// and the new project opening with no diagnostics.
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { PRODUCT } from '@cfe/protocol';
import { afterEach, describe, expect, it } from 'vitest';
import { BUILTIN_ACTIONS } from '../actions/builtins/index.js';
import { RpcError } from '../rpc/rpc-error.js';
import { createProjectFiles, newConfigText } from './create.js';
import { Project } from './project.js';

const folders: string[] = [];

function tempFolder(): string {
  const folder = mkdtempSync(join(tmpdir(), 'create-project-'));
  folders.push(folder);
  return folder;
}

afterEach(() => {
  for (const folder of folders.splice(0)) rmSync(folder, { recursive: true, force: true });
});

describe('createProjectFiles', () => {
  it('writes the config, the .gitignore and the folders, and the project opens cleanly', async () => {
    const root = join(tempFolder(), 'shop-tests');
    createProjectFiles({ root, name: 'Shop tests', baseUrl: 'http://localhost:5173' });
    expect(readdirSync(root).sort()).toEqual(
      ['.gitignore', 'actions', PRODUCT.configFile, 'flows', 'targets', 'tests'].sort(),
    );
    expect(readFileSync(join(root, PRODUCT.configFile), 'utf8')).toBe(
      [
        `# Shop tests: the ${PRODUCT.displayName} project configuration.`,
        'version: 1',
        "tests: ['tests/**/*.test.yaml']",
        "flows: ['flows/**/*.flow.yaml']",
        "targets: ['targets/**/*.targets.yaml']",
        "actions: ['actions/**/*.ts']",
        '',
        'defaults:',
        '  environment: local',
        '',
        'environments:',
        '  local:',
        '    baseUrl: http://localhost:5173',
        '',
      ].join('\n'),
    );
    const ignored = readFileSync(join(root, '.gitignore'), 'utf8').split('\n');
    expect(ignored).toContain(`${PRODUCT.dataDir}/`);
    expect(ignored).toContain('.env');
    const project = await Project.open(root, BUILTIN_ACTIONS, { environment: {} });
    expect(project.summary()).toMatchObject({
      environments: ['local'],
      defaultEnvironment: 'local',
      diagnostics: [],
    });
  });

  it('names the first environment as asked, in an existing empty folder', () => {
    const root = tempFolder();
    createProjectFiles({
      root,
      name: 'Staging',
      baseUrl: 'https://staging.example.com',
      environment: 'staging',
    });
    expect(readFileSync(join(root, PRODUCT.configFile), 'utf8')).toContain('  staging:\n');
  });

  it('refuses a folder that is not empty and names what is in it', () => {
    const root = tempFolder();
    writeFileSync(join(root, 'README.md'), '# hi\n');
    mkdirSync(join(root, 'src'));
    expect(() => createProjectFiles({ root, name: 'x', baseUrl: 'http://localhost' })).toThrow(
      new RpcError(
        'FolderNotEmpty',
        `"${root}" is not empty: it holds README.md, src/. Choose an empty folder, or a new one.`,
      ),
    );
  });

  it('refuses values the config would not accept, writing nothing', () => {
    const root = join(tempFolder(), 'bad');
    expect(() => createProjectFiles({ root, name: 'x', baseUrl: 'not a url' })).toThrow(
      /must be an absolute URL/,
    );
    expect(() =>
      createProjectFiles({ root, name: 'x', baseUrl: 'http://localhost', environment: '1st' }),
    ).toThrow('The environment name "1st" must start with a letter');
    expect(() => createProjectFiles({ root, name: ' ', baseUrl: 'http://localhost' })).toThrow(
      'Give the project a name.',
    );
    expect(() => readdirSync(root)).toThrow();
  });

  it('keeps the name on one line in the config', () => {
    expect(newConfigText('Two\nlines', 'http://localhost', 'local').split('\n')[0]).toBe(
      `# Two lines: the ${PRODUCT.displayName} project configuration.`,
    );
  });
});
