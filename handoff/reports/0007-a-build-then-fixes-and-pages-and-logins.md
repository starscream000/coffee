# Report 0007: a build for the owner, then fixes, pages and logins

- Date: 2026-10-10
- Written by: implementer
- Instruction: [0007](../instructions/0007-a-build-then-fixes-and-pages-and-logins.md)

## Summary

**The owner's build.** The owner has a working build of the desktop app with
the engine, handed over in the chat before any branch work began. That needed
two things on his machine:

- the .NET 10 SDK, installed with his approval;
- a `--source` flag for `dotnet publish`, because his NuGet configuration
  lists no package source.

**Review 0006 and the owner's decisions.**

- #25 holds the fixes for findings 1 to 3, with a test or written proof for
  each.
- #27 holds the rulings (run ids with milliseconds, the documents) and the
  owner's decisions: CSV as RFC 4180, and `regex:` URL patterns.

**Plan branch 10** exists in two stacked parts: #28 (pages, `opens`,
settings, `cancelRun`, stray user actions) and #29 (saved logins). Checks S7,
S8, S17, S18, F8 and I11 pass.

**CI.** Every pull request is green on all three jobs on Linux, Windows and
macOS. The `integration` job passed twice in a row on each. A `desktop` test
failed once on #28; it is described below, and the fix kept the shared demo
config as that test expects.

## The build (Part A)

Commands run from `D:\Workspace\test-tool`, on `main` at `79aa289`:

1. `git switch main`, `git pull --ff-only`, and deleting the three merged
   local branches.
2. Checking the tools. Node is 24.11.0 (as `.nvmrc` says) and pnpm 10.34.6
   through Corepack. `dotnet --list-sdks` listed **no SDK**, only the
   runtimes 5.0.17 and 10.0.11. I asked the owner; he approved, and I ran
   `winget install --id Microsoft.DotNet.SDK.10 -e`, which installed SDK
   10.0.401.
3. `corepack pnpm install --frozen-lockfile` and `corepack pnpm build`.
4. `corepack pnpm --filter @cfe/engine exec playwright install chromium`
   found it already installed.
5. `dotnet publish apps/desktop/src/Desktop.App -c Release` **failed to
   restore**: NU1100 for every Avalonia package. The owner's
   `%APPDATA%\NuGet\NuGet.Config` has an empty `<packageSources>`. I did not
   change that file, since it may be deliberate. Instead I published with
   `--source https://api.nuget.org/v3/index.json`, which succeeded and
   changes nothing on disk.
6. The executable is
   `D:\Workspace\test-tool\apps\desktop\src\Desktop.App\bin\Release\net10.0\publish\Desktop.App.exe`.
7. **What I saw when it started:** its process stayed up with a responding
   window titled "Coffee". After 10 seconds it had one child process,
   `node.exe …\packages\engine\dist\main.js --stdio`. When I closed the
   window, the app and the engine both exited. I could not look at the screen
   itself.
8. `git status` was clean afterwards.

I told the owner the path, what to try first, what works and what does not
yet, and the three rebuild commands (with the `--source` flag). I also told
him the one-line command that adds nuget.org to his NuGet configuration, if
the empty list is not deliberate.

## Branches and pull requests

| Branch                     | Based on                   | Last commit | Pull request | Changed lines\* | CI: `verify`, `integration`, `desktop` (Linux, Windows, macOS) |
| -------------------------- | -------------------------- | ----------- | ------------ | --------------- | -------------------------------------------------------------- |
| `feat/runner-rows-results` | itself, `main` merged in   | `7f33695`   | #25          | 194 (Part B)    | all green                                                      |
| `fix/review-0006-rulings`  | `feat/runner-rows-results` | `c6e2d7a`   | #27          | 390             | all green                                                      |
| `feat/runner-pages-logins` | `fix/review-0006-rulings`  | `82ee1de`   | #28          | 1,343           | all green                                                      |
| `feat/runner-logins`       | `feat/runner-pages-logins` | this report | #29          | 1,022 + report  | all green                                                      |

\* Insertions plus deletions against the branch below, without the lockfile
and fixtures. For #25 the count is the Part B commits only; the merge of
`main` brought the desktop track's files. Nothing under `apps/desktop/`
changed in any branch, no dependency was added, and no JSON Schema file
changed. `PROTOCOL_VERSION` is still `0.1.0`.

**The split.** Plan branch 10 would have been about 2,370 lines, so it is
two stacked branches: #28 holds pages, settings, cancelling and stray
actions; #29 holds saved logins. Merge order: #25, #27, #28, #29.

**`integration` twice in a row.**

- #25 passed twice: the first run, then a rerun of the whole workflow.
- #27, #28 and #29 were each rerun once after their first green run:
  all three green again on the second attempt, with all nine jobs passing.

**The `desktop` job failed once, on #28.** The failing test was
`Desktop.App.Tests.RealEngineAppTests.Opens_the_demo_project_through_the_real_engine`
on all three systems:

