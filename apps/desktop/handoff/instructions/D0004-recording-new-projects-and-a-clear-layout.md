# Instruction D0004: recording, new projects and a clear layout

- Date: 2026-10-10
- Written by: reviewer
- Track: desktop
- Based on: the branch `feat/protocol-new-project` of engine instruction 0010
  (not `main`; see "Branches")
- Replaces: none
- Follows review: [D0003](../reviews/D0003-a-complete-app-from-edit-to-run.md)

## Goal

The owner opens the app and can tell at a glance what each part of the window
is. He creates a new project from the app, records his first test in a
browser, sees it verified, runs it and edits it, and uses everything else the
engine offers, without writing YAML.

## Owner decisions

1. **One implementer now does both tracks** (2026-10-10). You carry on the
   desktop work. **The tracks stay separate:** this instruction changes
   nothing outside `apps/desktop/`, its report goes in
   `apps/desktop/handoff/reports/`, and its branches are named
   `desktop/<type>/<name>`. Engine instruction 0010 comes first; finish and
   report it before starting here.
2. **The owner has used the app** (a screenshot of 2026-10-10 is the basis of
   Part B). His words: the window is all black, which is fine, but "the
   boundaries of the subsections and their hierarchies are unclear and
   confusing". He wants a better user interface and a clear panel hierarchy.
3. **"Create new project" is missing entirely.** Add it.
4. **The app must use everything the engine has**, recording first of all.
5. The app is meant to be low-code or no-code (2026-10-10).
6. **Not decided:** ADR D0006 (AvaloniaEdit) and ADR D0007 (YamlDotNet) stay
   "Proposed". A shortcut for "Save all" is not decided; do not add it.

## What the engine offers now

On the branch you start from (read `docs/protocol.md` and `docs/recording.md`
there):

- **Recording:** `startRecording`, `stopRecording`, `verifyRecording`, and the
  events `recordingStarted`, `stepRecorded`, `stepChanged`, `recordingNotice`,
  `recordingStopped`, `recordingVerified`. The engine opens its own visible
  browser; the app only starts, stops and listens.
- **`createProject`**, which creates and opens a new project.
- **Every built-in action runs**, and flows can be called.
- `capabilities.installCommand`, and `stepSkipped` with the step's section,
  action, title and location.
- Protocol version `0.1.1`.

Still missing in the engine: screenshots and page states. The app's place for
them stays as it is.

## Branches

All stacked, in this order. One pull request per branch into `main`; say in
each description which branch it sits on.

| Branch                        | Based on                                   | What it holds           |
| ----------------------------- | ------------------------------------------ | ----------------------- |
| `desktop/feat/protocol-0-1-1` | `feat/protocol-new-project` (engine, 0010) | Tasks 1 to 5            |
| `desktop/feat/design-system`  | `desktop/feat/protocol-0-1-1`              | Tasks 6 and 7           |
| `desktop/feat/shell-layout`   | `desktop/feat/design-system`               | Tasks 8 to 11           |
| `desktop/feat/new-project`    | `desktop/feat/shell-layout`                | Tasks 12 and 13         |
| `desktop/feat/record`         | `desktop/feat/new-project`                 | Tasks 14 to 17          |
| `desktop/feat/project-tree`   | `desktop/feat/record`                      | Tasks 18 to 20          |
| `desktop/docs/screens`        | `desktop/feat/project-tree`                | Tasks 21 and 22, report |

The first branch makes the `desktop` CI job green again after the engine's
protocol change; the reviewer merges it together with the engine's pull
requests. If a branch passes roughly 1,500 changed lines (not counting lock
files and images), split it in two stacked branches and say so in the report.
Push each branch when it is done and green, before starting the next.

## Tasks

### Part A: catch up with the engine (`desktop/feat/protocol-0-1-1`)

1. **The folder's own documents** (`CLAUDE.md`, `handoff/README.md`, the
   README) say how the tracks work now (owner decision 1).
2. **C# types for protocol 0.1.1**: the recording requests and events,
   `createProject`, `installCommand`, the new `stepSkipped` fields. The
   contract tests pass again.
3. **Review D0003, finding 1.** The real-browser test that fails on Windows
   compares paths as paths. Check every place that compares, joins or shows a
   path that came from the engine. You now work on Windows: run the desktop
   tests there with `DESKTOP_TESTS_REQUIRE_ENGINE=1` and
   `DESKTOP_TESTS_REQUIRE_BROWSER=1`, and say in the report that they pass.
