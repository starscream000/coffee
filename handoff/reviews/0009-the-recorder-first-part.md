# Review 0009: the recorder, first part

- Date: 2026-10-10
- Written by: reviewer
- Instruction: [0009](../instructions/0009-the-recorder-first-part.md)
- Report: `handoff/reports/0009-the-recorder-first-part.md` on the branch
  `feat/recorder-verify` (it reaches `main` when pull request #52 is merged)
- Verdict: **Approved**, all five pull requests

## Pull requests

| Pull request                                           | Branch                     | Reviewed at | CI (`verify`, `integration`, `desktop`) | Verdict  |
| ------------------------------------------------------ | -------------------------- | ----------- | --------------------------------------- | -------- |
| [#48](https://github.com/starscream000/coffee/pull/48) | `fix/review-0008-findings` | `7863602`   | all nine jobs green                     | Approved |
| [#49](https://github.com/starscream000/coffee/pull/49) | `docs/recorder-design`     | `6c02fce`   | all nine jobs green                     | Approved |
| [#50](https://github.com/starscream000/coffee/pull/50) | `feat/recorder-core`       | `01a31df`   | all nine jobs green                     | Approved |
| [#51](https://github.com/starscream000/coffee/pull/51) | `feat/recorder-session`    | `7e63f4b`   | all nine jobs green                     | Approved |
| [#52](https://github.com/starscream000/coffee/pull/52) | `feat/recorder-verify`     | `9616866`   | all nine jobs green                     | Approved |

Merge in this order: #48, #49, #50, #51, #52.

## What I checked

- **CI** on all five pull requests, at the reviewed commits, on three systems.
- **The report**, and the protocol proposal in `docs/recording.md`.
- **A recording of my own, on a page the implementer never saw.** I wrote a
  small page with a sign-in form, two password fields, a dropdown, a checkbox,
  two buttons with the same name, a clickable element with no name at all,
  and a button named only by `aria-label`, and made real mouse and key input
  on it in the recording browser. The result:
  - every interaction became the right action: `fill`, `check`, `select`,
    `click`;
  - targets got a role and name first, then a label or text, then CSS, each
    checked against the page;
  - the password that equals a declared secret was written as
    `${secrets.SHOP_PASSWORD}`; the other one became an empty placeholder
    variable marked for review. Neither typed value is in the file or in any
    event;
  - the second of two "Delete" buttons and the unnamed element could only be
    identified by CSS; both were written and marked for review, as decision
    R5 says;
  - **the recorded test passed when played back.**
- The owner recorded by hand as well (a to-do and a login) and both passed.

## What I could not check

- The recorder's source and its tests; I relied on my recording and on CI.
- A visible browser: my session and the automated tests ran the recording
  browser without a window. The owner's hand-made sessions used a visible
  one.
- My session used an older Chromium than the pinned one.

## Findings

None that block a merge.

1. **Should improve. A repeated element falls back to position.** The second
   "Delete" button was recorded as `body > ul > li:nth-of-type(2) > button`.
   That breaks as soon as the list changes order. The step format already has
   the right tool: `within` (the Delete button inside the row named "Table").
   When a role and name match several elements, the recorder should first try
   to scope the target with `within` its nearest named container (a list item,
   table row, form, dialog or region), and only then fall back to CSS.
2. **Note. Hover menus** are recorded as a click that playback cannot reach.
   The documents say so. It needs `hover` to be recognised; a later part.
3. **Note.** The two suggestions of the report are accepted: a clear notice
   for a click on the page background, and `pnpm record` naming an argument it
   does not know.

## Rulings on the report

- **The protocol proposal is accepted as written**, including the notice kind
  `writeFailed`. Instruction 0010 builds it.
- The owner's two rulings during the work are on record in ADR 0022: test ID
  before text in the candidate order, and a placeholder variable for a
  password that matches no declared secret.
- All fifteen "Decisions I made" are accepted. Holding a click until its
  candidates are checked (1) is the right trade.
- **The report's question** (reuse a shared target when it identifies the same
  element): yes, later, as recommended. It belongs with finding 1.
- The split of Part C in two branches is accepted.

## Done-when checks

| Check                                                   | Result                                            |
| ------------------------------------------------------- | ------------------------------------------------- |
| `pnpm verify` on every branch; `integration` twice      | Yes per the report; green at the reviewed commits |
| Every pull request open, CI green on three systems      | Yes, all three jobs                               |
| No schema file changed; protocol still 0.1.0            | Yes                                               |
| `pnpm record` used by hand, recording passes verify     | Yes, by the owner                                 |
| Every case of task 13 has a test                        | Yes per the report                                |
| No password reaches a file, an event or the output      | Yes (their test, and my own recording)            |
| No dependency added; nothing under `apps/desktop/`      | Yes                                               |
| `CHANGELOG.md` updated; the report follows the template | Yes                                               |

## Owner decisions needed

None from this review.

## For the next instruction

Instruction 0010: recording over the protocol, so the desktop app can use it;
a request that creates a new project; finding 1 and the accepted suggestions.