- **Its message:** `Assert.Equal() Failure: Collections differ. Expected:
["local"]. Actual: ["local", "europe"]`.
- **The cause:** that test pins the demo project's environment list, and S17
  had added a second environment, `europe`, to the demo config.
- **The fix:** the demo config is shared with the desktop track, whose folder
  I may not touch, so I kept the config as the test expects. The second
  environment is now added only to the S17 integration test's copy of the
  project (`82ee1de`). The sample file is unchanged: its expected values are
  environment values, so it runs in any environment.
- **Result:** `desktop` has been green on #28 and #29 since then.

## Tasks

| Task | State | Notes                                                                                                                                                 |
| ---- | ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done  | Merged local branches deleted.                                                                                                                        |
| 2    | done  | See "The build".                                                                                                                                      |
| 3    | done  | `origin/main` merged into #25 with a merge commit.                                                                                                    |
| 4    | done  | Finding 1: record, then send.                                                                                                                         |
| 5    | done  | Finding 2: `demo.addTodo` waits for its item. S13 passed 30 times in a row.                                                                           |
| 6    | done  | Finding 3: every such `after` section now ends with a check.                                                                                          |
| 7    | done  | Run ids `20261009-054902-123-1a2b`. ADR 0015, `docs/protocol.md`, the protocol examples and the tests are updated.                                    |
| 8    | done  | `docs/step-format.md` ("Execution rules", rule 4) and `docs/protocol.md` (`openProject`, and the error code table).                                   |
| 9    | done  | CSV as RFC 4180, with no dependency. A broken file is a diagnostic in the CSV file at its line. The rules are in `docs/step-format.md` ("Data rows"). |
| 10   | done  | `regex:` prefix in the schema, `docs/actions.md` and `docs/step-format.md`. No ADR or fixture showed the old form.                                    |
| 11   | done  | `runner/pages.ts`, S7.                                                                                                                                |
| 12   | done  | `runner/logins.ts`, S8, S18, I11.                                                                                                                     |
| 13   | done  | S17, with `deviceScaleFactor: 1`. The second environment lives in the test's copy (see above).                                                        |
| 14   | done  | `cancelRun`.                                                                                                                                          |
| 15   | done  | F8.                                                                                                                                                   |
| 16   | done  | `/tabs`, `/receipt`, `/help`, `/settings`, `/login`, `/account` and `GET /api/logins`, documented in `examples/demo-app/README.md`.                   |
| 17   | done  | S7, S8, S17, S18, F8 and I11 pass in `integration`.                                                                                                   |

### The proof for each finding of review 0006

| Finding  | Test or proof                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1        | `runner/run-order.integration.test.ts`: "holds every event, and is complete when runFinished is sent". The run manager runs in-process with an event channel that, at the moment each event is sent, checks that `events.ndjson` already ends with it. When `runFinished` is sent, it also checks that `run.json` and every `test.json` are complete. With the old order (send, then record) it fails from the first event: "event 1 was sent before events.ndjson held it". |
| 2        | `demo.addTodo` waits until its item is on the page. S13 ran 30 times in a row through the harness on this Windows machine, and all 30 passed. The original failure was seen once, on macOS CI, and I could not reproduce it here, so the fix rests on the cause the review identified.                                                                                                                                                                                       |
| 3        | **A check, not a proof.** S13, both S6 files, F1 and F3 now end their `after` sections with `expect.text: { target: todos.count, equals: 0 }`. The page writes the count only after the server has answered the reset, so the check passes only once the reset has reached the server. S10 already ended that way.                                                                                                                                                           |
| 4 (note) | Run ids with milliseconds (task 7). The `keepRuns` test no longer waits a second between runs.                                                                                                                                                                                                                                                                                                                                                                               |
| 5 (note) | F8 (task 15).                                                                                                                                                                                                                                                                                                                                                                                                                                                                |

## Checks

| Command                                   | Result                                                                                                                                     |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `pnpm verify`, `feat/runner-rows-results` | passed, 382 tests                                                                                                                          |
| `pnpm verify`, `fix/review-0006-rulings`  | passed, 385 tests                                                                                                                          |
| `pnpm verify`, `feat/runner-pages-logins` | passed, 385 tests                                                                                                                          |
| `pnpm verify`, `feat/runner-logins`       | passed, 388 tests                                                                                                                          |
| `pnpm test:integration`                   | 53, 53, 60 and 67 tests on the four branches, all passing                                                                                  |
| No browser left behind                    | `cleanup.integration.test.ts` as before. `pages.integration.test.ts` also checks that every browser process is gone after a cancelled run. |
| `pnpm generate:schemas`                   | no change                                                                                                                                  |

### How long the `integration` job takes now

On the last run of #29 (67 browser tests): 1 min 9 s on Linux, 1 min 44 s on Windows and 1 min 28 s on macOS (whole job, install included).

## Decisions I made