4. **Avalonia's build telemetry is off** for every build of the app.
5. **Use the two small additions:** the install command comes from the engine,
   and a step that was skipped without starting shows in its place in the run
   view, greyed out, not in the messages.

### Part B: a design the eye can read (`desktop/feat/design-system`, `desktop/feat/shell-layout`)

What is wrong today, from the owner's screenshot: every surface is the same
black; no line or tone separates the explorer, the step list, the form and the
text; the bottom panel is an unexplained grey block; every button looks the
same, so nothing says what the main action is; the active tab is barely
marked; and the step list, its form and the YAML text sit in three corners of
the same area.

6. **Design tokens, in one resource file.** Colours, spacing, corner radius
   and text sizes are named once and used everywhere; no colour is written in
   a view. The dark theme stays. Use these values or better ones, and record
   the choice in an ADR:

   | Token       | Value     | Used for                                         |
   | ----------- | --------- | ------------------------------------------------ |
   | window      | `#0F1115` | behind everything                                |
   | panel       | `#161A20` | every panel's body                               |
   | raised      | `#1D222A` | panel headers, cards, inputs, the selected tab   |
   | line        | `#2A303A` | the 1-pixel line between panels and around cards |
   | text        | `#E6E8EB` | normal text                                      |
   | text, muted | `#9AA3AF` | labels, hints, counts                            |
   | accent      | `#4C8DFF` | the main action, selection, focus                |
   | passed      | `#3FB950` |                                                  |
   | warning     | `#D29922` |                                                  |
   | failed      | `#F85149` |                                                  |

   Spacing in steps of 4 (4, 8, 12, 16, 24). Text sizes: 12 for labels and
   counts, 13 for body, 15 for panel titles, 20 for page titles. A monospaced
   font only for file text and code.

7. **Four kinds of button, and only four:** main (filled with the accent; at
   most one per region), normal, quiet (for toolbars), and danger (Delete,
   Remove; set apart from the others, and asking before it destroys work).
   Inputs, lists, tabs and cards get one look each from the tokens. Every
   control shows keyboard focus.

8. **The window has named regions, each with a visible edge:**

   ```
   ┌───────────────────────────────────────────────────────────────────┐
   │ Top bar: project ▾  environment ▾     [Record] [Run ▾]   engine ● │
   ├──────────────┬──────────────────────────────────┬─────────────────┤
   │ PROJECT      │ tab │ tab │ tab                   │ DETAILS         │
   │  search      ├──────────────────────────────────┤  of what is      │
   │  Tests       │  the open file:                   │  selected:      │
   │  Flows       │  Steps │ Targets │ Text           │  a step's form, │
   │  Targets     │                                   │  a target's     │
   │  Config      │                                   │  candidates     │
   ├──────────────┴──────────────────────────────────┴─────────────────┤
   │ Problems │ Runs │ Engine log                            (collapse) │
   ├───────────────────────────────────────────────────────────────────┤
   │ status bar                                                         │
   └───────────────────────────────────────────────────────────────────┘
   ```

   - Each region is a panel: a header strip with its title in the `raised`
     tone, a body in the `panel` tone, and a `line` between it and its
     neighbours. Nested parts sit on a lighter tone than what holds them,
     never the same.
   - The splitters between regions can be dragged and are visible on hover.
     The bottom panel and the details panel can be collapsed. Sizes and
     collapsed states are remembered.
   - **Record** and **Run** are the two main actions and live in the top bar.
     Run offers: the open test, the selected tests, a tag, all.

9. **One place for each thing.** In a test's tab, "Steps", "Targets" and
   "Text" are three views of the same file, switched at the top of the tab.
   The steps view is a list of step cards grouped under `before`, `steps` and
   `after`, each group with its own "Add step". Selecting a step shows its
   form in the details panel, not under the list. Actions on a step (move,
   duplicate, remove) sit on the selected card or in its menu, not in a row of
   buttons above the list.
10. **Empty and busy states say something:** no project, no tests, no
    problems, no runs yet, engine starting, engine not found.
11. **Nothing is lost on the way:** every feature of today's app is still
    reachable, and its tests pass.

### Part C: new projects (`desktop/feat/new-project`)

12. **A start page worth the name**, shown when no project is open: "New
    project" and "Open project" as the two actions, the recent projects, and
    the engine's state.
