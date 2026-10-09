# 0015. Write each run to its own folder with a fixed layout

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

Each run produces results, screenshots and page snapshots. Clients (CLI,
desktop app, later the server) need to find them, and paths must work on
Windows, macOS and Linux.

## Decision

Everything the engine writes lives in the git-ignored data folder
(`PRODUCT.dataDir`, `.coffee/`) at the project root:

```
.coffee/
  logins/.key                           HMAC key for login cache keys (ADR 0018)
  logins/<key>.json                     saved login storage state
  logins/<key>.meta.json                createdAt, env and login name; no parameter values
  cache/actions/<hash>.mjs              compiled user actions (+ .map)
  runs/
    <runId>/                            runId: 20261009-054902-1a2b (UTC time + 4 random hex)
      run.json                          summary: versions, env, browser, times, totals,
                                        tests with status and their folder
      events.ndjson                     every protocol event of the run, in order, masked
      tests/
        <nn>-<slug>/                    nn: order in the run (01, 02, …); slug: file name
                                        without extension + row, max 40 chars
                                        e.g. 03-guest-checkout-row1
          test.json                     test result: steps, statuses, errors, locators
          steps/
            <stepKey>/                  stepId with "/" replaced by "~", e.g. steps.4~steps.1
              screenshot.png            one per step (of the step's page)
              pagestate/                owned by the PageStateRecorder (format hidden),
                                        e.g. trace.zip
```

Rules:

- `run.json` is created when the run starts and rewritten after each test;
  `test.json` is rewritten after each step. A crashed run still has partial
  results.
- `events.ndjson` is the complete, replayable record of the run; a client can
  rebuild any view from it.
- All files are written through the masking writer ([ADR 0014](0014-secret-masking.md)).
- Folder names use only `[a-z0-9.~-]`, and a step's path stays under
  160 characters from the project root so it fits Windows' 260-character
  limit in typical locations.
- **Keep the last N runs.** The setting `keepRuns` in the config's `defaults`
  (default **20**; `0` keeps all runs) is applied **at the start of each run**,
  before the new run folder is created: the engine lists `runs/`, sorts by
  `runId` (which sorts by time), and deletes the oldest folders until at most
  `keepRuns - 1` remain, so that the new run makes `keepRuns`. It deletes only
  folders whose name is a valid `runId`; anything else in `runs/` is left
  alone. A folder that cannot be deleted (for example because a file is open)
  is skipped with a `warn` log and retried at the next run. The run itself never
  fails because of clean-up.

## Alternatives rejected

- **Folders named after full file paths**: too long on Windows, and characters
  differ per OS.
- **One results database (SQLite)**: harder to inspect, attach to CI artifacts
  or delete by hand; can be added as an index later.
- **Results outside the repository (user profile folder)**: harder to find, and
  CI systems collect artifacts from the workspace.
- **Clean-up at the end of a run**: a crashed or cancelled run would skip it,
  and the folder would grow without bound.
- **Clean-up by age instead of count**: a project run rarely would lose all its
  history.

## Consequences

A run folder can be zipped and attached to a CI job as is. Clients get absolute
paths in events and never need to build them.

## Revisit when

The server (v0.4.0) needs results in object storage, or users regularly hit
path-length errors on Windows.
