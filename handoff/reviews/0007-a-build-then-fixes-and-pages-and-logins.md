# Review 0007: a build for the owner, then fixes, pages and logins

- Date: 2026-10-10
- Written by: reviewer
- Instruction: [0007](../instructions/0007-a-build-then-fixes-and-pages-and-logins.md)
- Report: `handoff/reports/0007-a-build-then-fixes-and-pages-and-logins.md` on
  the branch `feat/runner-logins` (it reaches `main` when pull request #29 is
  merged)
- Verdict: **Approved**, all four pull requests

## Pull requests

| Pull request                                           | Branch                     | Reviewed at | CI (`verify`, `integration`, `desktop`) | Verdict  |
| ------------------------------------------------------ | -------------------------- | ----------- | --------------------------------------- | -------- |
| [#25](https://github.com/starscream000/coffee/pull/25) | `feat/runner-rows-results` | `7f33695`   | all nine jobs green                     | Approved |
| [#27](https://github.com/starscream000/coffee/pull/27) | `fix/review-0006-rulings`  | `c6e2d7a`   | all nine jobs green                     | Approved |
| [#28](https://github.com/starscream000/coffee/pull/28) | `feat/runner-pages-logins` | `82ee1de`   | all nine jobs green                     | Approved |
| [#29](https://github.com/starscream000/coffee/pull/29) | `feat/runner-logins`       | `64a2e86`   | all nine jobs green                     | Approved |

Merge in this order: #25, #27, #28, #29.

## What I checked

- **CI** on all four pull requests: `verify`, `integration` and `desktop` green
  on Linux, Windows and macOS at the reviewed commits. Each branch holds
  `main`, so this is the merged result.
- **The report**, in full. The build was handed to the owner first, as asked.
- **A run of my own** through a real engine and my own client, on a copy of
  the demo app at the top of the stack:
  - all 13 tests and 5 failing fixtures, 21 test instances in 16 seconds: 14
    passed, 5 failed, 2 skipped, each as the definition of done says;
  - the new parts behave as documented: a named tab and an unnamed one with
    its warning, two saved logins, a fresh login, the settings sample, and the
    user action that ignores its signal (step timed out, page closed and
    replaced for the `after` step, both warnings sent);
  - a second run reused both saved logins ("Reused the saved login …") and
    took 0.3 seconds instead of 0.7;
  - no file under `.cfe/logins/` holds the password or a user name, and the
    password never appeared on stdout or stderr;
  - `events.ndjson` equalled the events received; run ids carry milliseconds;
  - `cancelRun` in the middle of a run of three tests ended it as `cancelled`
    with all three tests reported cancelled; an unknown run id is refused.
- **Scope:** nothing under `apps/desktop/` changed; no dependency was added.

## What I could not check

- The source of the four branches line by line, and the tests. I relied on my
  run above and on CI, twice green per pull request according to the report.
- My run used an older Chromium than the pinned one; CI uses the right one.
- The CSV reader and the `regex:` rule beyond what CI covers.
- The owner's build: I cannot see his machine.

## Findings

None that block a merge.

1. **Owner decides. Opening the demo project now needs `DEMO_PASSWORD`.** The
   saved logins read that secret. Without it, the demo project opens with two
   `SecretNotSet` errors, and the owner's desktop build will show them. See
   the owner decision below.
2. **Note. Both tracks share `examples/demo-app`.** A desktop test pins the
   demo config's list of environments, which forced the engine side to work
   around it (the second environment of S17 lives only in a test's copy). The
   desktop test should check what it needs and not the whole list; that goes
   to the desktop track.
3. **Note. The owner's NuGet configuration lists no package source**, so
   `dotnet publish` needs `--source https://api.nuget.org/v3/index.json` on
   his machine. This is his machine's setting, not the repository's.

## Rulings on the report's "Decisions I made"

All fourteen are accepted. The ones worth a word:

- 1, tabs are attributed to steps through a Chromium DevTools session:
  accepted. It is internal and Chromium-only, as v0.1.0 is; say so in the
  architecture document when another browser is added.
- 8, a login flow's steps are not reported as steps of the test, and the
  step's timeout starts once its page is ready: accepted; it is in the
  documents.
- 10, within one run a login's state is shared by every test that needs it:
  accepted.
- 11, the test-only variable `CFE_TEST_HELD_LOGIN_FILE`: accepted for now. A
  test hook in product code should not grow; keep it to this one.
- 12, the demo's admin user is `ada`: accepted.
- 13 and 14, the CSV details and an empty `regex:`: accepted.

The split of plan branch 10 into #28 and #29 is accepted.

## Done-when checks

| Check                                                    | Result                                            |
| -------------------------------------------------------- | ------------------------------------------------- |
| The owner was told about the build before branch work    | Yes per the report                                |
| `pnpm verify` on every branch, no browser installed      | Yes (CI)                                          |
| `integration` passed twice in a row on each pull request | Yes per the report; green at the reviewed commits |
| Every pull request open, CI green on three systems       | Yes, all three jobs                               |
| Findings 1 to 3 of review 0006 have a test or a proof    | Yes; finding 1 has a real ordering test           |
| S7, S8, S17, S18, F8, I11                                | Yes (CI, and my own run)                          |
| No browser process left behind, also after a cancel      | Yes (their tests on three systems)                |
| Schemas match the generator; protocol still 0.1.0        | Yes                                               |
| No dependency added; nothing under `apps/desktop/`       | Yes                                               |
| `CHANGELOG.md` updated; the report follows the template  | Yes. Again a careful and honest report.           |

## Owner decisions needed

1. **How does the demo project get its demo password?**
   - **(a) Commit a file `examples/demo-app/.env` with the demo value.** The
     demo then opens without errors for everyone. The value protects nothing:
     the demo server accepts it only on the local machine. It needs one
     exception to the rule "never commit `.env` files", limited to this
     folder and said so in the file.
   - (b) Keep it out of the repository. Everyone who opens the demo sets
     `DEMO_PASSWORD` first, or sees two errors.

   Recommended: **a**.

## For the next instruction

Instruction 0008:

- The owner's answer on the demo password.
- Plan branches 11 to 13, the remaining built-in actions.
