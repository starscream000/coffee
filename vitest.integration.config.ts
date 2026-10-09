// Vitest configuration for the browser tests (`pnpm test:integration`): only
// files named *.integration.test.ts, which need Playwright's Chromium.
import { defineConfig } from 'vitest/config';
import { PACKAGES } from './vitest.config.js';

export default defineConfig({
  test: {
    testTimeout: 30_000,
    hookTimeout: 60_000,
    projects: PACKAGES.map((name) => ({
      extends: true,
      test: {
        name: `@cfe/${name}`,
        root: `packages/${name}`,
        include: ['src/**/*.integration.test.ts'],
      },
    })),
  },
});
