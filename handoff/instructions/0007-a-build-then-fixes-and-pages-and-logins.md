# Instruction 0007: a build for the owner, then fixes, pages and logins

- Date: 2026-10-10
- Written by: reviewer
- Based on `main` at: `93806b5` or later
- Replaces: none
- Follows review: [0006](../reviews/0006-review-fixes-and-the-runner.md)

## Goal

First, the owner has a working build of the desktop app with the engine on his
machine and knows how to start it. Then: pull request #25 is reliable and can
be merged, the owner's decisions on CSV files and URL patterns are built, and
plan branch 10 exists (named pages, saved logins, settings, cancelling a run).

## Owner decisions

1. Pull requests #19, #21 and #23 are merged. #25 stays open until findings 1
   and 2 of review 0006 are fixed on its branch.
2. **The owner wants a build of the desktop app and the engine before any
   other work** (2026-10-10). That is Part A.
3. **CSV data files are read as spreadsheets write them (RFC 4180)**
   (2026-10-10).
4. **A regular expression for a URL is written with the prefix `regex:`**
   (2026-10-10), as in `wait.url: 'regex:^/orders/\d+$'`. Anything without the
   prefix is a glob, so `/orders/` is always a path. The `/regex/` form is
   gone.
5. Hidden elements count in `ctx.locate` (2026-10-10). Nothing to do; it is
   built and documented.
6. The desktop track is on hold. Its folder is still not yours: change nothing
   under `apps/desktop/`.
7. **Not now:** the change to `ErrorInfo.candidates` proposed in report 0006.
   It needs the matching change in the desktop app and waits for that track.

## The `desktop` CI job

`main` now has a third CI job, `desktop`, which builds the desktop app and runs
its tests on three systems. Some of those tests start the real engine and read
the protocol's schema files, so an engine change can make it fail. A branch
runs it once it holds `main`'s workflow file, so merge `main` into your
branches as the tasks say.

If `desktop` fails on one of your pull requests: do not change anything under
`apps/desktop/`. The failed test and its message show on the pull request.
Say in the report which test failed and why you think it did, and go on with
the tasks that do not depend on it.

## Branches

