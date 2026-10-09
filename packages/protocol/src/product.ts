// The product's identity. This is the single place to rename the product:
// every user-visible name (command, config file, data folder, environment
// variables) is derived from PRODUCT_ID and PRODUCT_NAME below.
// product.test.ts fails if a file that cannot import this module (such as
// .gitignore or the CLI's package.json) is out of step with it.

/**
 * Display name of the product. Placeholder until the owner supplies the final
 * name.
 */
export const PRODUCT_NAME = 'Coffee';

/**
 * Lower-case identifier of the product, used to derive command, file and folder
 * names. Must match `/^[a-z][a-z0-9-]*$/`.
 */
export const PRODUCT_ID = 'coffee';

/**
 * Names derived from {@link PRODUCT_ID}. Use these instead of writing a name
 * literally anywhere in the code.
 *
 * @example
 * ```ts
 * import { PRODUCT } from '@test-tool/protocol';
 * const configPath = path.join(root, PRODUCT.configFile); // "coffee.config.yaml"
 * ```
 */
export const PRODUCT = {
  /** Display name, for example in help text. */
  name: PRODUCT_NAME,
  /** Name of the command-line program. */
  command: PRODUCT_ID,
  /** Project configuration file at the root of the user's repository. */
  configFile: `${PRODUCT_ID}.config.yaml`,
  /** Git-ignored folder in the user's repository for runs, logins and caches. */
  dataDir: `.${PRODUCT_ID}`,
  /** Prefix of the product's own environment variables, such as `COFFEE_ENGINE`. */
  envPrefix: `${PRODUCT_ID.toUpperCase().replaceAll('-', '_')}_`,
} as const;
