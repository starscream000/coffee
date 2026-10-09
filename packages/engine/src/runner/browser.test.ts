// Unit tests for finding Playwright's Chromium without starting it, with the
// folder layouts of Windows, Linux and macOS (where the executable lies deep
// inside Chromium.app).
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { chromiumInstalledAt } from './browser.js';

const BROWSERS = join('/home', 'runner', 'ms-playwright');
const EXECUTABLES = {
  windows: join(BROWSERS, 'chromium-1248', 'chrome-win64', 'chrome.exe'),
  linux: join(BROWSERS, 'chromium-1248', 'chrome-linux', 'chrome'),
  macos: join(
    BROWSERS,
    'chromium-1248',
    'chrome-mac-arm64',
    'Chromium.app',
    'Contents',
    'MacOS',
    'Chromium',
  ),
};
const SHELL_DONE = join(BROWSERS, 'chromium_headless_shell-1248', 'INSTALLATION_COMPLETE');

describe('chromiumInstalledAt', () => {
  it.each(Object.entries(EXECUTABLES))(
    '%s: finds the full browser, or the headless shell beside it',
    (_system, executable) => {
      expect(chromiumInstalledAt(executable, (path) => path === executable)).toBe(true);
      expect(chromiumInstalledAt(executable, (path) => path === SHELL_DONE)).toBe(true);
      expect(chromiumInstalledAt(executable, () => false)).toBe(false);
    },
  );

  it('does not accept a headless shell of another revision', () => {
    const other = join(BROWSERS, 'chromium_headless_shell-1200', 'INSTALLATION_COMPLETE');
    expect(chromiumInstalledAt(EXECUTABLES.macos, (path) => path === other)).toBe(false);
  });
});