13. **New project.** Ask for the project's name, the folder to create it in,
    the address of the application under test, and the first environment's
    name. Create it with `createProject`, open it, and offer "Record your
    first test". A folder that is not empty, or cannot be written, gives the
    engine's message next to the field.

### Part D: recording (`desktop/feat/record`)

14. **Starting.** Record asks for the test's name (the file name is made from
    it, under `tests/`), the start address (the environment's base address by
    default), the environment, and optionally a saved login. Without a
    browser, the engine's install command is shown instead.
15. **While recording**, a Record tab shows the steps as they arrive, as the
    same step cards as the steps view; a step that changes (typing) updates in
    place. Notices ("a drag is not recorded yet") show in a list beside them.
    A step marked for review carries a visible mark and its reason. A Stop
    button ends the session; closing the browser ends it too.
16. **After recording**, the app verifies the recording by itself and shows
    the result as a run. Passed: "This test runs", with "Open test". Failed:
    which step, why, and "Open test" at that step. A password that matched no
    declared secret (a placeholder variable marked for review) is explained in
    plain words, with what to do.
17. **Rules:** one recording at a time, and none during a run, said clearly
    when refused; the engine stopping mid-recording keeps what was recorded
    and says so; the recorded test appears in the project tree at once.

### Part E: the rest of the engine (`desktop/feat/project-tree`)

18. **The project panel lists everything in the project**, in groups: tests
    (as today, with names, tags and row counts), flows, targets files, and the
    config file. New, rename and delete work from each group.
19. **A test's own settings without YAML:** at least its name, tags and skip
    reason, in the details panel when the test itself is selected. For the
    parts you do not get to (data rows, variables, pages and logins), the text
    view remains; say in the report which.
20. **A `call` step picks its flow** from the project's flows, and shows the
    flow's parameters as fields.

### Part F: seen by a person (`desktop/docs/screens`)

21. **Use the app by hand on Windows**, in the published build: create a new
    project for the demo app (`http://localhost:4310`, with the demo server
    running), record a test that adds a to-do, see it verified, run it with
    the browser shown, edit a step in its form, run again, open the run from
    the history. Fix what you find.
22. **Screenshots of the real window**, committed under
    `apps/desktop/docs/screens/` and shown in the README: the start page, the
    new-project form, a project with a test open in the steps view, the
    targets view, a recording in progress, a verified recording, a failed run
    with its error, and the run history. If you cannot capture the real
    window, render them headless and say so.

## Done when

- [ ] `apps/desktop/scripts/verify.ps1` (or `.sh`) and the root `pnpm verify`
      pass on every branch.
- [ ] Every pull request is open and all CI jobs are green on Linux, Windows
      and macOS.
- [ ] The desktop tests pass on Windows with the real engine and a real
      browser required.
- [ ] No colour, size or spacing value is written in a view; a test or a
      script checks that.
- [ ] A new project can be created, a test recorded into it, verified, run and
      edited, all from the app; one real-engine test does this from end to
      end (the recording browser headless, for the test only).
- [ ] The screenshots of task 22 are in the repository.
- [ ] File header comments and XML documentation on every public type and
      member; no `dynamic`; every `#pragma warning disable` listed in the
      report with its reason.
- [ ] No package is added. If one is needed, stop and say why.
- [ ] **Nothing outside `apps/desktop/` changed.**
- [ ] The desktop `CHANGELOG.md` is updated in each branch.
- [ ] `apps/desktop/handoff/reports/D0004-recording-new-projects-and-a-clear-layout.md`
      exists on the last branch and follows the root report template.

## Out of scope

- Anything outside `apps/desktop/`. If the engine lacks something, write a
  request in `handoff/requests/` and go on.
- A light theme.
- A form for the config file (environments, secrets, saved logins): it is
  edited as text for now.
- Recording checks, hover, drag and upload: the engine does not record them
  yet.
- Completion and syntax colouring in the text view.
- Installers (milestone D6).
- Any AI feature.
- Merging into `main`, pushing to it, or force-pushing anything.

## Report back

- For each branch: its pull request number, the CI result of all jobs on three
  systems, and its changed-line count.
- What you did by hand in task 21, what you found, and what you fixed.
- The screenshots, by file name, and whether each is the real window.
- Where the new layout departs from the sketch in task 8, and why.
- What of the engine the app still cannot use, and why.
- Every place where you had to choose because this instruction or a document
  was unclear, wrong or silent.
- Anything in this instruction you think is wrong.
