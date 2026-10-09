// Root Vitest configuration: runs the unit tests of every workspace package.
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    projects: ['packages/*'],
    include: ['src/**/*.test.ts'],
  },
});