| Branch                     | Based on                                   | Plan branch | What it holds          |
| -------------------------- | ------------------------------------------ | ----------- | ---------------------- |
| none                       | `main`, unchanged                          | none        | Part A (tasks 1 and 2) |
| `feat/runner-rows-results` | itself (existing branch, pull request #25) | 9           | Tasks 3 to 6           |
| `fix/review-0006-rulings`  | `feat/runner-rows-results`                 | none        | Tasks 7 to 10          |
| `feat/runner-pages-logins` | `fix/review-0006-rulings`                  | 10          | Tasks 11 to 17, report |

Add commits; never rewrite what is pushed. Push `feat/runner-rows-results` as
soon as tasks 3 to 6 are done and its CI is green, before starting the next
branch, so that #25 can be merged on its own.

Open a pull request for each new branch into `main` and say in its description
which branch it sits on. If a `feat/` branch passes roughly 1,500 changed lines
(not counting fixtures, generated files and the lockfile), split it in two
stacked branches and say so in the report.

`docs/` is the specification. Where this instruction and a document under
`docs/` disagree, stop and say so in the report.

## Tasks

### Part A: the build for the owner (no branch, no file changes)

1. `git switch main` and `git pull --ff-only`. Delete the local branches that
   are now contained in `main`. Keep `feat/runner-rows-results`.
2. **Build the desktop app and the engine from `main` as it is, on the owner's
   machine, and hand it over before doing anything else.**
   1. Check the tools: Node as `.nvmrc` says, pnpm through Corepack, and the
      .NET 10 SDK (`dotnet --list-sdks`). If the .NET 10 SDK is missing, tell
      the owner the command that installs it and ask before running it; do not
      install system software without his word.
   2. `pnpm install --frozen-lockfile`, then `pnpm build`.
   3. Install Playwright's Chromium for the engine:
      `pnpm --filter @cfe/engine exec playwright install chromium`.
   4. `dotnet publish apps/desktop/src/Desktop.App -c Release`. Leave the
      output where it lands, inside the repository: from there the app finds
      the engine of this checkout by itself (ADR D0004 of the desktop track).
   5. Start the published app once. Check that its process stays up and that
      it started the engine (a `node … main.js --stdio` child process). Then
      close it. Say honestly what you could and could not see.
   6. `git status` must be clean afterwards: nothing is committed for this
      task.
   7. **Tell the owner in the chat**, in plain words:
      - the full path of the executable and how to start it;
      - what to try first: "Open folder…", then `examples\demo-app`;
      - what works in this build: the list of tests (by file name until #25
        is merged), the problems the engine finds, editing and saving a step
        file with the engine's checks while typing, the action catalogue, the
        engine log, the settings;
      - what does not exist yet: starting a test run from the app (desktop
        milestone D2) and from the command line (plan branch 15). The engine
        can run tests, but only the automated tests drive it so far;
      - the three commands that rebuild it after a `git pull`.

   If a step fails, do not change anything under `apps/desktop/` to get past
   it: stop, and give the owner the exact error. Otherwise go straight on with
   Part B; do not wait for an answer.

### Part B: make #25 mergeable (`feat/runner-rows-results`)

The finding numbers are those of review 0006. Each fix comes with a test that
fails without it.

3. **Take in `main`.** Merge `origin/main` into the branch with a merge commit,
   so its CI also runs the `desktop` job.
4. **Finding 1.** An event is in the run folder before it is sent: when a
   client receives any event, `events.ndjson` already holds it, and when it
   receives `runFinished`, `run.json` and every `test.json` are complete.
   Prove the order with a test that looks at the files at the moment a message
   is sent, not with a wait.
5. **Finding 2.** Sample S13 passes every time. `demo.addTodo` waits until its
   item is on the page before it returns. Run the sample 30 times in a row and
   say so in the report.
6. **Finding 3.** For every sample whose `after` section ends with a click and
   nothing after it: either show that the reset always reaches the server
   before the next test starts, or end the section with a check. Say in the
   report which it was.

Nothing else goes on this branch.

### Part C: rulings and owner decisions (`fix/review-0006-rulings`)

7. **Run ids** (finding 4) carry milliseconds, so they sort exactly:
   `20261009-054902-123-1a2b`. Update ADR 0015, the documents and the examples
   that show a run id.
8. **Documents.** `docs/step-format.md` ("Execution rules"): a failing `after`
   step makes the test fail; one skipped because a variable was never set does
   not. `docs/protocol.md`: `openProject` during a run is refused with
   `RunInProgress`.
9. **CSV as RFC 4180** (owner decision 3). A value may be wrapped in double
   quotes; inside quotes a comma and a line break are part of the value, and a
   quote is written twice. Unquoted values are trimmed and blank lines are
   skipped, as today; a quoted value is kept exactly. Lines may end in LF or
   CRLF. A file that breaks the rules (a quote that is never closed, a row
   with more or fewer values than the header) is a diagnostic with file and
   line. No dependency. Write the rules into `docs/step-format.md` ("Data
   rows").
10. **URL patterns** (owner decision 4). A pattern that starts with `regex:`
    is a regular expression, checked when validating as review 0003 asked;
    anything else is a glob. Change the documents (`docs/actions.md`,
    `docs/step-format.md`, any ADR that shows the old form), the schemas, the
    validator, the fixtures and the tests. No built-in action uses URL
    patterns at run time yet, so this is the validation side only.

### Part D: pages and logins (`feat/runner-pages-logins`, plan branch 10)

The specification is `docs/step-format.md` ("Pages", "New tabs and pop-ups",
"Viewport, locale and timezone", "Saved logins"), ADR 0018,
`docs/protocol.md` (`cancelRun`, the events `pageOpened` and the warnings
`UnnamedPage`, `StrayActionCode`, `PageReplaced`, `LoginCleanupFailed`) and
`docs/actions.md` ("`ctx.signal`").

11. **Named pages.** Pages declared in a test, `page:` on a step, `opens` on a
    step that opens a new tab, automatic names with the `UnnamedPage` warning,
    and `pageOpened` events. Steps on a page that is not `main` report it in
    `page`.
12. **Saved logins**, as ADR 0018 describes them: the login flow runs when no
    saved state fits, the state is saved under `.cfe/logins/` with hashed file
    names, `maxAge`, `freshLogin`, deleting old states at the start of a run,
    and the `refreshLogins` option of `startRun`. No file under `.cfe/logins/`
    may hold a login parameter value.
13. **Settings.** The checks for viewport, locale and timezone, with an
    environment that overrides locale and timezone (S17). The settings are
    already applied; this is the sample and its test.
14. **`cancelRun`.** As `docs/protocol.md` says: the current step's signal is
    aborted and the step fails with `Cancelled`, the remaining steps are
    skipped, `after` steps still run within their limit, tests not started are
    reported as cancelled, and `runFinished` says `cancelled`.
15. **A user action that ignores `ctx.signal`** (F8): its step fails with
    `ActionTimeout`, its page is closed, a `StrayActionCode` warning is sent,
    and an `after` step on that page runs on a replacement page with the
    `PageReplaced` warning.
16. **The demo app** gets the pages these samples need (a login, a page that
    opens a tab, a page that shows viewport, locale and timezone), documented
    in `examples/demo-app/README.md`.
17. **Samples and tests.** Checks S7, S8, S17, S18, F8 and I11 pass in the
    `integration` job, with the file names the definition of done gives.

## Done when

- [ ] The owner has been told where the build is and how to start it, before
      any branch work began.
- [ ] `pnpm verify` passes on every branch, with no browser installed.
- [ ] `pnpm test:integration` passes on every branch, and the `integration` job
      passed **twice in a row** on each pull request (run it again once it is
      green; say so in the report).
- [ ] Every pull request is open, and CI is green on Linux, Windows and macOS
      for each, or the report says which `desktop` test failed and why.
- [ ] Findings 1 to 3 of review 0006 each have a test or a written proof, named
      in the report.
- [ ] Checks S7, S8, S17, S18, F8 and I11 pass on `feat/runner-pages-logins`.
- [ ] No browser process is left behind after any test, also after a cancelled
      run.
- [ ] Committed JSON Schema files match the generator. `PROTOCOL_VERSION`
      stays `0.1.0`.
- [ ] File header comments and TSDoc on every export, no `any`, and every lint
      or type suppression listed in the report with its reason.
- [ ] No npm dependency is added.
- [ ] No client package imports engine code. Nothing under `apps/desktop/`
      changed.
- [ ] `CHANGELOG.md` is updated in each branch.
- [ ] `handoff/reports/0007-a-build-then-fixes-and-pages-and-logins.md` exists
      on the last branch and follows [the template](../templates/report.md).

## Out of scope

- Any change under `apps/desktop/`, including fixes to make the build work.
- A way to start a run from the command line or the app.
- Every built-in action other than the four that exist.
- Screenshots, page snapshots and `openSnapshot` (plan branch 14).
- The `ErrorInfo.candidates` change (owner decision 7).
- Raising the Node pin.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- The build: the commands you ran, where the executable is, what you saw when
  you started it, and anything that had to be installed.
- For each branch: its pull request number, the CI result of all three jobs,
  and its changed-line count.
- For each finding: the test or proof.
- Every place where a document under `docs/` was unclear, wrong or silent and
  you had to choose.
- Anything in this instruction you think is wrong.
