# Review 0008: the demo `.env` and the built-in actions

- Date: 2026-10-10
- Written by: reviewer
- Instruction: [0008](../instructions/0008-demo-env-and-the-built-in-actions.md)
- Report: `handoff/reports/0008-demo-env-and-the-built-in-actions.md` on the
  branch `feat/actions-flows` (it reaches `main` when pull request #46 is
  merged)
- Verdict: **Approved**, all five pull requests

## Pull requests

| Pull request                                           | Branch                         | Reviewed at | CI (`verify`, `integration`, `desktop`) | Verdict  |
| ------------------------------------------------------ | ------------------------------ | ----------- | --------------------------------------- | -------- |
| [#30](https://github.com/starscream000/coffee/pull/30) | `chore/demo-env`               | `a70719f`   | all nine jobs green                     | Approved |
| [#43](https://github.com/starscream000/coffee/pull/43) | `feat/actions-interaction`     | `a00af62`   | all nine jobs green                     | Approved |
| [#44](https://github.com/starscream000/coffee/pull/44) | `feat/actions-wait-expect`     | `aafa3b5`   | all nine jobs green                     | Approved |
| [#45](https://github.com/starscream000/coffee/pull/45) | `feat/actions-data-http-flows` | `7e9c9ce`   | all nine jobs green                     | Approved |
| [#46](https://github.com/starscream000/coffee/pull/46) | `feat/actions-flows`           | `cca6d62`   | all nine jobs green                     | Approved |

Merge in this order: #30, #43, #44, #45, #46.

## What I checked

- **CI** on all five pull requests, at the reviewed commits.
- **`main` as it is now, merged with the whole stack** (temporary pull request
  #47): all nine jobs green on three systems. `main` gained the desktop app of
  instruction D0003 after these branches were cut, so this is the check that
  the new engine and the new app still fit together.
- **The report**, in full.
- **A run of my own** through a real engine and my own client, on a copy of
  the demo app at the top of the stack, with no environment variable set:
  - the project opens with no diagnostics, so the committed `.env` works;
  - all 19 tests and 5 failing fixtures, 31 test instances in 19 seconds: 24
    passed, 5 failed as they should, 2 skipped. That covers the new samples
    for forms, interactions, navigation, the network, secrets, a header token,
    `within` with data rows, and flows;
  - the demo password appears nowhere on stdout or stderr; saved logins were
    reused in a second run; a cancelled run ended cleanly.

## What I could not check

- The source of the five branches and their tests. I relied on my run and on
  CI, including the report's account of a real bug that CI found on macOS in
  `expect.response` and its fix.
- My run used an older Chromium than the pinned one; CI uses the right one.
- The desktop tests that run real tests in a browser, against this engine:
  they are still skipped in CI (desktop review D0003, finding 1).

## Findings

None that block a merge.

1. **Should fix. The test I11 fails by chance** about once in 60 runs: it
   looks for the text `ada` in files that hold random hex (desktop request
   R0007; seen on pull request #36). In the next instruction.
2. **Should fix. `select` with an option that does not exist** ends as a plain
   `ActionTimeout`. The tester needs to read which option was asked for and
   which options the element has.
3. **Note.** No test runs `wait.response … as` against a page that never reads
   the response body; the report says so. Add a demo page for it when that
   area is next touched.

## Rulings on the report

- **Both questions are settled as built**, by the reviewer: a failing flow
  fails its `call` step with `FlowFailed` while the step inside keeps its own
  code, and a response body the page never reads is a warning, not a failure.
  Neither needs the owner.
- All sixteen "Decisions I made" are accepted. Masking the credential inside
  an `Authorization` value as well as the whole value (3) is a real
  improvement over what ADR 0014 said; update the ADR.
- The split of plan branch 13 in two is accepted.

## Done-when checks

| Check                                                   | Result                                            |
| ------------------------------------------------------- | ------------------------------------------------- |
| `pnpm verify` on every branch, no browser installed     | Yes (CI)                                          |
| `integration` twice in a row on each pull request       | Yes per the report; green at the reviewed commits |
| Every pull request open, CI green on three systems      | Yes, all three jobs                               |
| The demo opens with no diagnostics and no variable set  | Yes (their test, and my run)                      |
| S1 to S5, S9, S16, S19, I2, I3, I4                      | Yes (CI; the samples pass in my run)              |
| No built-in action answers `NotImplemented`             | Yes per the report; a unit test guards it         |
| Schemas match the generator; protocol still 0.1.0       | Yes                                               |
| No dependency added; nothing under `apps/desktop/`      | Yes                                               |
| `CHANGELOG.md` updated; the report follows the template | Yes                                               |

## Owner decisions needed

None from this review.

## For the next instruction

Instruction 0009: findings 1 and 2, then the recorder, which the owner has put
ahead of plan branches 14 to 16.
