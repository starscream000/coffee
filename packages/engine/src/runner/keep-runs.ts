// `keepRuns` (ADR 0015): at the start of each run, before the new run folder
// is created, the oldest run folders are deleted until at most keepRuns - 1
// remain, so that the new run makes keepRuns. Only folders named like a run
// id are touched; 0 keeps every run. A folder that cannot be deleted is
// skipped, reported, and tried again at the next run.

import { readdirSync, rmSync, statSync } from 'node:fs';
import { join } from 'node:path';

/** A run id: UTC date and time, then 4 random hex digits. */
export const RUN_ID_PATTERN = /^\d{8}-\d{6}-[0-9a-f]{4}$/;

/** The default of `defaults.keepRuns`. */
export const DEFAULT_KEEP_RUNS = 20;

/** What {@link pruneRuns} did. */
export interface PruneResult {
  /** Run folders deleted, oldest first. */
  readonly removed: readonly string[];
  /** Run folders that could not be deleted, with the reason. */
  readonly failed: readonly { readonly runId: string; readonly reason: string }[];
}

/**
 * Deletes the oldest run folders so a new run makes `keepRuns`.
 *
 * @param runsDir - The `runs` folder; it may not exist yet.
 * @param keepRuns - How many runs to keep, the new one included; 0 keeps all.
 * @param remove - Deletes one folder; replaceable for tests.
 * @returns The folders deleted and those that could not be.
 *
 * @example
 * ```ts
 * pruneRuns(join(root, '.cfe', 'runs'), 20);
 * ```
 */
export function pruneRuns(
  runsDir: string,
  keepRuns: number,
  remove: (folder: string) => void = (folder) => {
    rmSync(folder, { recursive: true });
  },
): PruneResult {
  if (keepRuns <= 0) return { removed: [], failed: [] };
  let names: string[];
  try {
    names = readdirSync(runsDir);
  } catch {
    return { removed: [], failed: [] };
  }
  const runs = names
    .filter((name) => RUN_ID_PATTERN.test(name) && statSync(join(runsDir, name)).isDirectory())
    .sort();
  const excess = runs.slice(0, Math.max(0, runs.length - (keepRuns - 1)));
  const removed: string[] = [];
  const failed: { runId: string; reason: string }[] = [];
  for (const runId of excess) {
    try {
      remove(join(runsDir, runId));
      removed.push(runId);
    } catch (error) {
      failed.push({ runId, reason: error instanceof Error ? error.message : String(error) });
    }
  }
  return { removed, failed };
}
