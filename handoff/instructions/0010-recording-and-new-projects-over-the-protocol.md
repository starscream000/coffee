# Instruction 0010: recording and new projects over the protocol

- Date: 2026-10-10
- Written by: reviewer
- Track: engine
- Based on `main` at: `8fc884e` or later
- Replaces: none
- Follows review: [0009](../reviews/0009-the-recorder-first-part.md)

## Goal

A client can do over the protocol what `pnpm record` does in a terminal: start
a recording, receive each step as it is recorded, stop, and verify. A client
can also create a new project. With this, the desktop app can offer recording
and "New project" (its instruction D0004, which comes straight after this
one).

## Owner decisions

1. Pull requests #48 to #52 are merged. The recorder's first part is on
   `main`.
2. **One implementer now does both tracks** (2026-10-10). The desktop
   implementer is gone; you carry on its work. **The two tracks stay
   separate**: their instructions, reports, branches and scopes do not mix.
   - An engine instruction (`NNNN`, in `handoff/`) changes nothing under
     `apps/desktop/`. Its report goes in `handoff/reports/`.
   - A desktop instruction (`DNNNN`, in `apps/desktop/handoff/`) changes
     nothing outside `apps/desktop/`. Its report goes in
     `apps/desktop/handoff/reports/`. Its branches are named
     `desktop/<type>/<name>`.
   - When both tracks have an open instruction, the root `handoff/STATUS.md`
     says which comes first. Finish one, with its report and pull requests,
     before starting the other.
3. The desktop app must now be able to use everything the engine has,
   recording included. That is why this instruction is about the protocol.

## Reviewer decisions

- **R11. Compatible additions raise the patch number while the protocol is
  `0.x`.** New methods, events and optional fields make the protocol `0.1.1`.
  A `0.1.0` client keeps working with a `0.1.1` engine: the compatibility rule
  (major and minor equal) is unchanged. Record this in ADR 0011.
- **R12. A protocol change is two stacked pull requests: engine first, desktop
  second.** The desktop app's tests compare its C# types with the schema
  files, so the `desktop` CI job **will fail** on the pull requests of this
  instruction that add schema files. That is expected here and only here. Say
  so in each pull request's description, and name the desktop test that
  fails. The first branch of instruction D0004 adds the C# types and turns it
  green; the reviewer merges both together.

## Branches

| Branch                      | Based on                  | What it holds         |
| --------------------------- | ------------------------- | --------------------- |
| `chore/one-implementer`     | `main`                    | Task 2                |
| `feat/recorder-scoping`     | `chore/one-implementer`   | Tasks 3 and 4         |
| `feat/protocol-recording`   | `feat/recorder-scoping`   | Tasks 5 to 8          |
| `feat/protocol-new-project` | `feat/protocol-recording` | Tasks 9 to 11, report |

Open one pull request per branch into `main` and say in each description which
branch it sits on. If a `feat/` branch passes roughly 1,500 changed lines (not
counting fixtures, generated files and the lockfile), split it in two stacked
branches and say so in the report.

`docs/` is the specification. Where this instruction and a document under
`docs/` disagree, stop and say so in the report.

## Tasks

### Part A: housekeeping

1. `git switch main` and `git pull --ff-only`. Delete the local branches that
   are now contained in `main`.
2. **The root documents say how the tracks work now** (`chore/one-implementer`):
   - `CLAUDE.md`: replace "that folder has its own implementer" with the rule
     of owner decision 2, in "Repository layout" and in "How to work".
   - `README.md` and `CONTRIBUTING.md` where they describe who works where.

### Part B: recorder improvements (`feat/recorder-scoping`)

3. **Review 0009, finding 1.** When a role and name (or a label, or a text)
   fit several elements, the recorder first tries to scope the target with
   `within` its nearest named container (a list item, table row, form, dialog
   or region, identified by its own checked candidates), before it falls back
   to CSS. The result is checked by the runner's rule like every candidate.
   Sample: the second "Delete" button of a list is recorded as the Delete
   button within the item that holds it, not by position. Update
   `docs/recording.md`.
4. **The two accepted suggestions**: a clear notice for a click on the page
   background, and `pnpm record` names an argument it does not know.

