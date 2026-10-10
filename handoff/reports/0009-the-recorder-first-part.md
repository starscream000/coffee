# Report 0009: the recorder, first part

- Date: 2026-10-10
- Written by: implementer
- Instruction: [0009](../instructions/0009-the-recorder-first-part.md)

## Summary

Review 0008's two findings are fixed. The recorder is designed (ADR 0022,
`docs/recording.md`), built and tested.

A recording session opens Chromium with a run's context settings. It records
clicks, typing (one `fill` per field), Enter/Tab/Escape, selects, checkboxes,
radios, tabs a step opens, and fields inside nested frames. It checks every
candidate with the runner's own rule at the moment of the interaction, never
writes a typed password, and writes a valid test file after every change.
Verify plays the file back with the normal runner, and `pnpm record` lets a
person try it from a terminal.

Two disagreements came up during the work; the owner ruled on both (see
Departures):

- the candidate order;
- the password placeholder.

Part C passed the line limit, so there are five stacked pull requests
(#48 to #52), not four.

**One done-when item is not done by me:** a hand-made session with
`pnpm record`. I cannot use a mouse and keyboard on a visible window. The
command is proven to start and record its first step; the owner's own session
is the remaining check (see "Not done").

## Branches and pull requests

Each branch sits on the one above it; every pull request targets `main` and
names its base.

| Branch                     | Based on                   | Last commit | Pull request | Pushed | Changed lines (against its base) |
| -------------------------- | -------------------------- | ----------- | ------------ | ------ | -------------------------------- |
| `fix/review-0008-findings` | `main`                     | `bf3fc86`   | #48          | yes    | +80 / −10                        |
| `docs/recorder-design`     | `fix/review-0008-findings` | `c12a8d6`   | #49          | yes    | +496 / −40                       |
| `feat/recorder-core`       | `docs/recorder-design`     | `c500a60`   | #50          | yes    | +664 / −12                       |
| `feat/recorder-session`    | `feat/recorder-core`       | `58df1b0`   | #51          | yes    | +1,134 / −8                      |
| `feat/recorder-verify`     | `feat/recorder-session`    | this report | #52          | yes    | about +1,250, with this report   |

### CI (verify, integration, desktop; each on Linux, Windows, macOS)

| Pull request | First run                                                         | Second run          |
| ------------ | ----------------------------------------------------------------- | ------------------- |
| #48          | all 9 jobs pass                                                   | in the pull request |
| #49          | all 9 jobs pass                                                   | in the pull request |
| #50          | all 9 jobs pass                                                   | in the pull request |
| #51          | 8 pass; `integration` on macOS failed in I3 (an 0008 test), below | in the pull request |
| #52          | in the pull request                                               | in the pull request |

**The macOS failure on #51.** The failing test was I3, "an assertion whose
actual value is 10 MB", from instruction 0008. On that runner, reading 10 MB
of text did not finish within the step's 3 seconds, so the failure had no
`actual` to truncate. The engine's message ("could not be read") was right;
the test's time budget was too tight. On #52 the step gets 10 seconds
(`4b11c05`). The failed job of #51 is re-run.

I re-run each pull request until its `integration` job has passed twice in a
row; the results are in each pull request. The `desktop` job passed
everywhere.

## Tasks

| Task | State | Notes                                                                                                                                                                                                                                         |
| ---- | ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done  | `main` pulled; the five merged local branches deleted.                                                                                                                                                                                        |
| 2    | done  | I11 matches login values as whole words. The other tests that look for a value in output search long or fixed strings (secrets, 48-hex tokens, fixed text) or fixed fixtures; none has the same weakness.                                     |
| 3    | done  | `select` keeps a margin; on a timeout with a missing option it fails with `OptionNotFound`: `"form.country" has no option "Atlantis". Its options: "" (Choose…), "de" (Germany), "fr" (France).` ADR 0014 records the `Authorization` ruling. |
| 4    | done  | ADR 0022, "Accepted (reviewer decisions R4 to R10, instruction 0009; owner ruling on unmatched passwords)", with four alternatives and the cost of each decision.                                                                             |
| 5    | done  | `docs/recording.md`: session, mapping table, candidates and their check, typing, secrets, review marks, notices, what is not recorded, verify, and the protocol proposal (Proposed, not built; no schema change).                             |
| 6    | done  | `RecordingSession.start`: visible Chromium (`headless` only for tests), `contextOptions()` shared with runs, optional saved login through `LoginStore`, the `goto` first. Ends on `stop()` or when the browser is closed.                     |
| 7    | done  | Every row of the table, in every frame and in tabs a step opens. Hover menus, drag, upload, back/forward, shortcuts give notices (hover has no event to notice; see weak spots).                                                              |
| 8    | done  | R5 order (after the owner's ruling); the project's `testIdAttribute`; the same element reuses its target; names below.                                                                                                                        |
| 9    | done  | R7, with the owner's ruling on the placeholder.                                                                                                                                                                                               |
| 10   | done  | Each change: render with `yaml`, validate with `Project.validate` (the engine's validator, buffer form), write only without errors. Canonical form: shorthand when only the shorthand parameter is given.                                     |
| 11   | done  | `verifyRecording()` runs the file through `RunManager` with an in-process channel (masked like any client's events). It reports each step and, for a failed one, its error and every candidate's match count.                                 |
| 12   | done  | `pnpm record --project … --url … --file … [--env] [--login]`, documented in `CONTRIBUTING.md` with the demo app command. A live run opened the visible browser and wrote the `goto`.                                                          |
| 13   | done  | Every case has a test in `recorder.integration.test.ts` (11 tests), driving the browser with `page.mouse` and `page.keyboard` at coordinates; each recording is verified.                                                                     |

## Checks

| Command                                                   | Result                                                                |
| --------------------------------------------------------- | --------------------------------------------------------------------- |
| `pnpm verify` (every branch)                              | pass; 414 tests on the last branch                                    |
| `pnpm test:integration` (last branch, clean worktree)     | pass: 14 files, 97 tests                                              |
| recorder integration tests, 4 runs in a row (Windows)     | 11 of 11 each time                                                    |
| `pnpm record --project examples/demo-app --url …/todos …` | opened the visible browser and printed `+ steps.0  {"goto":"/todos"}` |

No file under `packages/protocol/schema/` changed and `PROTOCOL_VERSION` is
still `0.1.0`. No lint or type suppression was added. No dependency was added.
Nothing under `apps/desktop/` changed.

## How an interaction is recognised

A page script is added to every frame of the recording context with
`addInitScript`. It listens in the capture phase, before the page's own
listeners:

- **Clicks.** A click on anything but a form field is held
  (`preventDefault`, `stopImmediatePropagation`). It is reported through a
  binding, and replayed with `element.click()` once the engine answers.
- **Typing.** It is collected per field and reported once, when the field is
  done: it loses the focus, Enter, Tab or Escape is pressed, or another
  interaction is reported.
- **Selects, checkboxes and radios** are reported from their `change` events.

The engine handles reports one at a time, in arrival order. For each touched
element:

1. It reads role and name from the first line of `locator.ariaSnapshot()`.
2. It proposes candidates from that and from what the script saw.
3. It keeps a candidate only if the locator `ctx.locate` would build matches
   exactly one element, and that element carries the touched element's marker
   attribute.

A field's target is found when it gets the focus, so a form that navigates on
Enter is no problem.

**Weak spots: pages and widgets that will record badly.**

- **Hover menus.** A hover is not an interaction the page reports. A click
  inside a hover menu is recorded, but on playback the menu is closed and the
  step fails.
- **Custom widgets built from `div`s** (date pickers, comboboxes, sliders).
  Without roles and names they give CSS-only targets marked for review, and
  their keyboard use gives "key" notices.
- **Pages that check `event.isTrusted`, or the position of a click.** The
  replayed click is untrusted and has no coordinates. A canvas, a map, or
  click-position menus behave differently while recording; playback is
  unaffected.
- **Actions on `mousedown` or `pointerdown` alone.** Some menus and drag
  handles act there, before any click, so they are not seen as clicks.
- **Rich text editors (`contenteditable`)** give a notice and no step.
- **Generated ids** (`#ember123`, `#radix-:r1:`) become CSS candidates that
  break on the next load. They are the last candidate, but a CSS-only target
  with one is fragile.
- **Keyboard use of a native `<select>` differs by system.** On macOS, arrow
  keys open its popup instead of changing it, and Enter submits the form;
  the recorder then records the browser's click on the submit button, not a
  `select`. The first macOS run of #52 showed this; the test now chooses the
  option by typing, which works the same everywhere.
- **Text that changes while typing.** A field whose accessible name changes
  as one types (a floating label) is checked at focus time and may keep the
  old name.

## Candidates proposed and rejected by the R5 check

The recorder integration tests, as summed from the session statistics:

| Kind        | Proposed | Rejected | Why rejected                                                                               |
| ----------- | -------- | -------- | ------------------------------------------------------------------------------------------ |
| role + name | 17       | 2        | "Details" (two buttons on `/details`), "Delete" (three rows on `/products`)                |
| label       | 9        | 0        |                                                                                            |
| placeholder | 0        | 0        | no demo field has one                                                                      |
| testId      | 3        | 0        |                                                                                            |
| text        | 8        | 3        | "Details" (twice), "Delete" (three times), "Sign in" (the heading and the button share it) |
| css         | 17       | 0        |                                                                                            |

## The recorded files

The hand-made session is not done (see below). These are the files the
integration tests recorded, as written. They are the same interactions the
hand-made session asks for, made with real mouse and key input.

A to-do added (click into the field, type, Enter; again, then the Add
button):

```yaml
version: 1
name: R1
targets:
  todos.newTodo:
    - role: textbox
      name: New to-do
    - label: New to-do
    - css: '#new-todo'
  todos.add2:
    - role: button
      name: Add
    - text: Add
    - css: '#add-form > button'
steps:
  - goto: /todos
  - fill: { target: todos.newTodo, value: Buy milk }
  - press: { target: todos.newTodo, key: Enter }
  - fill: { target: todos.newTodo, value: Walk the dog }
  - click: todos.add2
```

A login with the demo password typed into the field:

```yaml
version: 1
name: R3
targets:
  login.username2:
    - role: textbox
      name: Username
    - label: Username
    - css: '#username'
  login.password2:
    - role: textbox
      name: Password
    - label: Password
    - css: '#password'
  login.signIn:
    - role: button
      name: Sign in
    - css: '#login-form > button'
steps:
  - goto: /login
  - fill: { target: login.username2, value: alice }
  - fill: { target: login.password2, value: '${secrets.DEMO_PASSWORD}' }
  - click: login.signIn
```

Both pass verify. The login test searches the file, the session's events, the
printed output, the verify result and the run folder for the password, and
finds it nowhere.

## Departures from the instruction

- **Part C is two branches**, `feat/recorder-core` and `feat/recorder-session`
  (+1,800 lines together), so there are five pull requests. The recorder
  integration tests (task 13) are on `feat/recorder-verify` as the
  instruction says; `feat/recorder-session` is covered there.
- **Candidate order.** R5 (and `CLAUDE.md`) put the test ID before text, but
  `docs/step-format.md` put text first. I stopped and asked. The owner ruled
  for R5, so the table in `docs/step-format.md` is corrected (in #49).
- **R7's placeholder.** R7 says an unmatched password gets
  `${secrets.<placeholder>}`. An undeclared secret is an `UndeclaredSecret`
  validation error, so the file could not be valid at every moment (task 10).
  I stopped and asked. The owner ruled for a **placeholder variable**:
  `${vars.loginPassword}`, declared empty in the test's `vars`, marked for
  review. ADR 0022 and `docs/recording.md` record the ruling.

## Decisions I made

1. **Holding clicks.** To check candidates "at the moment of the
   interaction" for a click that navigates or removes its element, clicks are
   held and replayed with `element.click()`. Alternatives and costs are in
   ADR 0022.
2. **Review marks** are a YAML comment directly above the step:
   `# review: <reason>`.
3. **At most three candidates**: the first two that are not CSS, then CSS if
   it passes.
4. **The label candidate** uses the accessible name of a form field; the
   check rejects it when `getByLabel` does not find exactly that field.
5. **Target names.**
   - The form is `<page>.<element>`. `<page>` is the first path segment of
     the tab's top page, or `home`.
   - `<element>` is the first of: accessible name, placeholder, test ID,
     text, tag name, in lower camel case, at most four words. A word with a
     hyphen stays one word: "New to-do" → `newTodo`.
   - A name taken in the file **or by a shared target** gets 2, 3, …. Because
     of that, the demo's recordings get `todos.add2` and `login.username2`:
     the demo project's shared targets already use those names. Reusing a
     shared target that identifies the same element would be nicer, but R4
     and task 8 say the file's own targets.
6. **Frame targets** use CSS on the `<iframe>`, in this order: `id`, `title`,
   `name`, then the frame's position as a last resort (`css: 'iframe, frame'`
   with `nth`). They are not marked for review for being CSS only; the
   documents' own frame examples are CSS.
7. **Tab names** come from the new page's path (`/receipt` → `receipt`), and
   `tab` when it has none. A tab that opens more than 5 seconds after the last
   step, or from another page, is not attributed: a notice, and nothing in it
   is recorded.
8. **Enter in a form field.** The browser then makes its own click on the
   submit button. That click has `detail === 0` and comes within a second of
   the Enter, so it is not a step.
9. **Double-click** records the two clicks and adds a notice.
10. **`select`** records the option's label, or its value when two options
    share a label.
11. **Recorded text is escaped**: `${` is written as `$${`.
12. **`stop()` waits 200 ms** for interactions the page reported just before,
    so a click made right before stopping is not lost.
13. **A notice kind `writeFailed`** is added to the proposal. It means the
    recording would not be valid, so it was not written; the tests never saw
    it.
14. **Verify writes a run folder**, like any run (R6: "with the normal
    runner").
15. **Two small additions to existing code:**
    - `Project.mask()`, so verify and `pnpm record` mask what they print;
    - `candidateLocator` accepts a `Frame` as its scope.

## Questions for the owner

1. **Should the recorder reuse a shared target when its candidates find the
   same element?**
   - Yes, later (recommended): files would say `todos.add` instead of
     defining `todos.add2`. It needs a rule for when a shared target "is" the
     element, and R4/task 8 would change.
   - No: every recording is self-contained, as now.

## Not done, not pushed, not verified

- **The hand-made `pnpm record` session** (done-when: "a to-do added and a
  login, then the recorded test passes verify"). I cannot use the visible
  browser by hand. I ran `pnpm record` on the demo app: it opened the browser
  and wrote the `goto`. The same interactions, made with real mouse and key
  input, are recorded and verified in the integration tests (files above).
  For the owner, in two terminals:

  ```
  node examples/demo-app/server/server.ts
  pnpm record --project examples/demo-app --url http://localhost:4310/todos --file tests/recorded/add-todo.test.yaml
  ```

  Add a to-do, close the browser, and read the verify result. Then the same
  for a login, with `--url http://localhost:4310/login`, typing the password
  `demo-only-not-a-secret`.

- **No test drives a headed browser.** The tests run the recording browser
  headless (`headless: true`); `pnpm record` runs it headed.

## Suggestions

- The desktop app's Record screen can be built on `RecordingSession` and its
  listener as soon as the protocol proposal is accepted. The proposal maps
  one to one onto the listener's three methods.
