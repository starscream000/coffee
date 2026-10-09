# Review 0006: review fixes and the runner

- Date: 2026-10-10
- Written by: reviewer
- Instruction: [0006](../instructions/0006-review-fixes-and-the-runner.md)
- Report: `handoff/reports/0006-review-fixes-and-the-runner.md` on the branch
  `feat/runner-rows-results` (it reaches `main` when pull request #25 is
  merged)
- Verdict: #19, #21 and #23 are **approved**. **Changes requested** on #25.

## Pull requests

| Pull request                                           | Branch                     | Reviewed at | CI                                                          | Verdict           |
| ------------------------------------------------------ | -------------------------- | ----------- | ----------------------------------------------------------- | ----------------- |
| [#19](https://github.com/starscream000/coffee/pull/19) | `fix/review-0005-findings` | `3a4ab9f`   | green on three systems                                      | Approved          |
| [#21](https://github.com/starscream000/coffee/pull/21) | `feat/runner-core`         | `eb3d63a`   | green on three systems                                      | Approved          |
| [#23](https://github.com/starscream000/coffee/pull/23) | `feat/runner-actions`      | `3752ed7`   | green on three systems, also merged with `main` (see below) | Approved          |
| [#25](https://github.com/starscream000/coffee/pull/25) | `feat/runner-rows-results` | `d568132`   | first run green; second run red on macOS and on Windows     | Changes requested |

Merge #19, #21 and #23 in order. #25 stays open until findings 1 and 2 are
fixed on its branch.

## What I checked

- **CI** on all four pull requests: `verify` and `integration` green on three
  systems at the reviewed commits.
- **`main` merged with the stack up to #23**, with all three CI jobs, the
  `desktop` job included (temporary pull request #26): nine jobs green on
  three systems. The desktop app's tests therefore still pass against the
  runner.
- **A second CI run of #25**, which found findings 1 and 2.
- **A run of my own** through a real engine and my own client, on a copy of
  the demo app: all 8 tests and 4 failing fixtures, 16 test instances in 11
  seconds. Every instance ended as the definition of done says (10 passed, 4
  failed, 2 skipped); `seq` rose by one over 173 events; nothing but JSON
  reached stdout; a second `startRun` was refused with `RunInProgress`; the
  failing steps carry readable errors; `events.ndjson` equalled the events
  received once the run had ended; closing stdin in the middle of a run ended
  the engine with code 0 and left no browser process.
- **Review 0005, finding 1**, again with the secret `string`: no parameter
  schema is changed.
- **`listTests`** on the demo app: files, tags and row counts are right.
- **Source read:** the run folder, the part of the runner that sends and
  records events, the stdout guard. For the rest of the runner I relied on
  the run above and on CI.

## What I could not check

- The runner's code line by line (see above), and the tests.
- My own run used an older Chromium than the pinned one; CI uses the right
  one.
- That each fix's test fails without the fix, and the timings in the report.

## Findings

1. **Must fix (#25). An event reaches the client before it is in the run
   folder** (`packages/engine/src/runner/run.ts`, `emit`: `notify` first, then
   `folder.record`). A client that reads the folder when `runFinished` arrives
   can find `events.ndjson` without its last line and `run.json` still saying
   `running`. CI caught exactly that on Windows (check I8: 44 lines instead of
   45). The desktop app's run history will read these files on `runFinished`.
   Record first, then send; at the least, the run folder must be complete
   before `runFinished` is sent.
2. **Must fix (#25). Sample S13 fails now and then** (macOS, second CI run:
   the run ended `failed`). `demo.addTodo` is called twice in a row, and the
   demo page empties the text field only after its request has returned, so
   the first call's answer can wipe what the second call just typed. Make the
   sample stable; the honest way is for the action to wait until its item is
   on the page before it returns, which is also what a real user action should
   do.
3. **Should check.** Several samples end their `after` steps with
   `click: todos.clearAll` and nothing after it. The test's browser context
   closes right after the click, so the reset request may not reach the
   server, and the next test would start with leftovers. Either show it cannot
   happen or end those `after` sections with a check.
4. **Note. Run ids sort only to the second** (the report's own point):
   `keepRuns` can delete the newer of two runs started in the same second.
   Accepted as proposed: milliseconds in the run id, with ADR 0015 updated.
5. **Note. A user action that ignores `ctx.signal`** keeps running after its
   step timed out. Plan branch 10 closes its page (check F8).

## Rulings on the report

All sixteen "Decisions I made" are accepted. The ones worth a word:

- 4, a failing `after` step fails the test: accepted; write it into
  `docs/step-format.md` ("Execution rules").
- 6, margins of 150 ms before the deadline: accepted. They make a missing
  target read as `TargetNotFound` instead of a timeout.
- 8, `NotImplemented` for built-ins that do not exist yet: accepted.
- 11, `openProject` during a run is refused: accepted; document it in
  `docs/protocol.md`.
- 15, CSV values trimmed and blank lines skipped: accepted, as part of the
  owner's answer on CSV.

The departures are accepted: the runner split in two branches, user actions
running from the first of them, and the macOS fix for finding Chromium.

## Done-when checks

| Check                                                   | Result                                                   |
| ------------------------------------------------------- | -------------------------------------------------------- |
| `pnpm verify` on every branch, no browser installed     | Yes (CI)                                                 |
| `pnpm test:integration` on the runner branches          | Yes for #21 and #23; **not reliably for #25**            |
| Every pull request open, CI green on three systems      | Yes for #19, #21, #23; #25 red on its second run         |
| Findings 1 to 5 of review 0005 have tests               | Yes per the report; finding 1 confirmed by my probe      |
| S10, S11, S12, S14, F1, F2, F3, F7                      | Yes (CI, and my own run)                                 |
| S6, S13, S15, I8, I9, I10                               | Pass in my run; S13 and I8 failed once each in CI        |
| No browser process left behind                          | Yes (their test on three systems; my probe)              |
| Schemas match the generator; protocol still 0.1.0       | Yes                                                      |
| No dependency added; no client imports engine code      | Yes                                                      |
| Nothing under `apps/desktop/` changed                   | Yes                                                      |
| `CHANGELOG.md` updated; the report follows the template | Yes. The report is thorough and honest about its limits. |

## Owner decisions needed

1. **CSV data files: read them as spreadsheets write them (RFC 4180)?** That
   means double quotes around a value that contains a comma, a quote or a line
   break. Today such a file is refused. Recommended: yes; it is about 40 lines
   and no new dependency.
2. **Add two details to the error for a target that was not found** (which
   level failed: frame, containing element or element; and "not tried" for a
   candidate). It is a compatible protocol addition. Recommended: yes, but
   together with the matching change in the desktop app, because the desktop's
   tests compare its C# types with the protocol and would fail otherwise. So
   it waits until the desktop track is active again.

## For the next instruction

Instruction 0007:

- On `feat/runner-rows-results` (pull request #25): findings 1, 2 and 3.
- Finding 4, the documents of rulings 4 and 11, the owner's answer on CSV.
- The owner's decision of 2026-10-10 on URL regular expressions (the prefix
  `regex:`): documents, schemas and validator.
- Then plan branch 10 (named pages, saved logins, `cancelRun`).
