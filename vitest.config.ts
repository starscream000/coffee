// Root Vitest configuration: runs the unit tests of every workspace package.
// Browser tests (*.integration.test.ts) run only with vitest.integration.config.ts,
// so `pnpm verify` needs no browser.
import { defineConfig } from 'vitest/config';

/** The workspace packages; each is a Vitest project that inherits this file's settings. */
export const PACKAGES = ['protocol', 'engine', 'cli'];

export default defineConfig({
  test: {
    projects: PACKAGES.map((name) => ({
      extends: true,
      test: {
        name: `@cfe/${name}`,
        root: `packages/${name}`,
        include: ['src/**/*.test.ts'],
        exclude: ['src/**/*.integration.test.ts'],
      },
    })),
  },
});
