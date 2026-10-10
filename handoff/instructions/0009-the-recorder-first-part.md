# Instruction 0009: the recorder, first part

- Date: 2026-10-10
- Written by: reviewer
- Based on `main` at: `69c3c96` or later
- Replaces: none
- Follows review: [0008](../reviews/0008-demo-env-and-the-built-in-actions.md)

## Goal

A person uses a web page in a browser the engine opened, and the engine writes
what they did as a test file that runs. After this instruction the owner can
record a short flow on the demo app from a terminal, see the steps appear, and
watch the recorded test pass when it is played back.

This is the heart of the product for the owner: **record a flow, and it is
runnable. The engine must recognise each thing the user did and map it to the
right action.**

## Owner decisions

1. Pull requests #30 and #43 to #46 are merged. Every built-in action runs.
2. **The recorder comes now**, ahead of plan branches 14 to 16 (2026-10-10).
   Those follow in a later instruction; v0.1.0 is released after them.
3. The desktop track is not part of this instruction. Change nothing under
   `apps/desktop/`.

## Reviewer decisions

These settle how recording works, so that work can start. They are recorded as
an ADR in task 4. If one of them turns out to be wrong or impossible, stop and
say so in the report; do not work around it silently.

- **R4. A recording is an ordinary test file.** It uses the built-in actions
  and ordinary targets with ordered candidates. Nothing in a step file says
  that it was recorded.
- **R5. A recorded target is checked by the runner's own rule.** For the
  element the user touched, the recorder proposes candidates in the order of
  reliability (role and name, label, placeholder, test ID, text, CSS last). A
  candidate is written only if the same rule `ctx.locate` uses finds exactly
  that element with it, at the moment of the interaction. At least one
  candidate that is not CSS is wanted; if only CSS works, the step is written
  and marked for review.
- **R6. A recording is proven by playing it back.** "Verify" runs the recorded
  test with the normal runner, from a fresh browser context, and reports each
  step. A recording counts as runnable only after a verify that passed.
- **R7. Secrets never reach a file.** What is typed into a password field is
  never written to a step file, an event or a log. The step gets
  `${secrets.NAME}`: the name of a declared secret whose value equals what was
  typed, else a placeholder name, marked for review. The same replacement is
  made for any typed value that equals a declared secret.
- **R8. Nothing is dropped silently.** An interaction the recorder cannot map
  to an action is reported as a notice with what it saw. A step it is not sure
  of is written and marked for review, with the reason.
- **R9. Public Playwright API only.** If something needs a private one, the ADR
  must show that there is no public way, and a test must fail when a Playwright
  upgrade breaks it, as ADR 0007 did for the trace viewer.
- **R10. The engine records; clients only start, stop and listen.** The
  recorder knows nothing of any user interface. In this instruction the
  protocol does not change: no schema file is added or edited, so the desktop
  app's tests stay green. The messages a client will use are written down as a
  proposal (task 5).

## Branches

| Branch                     | Based on                   | What it holds          |
| -------------------------- | -------------------------- | ---------------------- |
| `fix/review-0008-findings` | `main`                     | Tasks 2 and 3          |
| `docs/recorder-design`     | `fix/review-0008-findings` | Tasks 4 and 5          |
| `feat/recorder-core`       | `docs/recorder-design`     | Tasks 6 to 10          |
| `feat/recorder-verify`     | `feat/recorder-core`       | Tasks 11 to 13, report |

Open one pull request per branch into `main` and say in each description which
branch it sits on. Push each branch when it is done and green, before starting
the next.

If a `feat/` branch passes roughly 1,500 changed lines (not counting fixtures,
generated files and the lockfile), split it in two stacked branches and say so
in the report. If a part cannot be finished, stop there and report.

`docs/` is the specification. Where this instruction and a document under
`docs/` disagree, stop and say so in the report.

## Tasks

### Part A: housekeeping and fixes

1. `git switch main` and `git pull --ff-only`. Delete the local branches that
   are now contained in `main`.
2. **Review 0008, finding 1** (`fix/review-0008-findings`). The test I11 in
   `packages/engine/src/runner/logins.integration.test.ts` cannot fail by
   chance: it must not look for short words in text that holds random hex.
   Look for other tests with the same weakness.
3. **Review 0008, finding 2.** When `select` times out because the option does
   not exist, the error names the option asked for and the options the element
   has. Update ADR 0014 for the `Authorization` ruling of review 0008.

### Part B: the design, written down first (`docs/recorder-design`)

4. **An ADR, "How recording works"**, with decisions R4 to R10, the
   alternatives you looked at (at least: Playwright's own code generator, and
   listening without checking candidates), and what each decision costs.
   Status: "Accepted (reviewer decisions R4 to R10, instruction 0009)".
5. **`docs/recording.md`**, the specification a tester and a client developer
   can read:
   - what a recording session is: its start (project, environment, start URL,
     optional saved login, the file to write) and its end;
   - **the mapping table**: each kind of interaction and the step it becomes;
   - how candidates are proposed and checked, and their order;
   - how typing becomes one `fill` per field;
   - what "marked for review" means and how it shows in the file (a comment
     above the step is enough; say what you chose);
   - what is not recorded yet;
   - **the protocol proposal**: the requests and events a client will use
     (start, stop, verify; a recorded step arriving, changing, a notice), with
     example messages. Marked "Proposed, not built". No schema file changes.

