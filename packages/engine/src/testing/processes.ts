// Test helper: counts browser processes whose command line contains a marker.
// An engine started with its temporary folder (TEMP, TMP, TMPDIR) set to a
// folder named after the marker gives Chromium a profile folder under it, so
// the browser's main process carries the marker on its command line. When
// that process is gone, its helper processes go with it. Not part of the
// engine build.

import { execFileSync } from 'node:child_process';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

/** A temporary folder for an engine, and the marker its browser processes carry. */
export interface MarkedTemp {
  /** The folder; pass it as TEMP, TMP and TMPDIR. */
  readonly folder: string;
  /** The folder's name, which appears in the browser's command line. */
  readonly marker: string;
  /** The environment variables that point an engine at the folder. */
  readonly env: Readonly<Record<string, string>>;
}

/**
 * Makes a temporary folder whose name marks the browser processes started
 * under it.
 *
 * @returns The folder, its marker and the environment for the engine.
 */
export function markedTemp(): MarkedTemp {
  const folder = mkdtempSync(join(tmpdir(), 'cfe-browser-mark-'));
  return { folder, marker: basename(folder), env: { TEMP: folder, TMP: folder, TMPDIR: folder } };
}

/**
 * Counts running browser processes whose command line contains `marker`.
 *
 * @param marker - Text unique to one engine's browser.
 * @returns How many such processes are running.
 */
export function browserProcessCount(marker: string): number {
  if (process.platform === 'win32') {
    const script = `@(Get-CimInstance Win32_Process | Where-Object { $_.Name -like '*chrom*' -and $_.CommandLine -like '*${marker}*' }).Count`;
    const output = execFileSync(
      'powershell.exe',
      ['-NoProfile', '-NonInteractive', '-Command', script],
      {
        encoding: 'utf8',
      },
    );
    return Number(output.trim());
  }
  const output = execFileSync('ps', ['-A', '-o', 'args='], { encoding: 'utf8' });
  return output
    .split('\n')
    .filter((line) => line.includes(marker) && /chrom|headless_shell/i.test(line)).length;
}

/**
 * Waits until no browser process carries the marker any more.
 *
 * @param marker - Text unique to one engine's browser.
 * @param timeoutMs - How long to wait.
 * @returns The count when the wait ended: 0 unless processes were left behind.
 */
export async function waitForNoBrowser(marker: string, timeoutMs = 10_000): Promise<number> {
  const deadline = Date.now() + timeoutMs;
  let count = browserProcessCount(marker);
  while (count > 0 && Date.now() < deadline) {
    await new Promise((resolve) => setTimeout(resolve, 250));
    count = browserProcessCount(marker);
  }
  return count;
}
