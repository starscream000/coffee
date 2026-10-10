# Review D0003: a complete app, from editing to running

- Date: 2026-10-10
- Written by: reviewer
- Instruction: [D0003](../instructions/D0003-a-complete-app-from-edit-to-run.md)
- Report: `apps/desktop/handoff/reports/D0003-a-complete-app-from-edit-to-run.md`
  on the branch `desktop/feat/finishing` (it reaches `main` when pull request
  #41 is merged)
- Verdict: **Approved**, all eleven pull requests (#31 to #41). One test must
  be fixed before the browser can be switched on in the `desktop` CI job
  (finding 1).

## Pull requests

| Pull requests | Branches, in merge order                                                                                                                                                           | Reviewed at (top) | CI                                            | Verdict  |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------- | --------------------------------------------- | -------- |
| #31 to #41    | `review-d0002`, `run-model`, `runs`, `run-history`, `step-reader`, `step-builder`, `step-forms`, `new-files`, `targets-editor`, `failure-to-target`, `finishing` (all `desktop/…`) | `29c3f96`         | all nine jobs green on each, on three systems | Approved |

Merge in the order of their numbers. Pull request
[#42](https://github.com/starscream000/coffee/pull/42), the reviewer's change
that gives the `desktop` CI job a browser, stays open until finding 1 is
fixed.

## What I checked

- **CI** on all eleven pull requests: `verify`, `integration` and `desktop`
  green on Linux, Windows and macOS. In those runs the tests that need a
  browser are skipped, because the `desktop` job has none yet.
- **The real-browser tests on three systems**, by adding Chromium to the
  `desktop` job on a branch on top of the stack (pull request #42) and making
  those tests fail instead of skip:
  - Linux and macOS: everything passes, including the tests that build a test
    in the step list, run it in Chromium, see it pass and fail, and reopen
    both runs from the history.
  - Windows: one test fails, finding 1. Every other real-browser test passes
    there too.
- **The report**, in full, and the four new requests.
- **Scope:** nothing outside `apps/desktop/` changed, and each branch sits on
  the one before, with `main` at the bottom.
- **Source read:** `StepEdits.cs`, the code that changes a step's lines in a
  file, because a mistake there damages the user's files. It works on text
  positions and leaves the rest of the file alone, as asked.

## What I could not check

- **Almost all of the source and the tests.** About 12,700 lines arrived; I
  read one file of them. I relied on CI on three systems and on the
  real-browser run above. This is a much lighter check than the earlier
  desktop reviews, at the owner's request for speed.
- **The app on a real screen.** The implementer could not click through it
  either; the walk-through's last steps were run through the view models.
  Only the owner can see whether the screens are usable.
- **A run with the browser shown**, and screenshots and page states, which the
  engine does not produce yet.

## Findings

1. **Must fix before #42. One real-browser test fails on Windows**
   (`RealEngineRunTests.Runs_demo_tests_in_a_real_browser`, line 61). It
   compares the run folder's path as text; the engine writes it with forward
   slashes (`C:/Users/…`), the test expects backslashes. The run itself had
   ended as expected by then. Compare paths as paths. Then check every place
   in the app that compares, joins or shows a path that came from the engine
   (`resultsDir`, screenshot paths) for the same assumption, and say in the
   report what you found.
2. **Note. The rest of that test never ran on Windows**, since it stops at the
   first failed check. Its later checks (each test's result, the history) are
   proven on Linux and macOS only until finding 1 is fixed.
3. **Note. No person has used these screens.** Eleven branches of user
   interface were built and tested without anyone seeing them. Expect a round
   of corrections once the owner has tried the app.

## Rulings on the report

- The splits into eleven branches are accepted.
- **Decisions:** all accepted. The YAML approach (a parser for positions,
  changes made on the text) is the right one for keeping comments. Form
  fields writing when they lose focus, and new files starting with one
  `goto: /` step, are fine.
- **Avalonia's build telemetry** (question 3): turn it off, as recommended.
  A build should not call out to anyone. In the next instruction.

## Answers to the requests

- **R0004, the structure of step files through the protocol: accepted, for
  later.** It is the right long-term shape: only the engine should know how a
  step file is read. It is a protocol addition, so it comes after the engine's
  v0.1.0 actions are in, together with the desktop change that uses it.
- **R0005, the browser install command from the engine: accepted, for later**,
  with R0004, as one protocol addition.
- **R0006, skipped steps that never started: accepted, for later**, as its
  option 1 (optional fields on `stepSkipped`), with the same protocol
  addition.
- **R0007, an engine test that fails by chance: accepted, now.** It goes into
  the engine track's next instruction as a fix.

## Owner decisions needed

1. **Accept ADR D0006 (AvaloniaEdit, the text editor component) and ADR D0007
   (YamlDotNet, the YAML reader)?** Recommended: yes to both. Both are in use
   and working on three systems.
2. **Try the app** and say what is wrong or missing. That is now the most
   useful input to this track.

## For the next instruction

Instruction D0004:

- Finding 1, so that #42 can be merged and the real-browser tests run in CI.
- Avalonia's build telemetry off.
- Whatever the owner reports after trying the app.
