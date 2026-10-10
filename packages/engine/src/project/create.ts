// `createProject` (docs/protocol.md, "createProject"; docs/step-format.md,
// "Project configuration"): a new project in a folder that is empty or does
// not exist yet. It writes a minimal valid config with one environment, the
// folders the config's globs name, and a .gitignore that leaves out the data
// folder and .env. Every name comes from PRODUCT.

import { existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { PRODUCT, type CreateProjectParams } from '@cfe/protocol';
import { stringify } from 'yaml';
import { ConfigFileSchema } from '../schema/files.js';
import { RpcError } from '../rpc/rpc-error.js';

/** The folders a new project gets, and the glob the config names for each. */
export const PROJECT_FOLDERS = {
  tests: 'tests/**/*.test.yaml',
  flows: 'flows/**/*.flow.yaml',
  targets: 'targets/**/*.targets.yaml',
  actions: 'actions/**/*.ts',
} as const;

/** How many entries of a folder that is not empty the error names. */
const NAMED_ENTRIES = 5;

/**
 * The text of a new project's config file.
 *
 * @param name - The project's name, written in the first comment line.
 * @param baseUrl - The first environment's base URL.
 * @param environment - The first environment's name.
 * @returns YAML text, ending with a newline.
 */
export function newConfigText(name: string, baseUrl: string, environment: string): string {
  const scalar = (value: string): string => stringify(value, { lineWidth: 0 }).trimEnd();
  const title = name.replace(/\s+/g, ' ').trim();
  return [
    `# ${title}: the ${PRODUCT.displayName} project configuration.`,
    'version: 1',
    `tests: ['${PROJECT_FOLDERS.tests}']`,
    `flows: ['${PROJECT_FOLDERS.flows}']`,
    `targets: ['${PROJECT_FOLDERS.targets}']`,
    `actions: ['${PROJECT_FOLDERS.actions}']`,
    '',
    'defaults:',
    `  environment: ${scalar(environment)}`,
    '',
    'environments:',
    `  ${scalar(environment)}:`,
    `    baseUrl: ${scalar(baseUrl)}`,
    '',
  ].join('\n');
}

/**
 * The text of a new project's .gitignore: the data folder and .env.
 *
 * @returns Text, ending with a newline.
 */
export function newGitignoreText(): string {
  return [
    `# ${PRODUCT.displayName}'s data folder: runs, saved logins and caches.`,
    `${PRODUCT.dataDir}/`,
    '# Secrets for this machine; never commit them.',
    '.env',
    '',
  ].join('\n');
}

/** The entries of a folder, folders with a trailing slash, sorted. */
function entriesOf(folder: string): string[] {
  return readdirSync(folder)
    .map((name) => (statSync(join(folder, name)).isDirectory() ? `${name}/` : name))
    .sort();
}

/**
 * Creates a project's files in an empty or new folder. The caller opens it.
 *
 * @param params - `root`, `name`, `baseUrl` and the optional first environment's name.
 * @returns The project's root folder, absolute.
 * @throws RpcError `FolderNotEmpty` naming what is in the way; `InvalidParams`
 *   for a root that is a file, an empty name, or a base URL or environment
 *   name the config would not accept.
 */
export function createProjectFiles(params: CreateProjectParams): string {
  const root = resolve(params.root);
  const environment = params.environment ?? 'local';
  if (params.name.trim() === '') {
    throw new RpcError('InvalidParams', 'Give the project a name.');
  }
  // The config's own rule for environment names, said plainly.
  if (!/^[A-Za-z][A-Za-z0-9_-]*$/.test(environment)) {
    throw new RpcError(
      'InvalidParams',
      `The environment name "${environment}" must start with a letter and hold only letters, digits, - and _.`,
    );
  }
  if (existsSync(root)) {
    if (!statSync(root).isDirectory()) {
      throw new RpcError('InvalidParams', `"${root}" is a file, not a folder.`);
    }
    const entries = entriesOf(root);
    if (entries.length > 0) {
      const named = entries.slice(0, NAMED_ENTRIES).join(', ');
      const more =
        entries.length > NAMED_ENTRIES ? ` and ${String(entries.length - NAMED_ENTRIES)} more` : '';
      throw new RpcError(
        'FolderNotEmpty',
        `"${root}" is not empty: it holds ${named}${more}. Choose an empty folder, or a new one.`,
      );
    }
  }
  const config = newConfigText(params.name, params.baseUrl, environment);
  const checked = ConfigFileSchema.safeParse({
    version: 1,
    defaults: { environment },
    environments: { [environment]: { baseUrl: params.baseUrl } },
  });
  if (!checked.success) {
    throw new RpcError(
      'InvalidParams',
      `The project cannot be created with these values: ${checked.error.issues
        .map((issue) => `${issue.path.join('.')}: ${issue.message}`)
        .join('; ')}.`,
    );
  }
  mkdirSync(root, { recursive: true });
  writeFileSync(join(root, PRODUCT.configFile), config);
  writeFileSync(join(root, '.gitignore'), newGitignoreText());
  for (const folder of Object.keys(PROJECT_FOLDERS)) {
    mkdirSync(join(root, folder), { recursive: true });
  }
  return root;
}