1. **How a new tab is attributed to the step that opened it.** Playwright
   reports a tab only once it has started loading, which can be after the
   step that opened it has ended. I measured about 60 ms after `click`
   returns for a `target="_blank"` link, so the warning pointed at the next
   step. Chromium reports the tab's creation earlier, always before `click`
   returns. The engine therefore listens on a browser DevTools session
   (`Target.targetCreated`) and attributes each tab to the step that was
   running when it was created. This is internal and Chromium-only, as
   v0.1.0 is. A fixed wait after every step would have cost tens of
   milliseconds per click.
2. **`pageOpened` is sent for pages the application opens** (with `opens` or
   unnamed), not for the declared pages the engine opens on first use. The
   protocol describes `pageOpened` as "a new page appeared during a step".
3. **Automatic names count every page of the test** in order of opening, with
   `main` as the first. In S7 the receipt tab is the second page and the help
   tab is `tab-3`.
4. **`opens` waits for the named page within the step's timeout minus
   150 ms**, so a missing tab is `PageNotOpened` rather than `ActionTimeout`.
5. **A step on a closed page** gets a new blank page only in `after` (with
   `PageReplaced`); elsewhere it fails with `PageClosed`. A page name nobody
   registered fails with `UnknownPage`. Validation catches that in tests, but
   not in flows.
6. **Stray user actions.** `StrayActionCode` is sent once, when the engine
   gives up on the action and closes its page. The action's later calls are
   then ignored silently: `ctx.vars.set` and `ctx.log` do nothing, and
   `ctx.locate` rejects.
7. **`cancelRun`** with a run id that is not in progress is `-32602`. After a
   cancellation, the `after` steps share 30 seconds in all.
8. **A login flow's steps are not reported as steps of the test.** A `log`
   event says whether the login signed in or reused a saved state. The login
   runs when a step first uses a page with that login, and that step's
   timeout now starts once its page is ready. Before that change, a slow
   sign-in showed as the step's own `ActionTimeout`. A failing login fails
   that step with the flow step's error code and location, prefixed "The
   saved login "customer" could not sign in: …". This is now written into
   `docs/step-format.md` ("Saved logins").
9. **A login flow's `params`** are the login's `with` values over the flow's
   parameter defaults. A login flow cannot itself use a saved login
   (`NestedLogin`).
10. **Within one run**, a login's state is shared by every test that needs it.
    `refreshLogins` therefore signs in once per login per run, not once per
    test. `freshLogin` signs in for each test that has it.
11. **The test hook for I11** is the environment variable
    `CFE_TEST_HELD_LOGIN_FILE`. Clean-up treats the file it names as held
    open. It exists only for that check.
12. **The demo app's admin user is `ada`, not `admin`.** The metadata must
    hold the login's name (`admin`), and I11 checks that no file holds a login
    parameter value, so the two had to differ.
13. **CSV details beyond RFC 4180's core:**
    - spaces around a quoted value are allowed and dropped;
    - a quote inside an unquoted value is an error, as RFC 4180 does not allow
      it;
    - a broken file's diagnostic is in the CSV file at the problem's line,
      with a hint naming the test that uses it.
14. **`regex:` with nothing after it** is an `InvalidRegex`.

### Where the documents were unclear or silent

- How a login flow's steps are reported, and whether their time counts
  against the step that needed the login (decision 8).
- Whether `pageOpened` covers declared pages, and how automatic names are
  numbered (decisions 2 and 3).
- When `StrayActionCode` is sent (decision 6).
- What `cancelRun` answers for an unknown run id (decision 7).

## Anything in this instruction I think is wrong

- **Task 13 and the shared demo config.** "An environment that overrides
  locale and timezone" is natural in the demo config. But a desktop test pins
  that config's environment list, and the desktop folder is not mine to
  change. While both tracks share `examples/demo-app`, a change to it can
  break the other track's tests. A short list of what the desktop tests rely
  on in the demo project would help both sides.
- **The demo project now needs `DEMO_PASSWORD`.** The saved logins read it,
  so opening the demo project without it reports two `SecretNotSet` errors in
  the config. The harness and the protocol tests set it, and the desktop test
  does not check diagnostics. **Once #29 is merged, the owner's desktop app
  will show those two errors when it opens `examples/demo-app`**, unless the
  variable is set. See question 1.

## Questions for the owner

1. **How should the demo project get `DEMO_PASSWORD` when you open it in the
   desktop app?**
   - **(a) Commit a demo-only `.env` in `examples/demo-app/` (recommended).**
     The value is a demo password with no meaning outside the demo, though
     CLAUDE.md says to never commit `.env` files in general.
   - (b) Document that you set `DEMO_PASSWORD` yourself, and accept the two
     errors until then.

## Not done, not pushed, not verified

- I saw the app's window only from outside: process, window title and
  responsiveness. I did not see its contents.
- The flake of finding 2 could not be reproduced locally (see the table).
- `pnpm verify` was not run with Chromium uninstalled on this machine. CI's
  `verify` job has no browser and passes.

## Suggestions

- Add nuget.org to the owner's NuGet configuration
  (`dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org`)
  if the empty list is not deliberate. Otherwise rebuilds always need the
  `--source` flag.
