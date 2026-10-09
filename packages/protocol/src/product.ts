// The product's identity: the single place to rename the product. The display
// name, command name, data folder name and npm scope are independent values,
// because a good display name does not always make a safe command or folder
// name. product.test.ts fails if a file that cannot import this module (such as
// .gitignore or a package.json) is out of step with it.

/**
 * The product's names. Use these instead of writing a name literally anywhere
 * in the code.
 *
 * The unscoped npm package `cfe` belongs to someone else: always refer to the
 * scoped packages (`@cfe/cli`), never to `cfe` alone, in anything users run.
 *
 * @example
 * ```ts
 * import { PRODUCT } from '@cfe/protocol';
 * const configPath = path.join(root, PRODUCT.configFile);
 * ```
 */
export const PRODUCT = {
  /** Name shown to people: help text, window titles, documentation. */
  displayName: 'Coffee',
  /** Name of the command-line program. Lower case, letters, digits and `-`. */
  command: 'cfe',
  /** Git-ignored folder in the user's repository for runs, logins and caches. */
  dataDir: '.cfe',
  /** npm scope of every package in this repository, including the user-facing SDK. */
  npmScope: '@cfe',
  /** Project configuration file at the root of the user's repository. */
  get configFile(): string {
    return `${this.command}.config.yaml`;
  },
  /** Prefix of the product's own environment variables, such as `CFE_ENGINE`. */
  get envPrefix(): string {
    return `${this.command.toUpperCase().replaceAll('-', '_')}_`;
  },
} as const;
