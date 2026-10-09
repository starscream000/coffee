// The browsers this engine can run (`capabilities.browsers`) and how to start
// one. v0.1.0 runs Chromium only, through the engine's own copy of Playwright.
// Whether Chromium is installed is read from the files Playwright installs,
// without starting a browser, so `initialize` stays fast.

import { existsSync } from 'node:fs';
import { createRequire } from 'node:module';
import { basename, dirname, join } from 'node:path';
import { chromium, type Browser } from 'playwright';

const require = createRequire(import.meta.url);

/** The browser names this engine knows; `capabilities.browsers` lists the installed ones. */
export const KNOWN_BROWSERS = ['chromium'] as const;

/** The exact Playwright version the engine uses. */
export const PLAYWRIGHT_VERSION = (require('playwright/package.json') as { version: string })
  .version;

/** The command that installs the browser this engine's Playwright needs. */
export const INSTALL_COMMAND = `npx playwright@${PLAYWRIGHT_VERSION} install chromium`;

/**
 * Tells whether Playwright's Chromium is installed: the full browser, or the
 * headless shell that headless runs use.
 *
 * Playwright names the folder of the full browser `chromium-<revision>` and the
 * headless shell `chromium_headless_shell-<revision>`, side by side, and writes
 * `INSTALLATION_COMPLETE` into each when its download has finished.
 *
 * @returns True when a run can start Chromium.
 */
export function chromiumInstalled(): boolean {
  const executable = chromium.executablePath();
  if (existsSync(executable)) return true;
  // <browsers>/chromium-<rev>/<platform folder>/<executable>
  const browserFolder = dirname(dirname(executable));
  const revision = /^chromium-(\d+)$/.exec(basename(browserFolder))?.[1];
  if (revision === undefined) return false;
  const shellFolder = join(dirname(browserFolder), `chromium_headless_shell-${revision}`);
  return existsSync(join(shellFolder, 'INSTALLATION_COMPLETE'));
}

/**
 * The browsers that can run now, for `capabilities.browsers`.
 *
 * @returns `["chromium"]` when Chromium is installed, otherwise `[]`.
 */
export function availableBrowsers(): string[] {
  return chromiumInstalled() ? ['chromium'] : [];
}

/**
 * Starts the browser for one run.
 *
 * @param headed - Show the browser window (`options.headed`).
 * @returns The running browser.
 * @throws Error from Playwright when the browser cannot start, for example a
 *   headed run with only the headless shell installed.
 */
export function launchBrowser(headed: boolean): Promise<Browser> {
  return chromium.launch({ headless: !headed });
}
