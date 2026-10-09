// Test helper: finds the browser processes an engine started, by walking the
// process tree down from the engine's process id, and checks later whether
// those processes are gone. Process ids are recorded while the engine runs,
// because once it exits its children are given to another parent and can no
// longer be found through it. Not part of the engine build.

import { execFileSync } from 'node:child_process';

interface ProcessInfo {
  readonly pid: number;
  readonly ppid: number;
  readonly command: string;
}

/** Every running process with its parent and its command. */
function listProcesses(): ProcessInfo[] {
  if (process.platform === 'win32') {
    const script =
      'Get-CimInstance Win32_Process | ForEach-Object { "$($_.ProcessId)`t$($_.ParentProcessId)`t$($_.Name)" }';
    const output = execFileSync(
      'powershell.exe',
      ['-NoProfile', '-NonInteractive', '-Command', script],
      { encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 },
    );
    return output.split(/\r?\n/).flatMap((line) => {
      const [pid, ppid, command = ''] = line.split('\t');
      return pid === undefined || ppid === undefined || pid === ''
        ? []
        : [{ pid: Number(pid), ppid: Number(ppid), command }];
    });
  }
  const output = execFileSync('ps', ['-A', '-o', 'pid=,ppid=,comm='], {
    encoding: 'utf8',
    maxBuffer: 16 * 1024 * 1024,
  });
  return output.split('\n').flatMap((line) => {
    const match = /^\s*(\d+)\s+(\d+)\s+(.*)$/.exec(line);
    return match === null
      ? []
      : [{ pid: Number(match[1]), ppid: Number(match[2]), command: match[3] ?? '' }];
  });
}

/**
 * The ids of the browser processes below a process: Chromium's main process
 * and its helpers.
 *
 * @param rootPid - The engine's process id.
 * @returns Process ids whose command names Chromium or its headless shell.
 */
export function browserProcessIds(rootPid: number): number[] {
  const all = listProcesses();
  const below = new Set<number>([rootPid]);
  // Parents do not always come before their children in the listing, so
  // repeat until nothing is added.
  for (let grown = true; grown;) {
    grown = false;
    for (const info of all) {
      if (!below.has(info.pid) && below.has(info.ppid)) {
        below.add(info.pid);
        grown = true;
      }
    }
  }
  return all
    .filter((info) => info.pid !== rootPid && below.has(info.pid))
    .filter((info) => /chrom|headless_shell/i.test(info.command))
    .map((info) => info.pid);
}

/** Whether a process with this id is running. */
function isRunning(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    // EPERM: it exists but belongs to someone else.
    return (error as NodeJS.ErrnoException).code === 'EPERM';
  }
}

/**
 * Waits until none of the given processes is running.
 *
 * @param pids - Process ids recorded earlier.
 * @param timeoutMs - How long to wait.
 * @returns The ids still running when the wait ended: empty unless processes
 *   were left behind.
 */
export async function waitUntilGone(
  pids: readonly number[],
  timeoutMs = 10_000,
): Promise<number[]> {
  const deadline = Date.now() + timeoutMs;
  let alive = pids.filter(isRunning);
  while (alive.length > 0 && Date.now() < deadline) {
    await new Promise((resolve) => setTimeout(resolve, 250));
    alive = alive.filter(isRunning);
  }
  return alive;
}