### Part C: the recording core (`feat/recorder-core`)

6. **A session.** The engine opens a visible Chromium with the same context
   settings a run uses (viewport, locale, timezone, an optional saved login),
   goes to the start URL, and records until the session is stopped or the
   browser is closed. The first step of every recording is the `goto`.
7. **Recognising interactions**, in every page and frame of the context,
   including pages that load later. In this part:

   | The user…                                  | Recorded as                               |
   | ------------------------------------------ | ----------------------------------------- |
   | clicks a button, link or other element     | `click`                                   |
   | types into a text field or text area       | one `fill` per field, with the final text |
   | chooses an option of a `<select>`          | `select`                                  |
   | ticks or unticks a checkbox, picks a radio | `check` (and uncheck)                     |
   | presses Enter, Tab or Escape in a field    | `press`                                   |
   | does something that opens a new tab        | that step gets `opens`                    |
   | acts inside a frame                        | the target gets its `frame`               |

   A click that only moves the focus into a field that is then typed into is
   not a step of its own. Anything else (hover menus, drag, file upload, the
   browser's back button, keyboard shortcuts) is a notice by R8 until a later
   part.

8. **Targets**, by R5. Role and accessible name come first. The test ID
   attribute is the project's `testIdAttribute`. When the same element is used
   again, the recording reuses the target it already made. Targets are written
   into the test file's own `targets`, with readable names made from the
   element (`todos.add`, `login.password`); say in the report how names are
   made and kept unique.
9. **Secrets**, by R7.
10. **Writing the file.** The recorded test is a valid test file at every
    moment of the session: it is validated by the engine's own validator
    before it is written, in the canonical form a person would write
    (shorthand where the documents show shorthand).

### Part D: verify, and a way to try it (`feat/recorder-verify`)

11. **Verify**, by R6: run the recorded file with the normal runner from a
    fresh context and report each step. After a failed verify, the report says
    which step failed and why, and the candidates that did not match.
12. **A developer command to try it**: `pnpm record`, a script of the
    repository (not the product's command line), which takes the project, the
    start URL and the file to write, opens the browser, prints each recorded
    step and notice as it happens, and on closing the browser runs verify and
    prints the result. Document it in `CONTRIBUTING.md` with the exact command
    for the demo app. It is temporary: the desktop app's Record screen replaces
    it.
13. **Tests.** Integration tests drive the recording browser the way a person
    does: real mouse moves and clicks at coordinates, real key presses, not
    locator calls. Record on the demo app, then check the file and that verify
    passes. Cover at least:
    - a to-do added on `/todos` (click, fill, press Enter or click Add);
    - the form sample's page (select, check, a radio);
    - a login on `/login`: the password is never in the file, the events or
      the output, and the step holds the declared secret's name;
    - a field inside the nested frames of `/frames`;
    - a link that opens a tab;
    - two elements with the same text, so a text candidate must be rejected
      and another kind chosen;
    - an element with nothing but CSS to go by: written, marked for review;
    - an interaction that cannot be mapped: a notice, no step.

## Done when

- [ ] `pnpm verify` passes on every branch, with no browser installed.
- [ ] `pnpm test:integration` passes on every branch, and the `integration` job
      passed twice in a row on each pull request.
- [ ] Every pull request is open, and CI is green on Linux, Windows and macOS
      for each, all three jobs.
- [ ] No file under `packages/protocol/schema/` changed, and
      `PROTOCOL_VERSION` is still `0.1.0`.
- [ ] `pnpm record` on the demo app, used by hand: a to-do added and a login,
      then the recorded test passes verify. The report says what you did and
      shows the recorded file.
- [ ] Every case of task 13 has a test.
- [ ] No password reaches a file, an event or the output, proven by a test
      that searches them.
- [ ] File header comments and TSDoc on every export, no `any`, and every lint
      or type suppression listed in the report with its reason.
- [ ] No npm dependency is added.
- [ ] No client package imports engine code. Nothing under `apps/desktop/`
      changed.
- [ ] `CHANGELOG.md` is updated in each branch.
- [ ] `handoff/reports/0009-the-recorder-first-part.md` exists on the last
      branch and follows [the template](../templates/report.md).

## Out of scope

- Any protocol change, and anything in the desktop app.
- Recording checks ("this text should be here"): they need a way for the user
  to say so, which comes with the desktop app's Record screen.
- Hover, drag, file upload, browser back and forward, keyboard shortcuts.
- Recording into an existing test, or into flows.
- Screenshots, page snapshots, the command-line `run`, the release (plan
  branches 14 to 16).
- Any AI feature.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- For each branch: its pull request number, the CI result of all three jobs,
  and its changed-line count.
- The recorded file of your hand-made session, as it was written.
- How an interaction is recognised, in a few sentences, and where the weak
  spots are: what kinds of pages or widgets will be recorded badly.
- How often a candidate was proposed and then rejected by the check of R5 in
  your tests, and for which kinds.
- Anything of R4 to R10 that you could not keep, and why.
- Every place where a document under `docs/` was unclear, wrong or silent and
  you had to choose.
- Anything in this instruction you think is wrong.