### Part C: recording over the protocol (`feat/protocol-recording`)

5. **Build the protocol proposal of `docs/recording.md` as written**:
   `startRecording`, `stopRecording`, `verifyRecording`, and the events
   `recordingStarted`, `stepRecorded`, `stepChanged`, `recordingNotice`,
   `recordingStopped`, `recordingVerified`. Zod schemas, generated JSON Schema
   files, example messages checked by the existing example tests, the error
   names it mentions in the error table. The proposal moves from
   `docs/recording.md` into `docs/protocol.md` as part of the protocol.
6. **`PROTOCOL_VERSION` becomes `0.1.1`** (R11); ADR 0011 updated.
7. **Rules of a session over the protocol:**
   - one recording at a time, and none while a run is going
     (`RecordingInProgress`, `RunInProgress`);
   - the recording browser is always visible;
   - the client closing its connection, `shutdown`, or the engine ending for
     any reason stops the recording and closes its browser; the file keeps
     what was recorded;
   - `stepRecorded.review` and the comment in the file say the same thing;
   - every message is masked like any other.
8. **Tests** through a real engine process and a real (headless, for the
   tests) recording browser: a whole session with steps, a changed `fill`, a
   notice, stop, verify passed; a failed verify; the refusals; the password
   never in any message. `pnpm record` keeps working.

### Part D: new projects, and two small requests (`feat/protocol-new-project`)

9. **`createProject`**: creates a project in a folder that is empty or does not
   exist yet, and opens it. Params: `root`, `name`, `baseUrl`, and optionally
   the first environment's name (default `local`). It writes a minimal valid
   `cfe.config.yaml`, the folders the config's globs name, and a `.gitignore`
   that leaves out the data folder and `.env`. The result is that of
   `openProject`. A folder that is not empty is refused with a clear error
   that names what is in the way. Document it in `docs/protocol.md` and
   `docs/step-format.md` ("Project configuration"). Names come from `PRODUCT`.
10. **Desktop request R0005**: `capabilities.installCommand`, the command that
    installs the browser, present when `capabilities.browsers` is empty.
11. **Desktop request R0006**: optional `section`, `action`, `title` and
    `location` on `stepSkipped`, filled when no `stepStarted` was sent for the
    step.

## Done when

- [ ] `pnpm verify` passes on every branch, with no browser installed.
- [ ] `pnpm test:integration` passes on every branch, and the `integration` job
      passed twice in a row on each pull request.
- [ ] `verify` and `integration` are green on Linux, Windows and macOS for
      every pull request. `desktop` is green on the first two; on the last two
      it fails only in the tests R12 names.
- [ ] Committed JSON Schema files match the generator; `PROTOCOL_VERSION` is
      `0.1.1`.
- [ ] A recording session over the protocol works from start to verify, and
      `createProject` gives a project that opens with no diagnostics.
- [ ] No password reaches any message, proven by a test.
- [ ] File header comments and TSDoc on every export, no `any`, and every lint
      or type suppression listed in the report with its reason.
- [ ] No npm dependency is added.
- [ ] No client package imports engine code. **Nothing under `apps/desktop/`
      changed.**
- [ ] `CHANGELOG.md` is updated in each branch.
- [ ] `handoff/reports/0010-recording-and-new-projects-over-the-protocol.md`
      exists on the last branch and follows [the template](../templates/report.md).

## Out of scope

- Anything under `apps/desktop/`. The C# side is instruction D0004.
- Recording checks, hover, drag, upload, recording into an existing test.
- Desktop request R0004 (the structure of step files through the protocol).
- Screenshots, page snapshots, the command-line `run`, the release (plan
  branches 14 to 16).
- Any AI feature.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- For each branch: its pull request number, the CI result of all three jobs
  (with the names of the `desktop` tests that fail, where R12 applies), and
  its changed-line count.
- One whole recording session as a client sees it, one message per line.
- What `createProject` writes, file by file.
- Every place where a document under `docs/` was unclear, wrong or silent and
  you had to choose.
- Anything in this instruction you think is wrong.

When this instruction is done and reported, go on with the desktop track's
open instruction (`apps/desktop/handoff/STATUS.md`).
