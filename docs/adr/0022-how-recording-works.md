# 0022. Record by listening in the page, and check every candidate with the runner's rule

- Status: Accepted (reviewer decisions R4 to R10, instruction 0009; owner ruling on
  unmatched passwords, 2026-10-10)
- Date: 2026-10-10

## Context

The recorder is the heart of the product for the owner: a person uses a web
page in a browser the engine opened, and the engine writes a test file that
runs. "Runs" is the hard part. A recording is only worth something if every
step finds the same element on playback that the person touched, and if no
secret the person typed ends up in a file. The engine must recognise each
thing the person did and map it to one of the built-in actions, and say so
when it cannot.

The recorder lives in the engine (R10). The engine already has a validator, a
runner, `ctx.locate` with its "exactly one element" rule (ADR 0010), declared
secrets with masking (ADR 0014) and saved logins (ADR 0018). The recorder must
build on these rather than beside them.

A spike with Playwright 1.64.0 and Chromium (throwaway code, not in the
repository) recorded on the demo app with public API only:

- an init script in every frame of the context, including the iframe in an
  iframe on `/frames` and tabs opened later;
- a binding back to Node;
- `locator.ariaSnapshot()` for the touched element's role and accessible name;
- a candidate check of the form "this locator matches exactly one element, and
  that element is the one touched".

Every interaction of the spike (a to-do added, a checkbox, typing in a nested
frame, a link that opens a tab) was recognised, and every role candidate
passed its check.

## Decision

**R4. A recording is an ordinary test file.** It uses the built-in actions and
ordinary targets with ordered candidates, written into the test file's own
`targets`. Nothing in a step file says that it was recorded; a "marked for
review" step carries a YAML comment above it, which a person may delete.

**R5. A recorded target is checked by the runner's own rule.** For the element
the person touched, the recorder proposes candidates in the order of
reliability:

1. role and name;
2. label;
3. placeholder;
4. test ID (the project's `testIdAttribute`);
5. text;
6. CSS, last.

A candidate is written only if the locator `ctx.locate` would build for it
(`candidateLocator`, the same code) matches exactly one attached element at
the moment of the interaction, and that element is the one touched.

Two mechanisms make "the moment of the interaction" real:

- **The element is marked.** The page script gives the element a temporary
  attribute. The check is then
  `candidate.count() === 1 && candidate.and(marked).count() === 1`.
- **Clicks are held.** The page script holds a click (`preventDefault` and
  `stopImmediatePropagation` in the capture phase) until Node has checked the
  candidates, then replays it with `element.click()`. A link that navigates or
  a button that removes its own row is therefore still there when it is
  checked.

At least one candidate that is not CSS is wanted. If only CSS works, the step
is written and marked for review. Frames get CSS candidates on the `<iframe>`
element (`id`, `title`, `name`), as the documents' own examples do; a frame
target with only CSS is not marked.

**R6. A recording is proven by playing it back.** "Verify" runs the recorded
file with the normal runner (`RunManager`, the same code as `startRun`), in a
fresh browser context, and reports each step. A recording counts as runnable
only after a verify that passed.

**R7. Secrets never reach a file.** What is typed into a password field never
reaches a step file, an event or a log:

- When a declared secret has the typed value, the step gets
  `${secrets.NAME}`. The same applies to any typed value, in any field, that
  equals a declared secret.
- When no declared secret has the value, the step gets a **placeholder
  variable**, `${vars.<name>}`. The test's own `vars` declares it with an
  empty value, and the step is marked for review. This is the owner's ruling
  of 2026-10-10: a placeholder secret would be an `UndeclaredSecret` error,
  and the file must stay valid at every moment.

The typed value travels from the page to Node only in memory, is compared
there, and is dropped.

**R8. Nothing is dropped silently.** An interaction the recorder cannot map
to an action is reported as a notice that says what it saw. A step it is not
sure of is written and marked for review, with the reason.

**R9. Public Playwright API only.** The recorder uses:

- `BrowserContext.addInitScript`;
- `BrowserContext.exposeBinding`;
- `Locator.ariaSnapshot`, `count`, `and`, `getAttribute`;
- `Frame.parentFrame`, `Frame.frameElement`, `ElementHandle.contentFrame`;
- the `page` event.

No private API is needed. The format of `ariaSnapshot` output is public
(Playwright's "ARIA snapshots"). The recorder reads only the first line,
`- role "name"`. If an upgrade changes that line, the recorder's integration
tests fail, because every recorded role candidate comes from it.

**R10. The engine records; clients only start, stop and listen.** The
recorder is an engine module, `packages/engine/src/recorder/`. Its interface
is a class that emits recorded steps, changed steps and notices to a
listener, and knows nothing of any user interface. In this instruction the
protocol does not change. The requests and events a client will use are a
proposal in [recording.md](../recording.md#protocol-proposal).

## Alternatives rejected

**Playwright's own code generator** (`playwright codegen`, or the recorder
behind `page.pause()`) is the obvious candidate. It is rejected for these
reasons:

- **It is not public API.** It is driven through the inspector or the CLI.
  The recorder's internals, the code it emits and its selector engine are not
  part of the library's documented interface, so R9 rules it out.
- **It writes code, not step files.** Its output is TypeScript or another
  language with one locator per action. Turning that into actions with ordered
  candidates means parsing generated code whose shape can change in any
  release.
- **Its selectors follow its own preferences.** It picks one locator by its
  own ranking (and prefers test IDs). It does not check a list of candidates
  with our runner's "exactly one element" rule (ADR 0010), which hidden
  elements make different from Playwright's.
- **It knows nothing of the project.** It cannot know declared secrets, so a
  password would be written as typed. It cannot know the project's
  `testIdAttribute` or saved logins either.

**Listening without checking candidates**: the page script writes selectors
it computes itself, and the engine trusts them. It is rejected for these
reasons:

- **It is cheaper but unproven.** An accessible-name or role computation of
  our own would differ from Playwright's in edge cases, and every difference
  is a step that fails on playback.
- **It misses ambiguity.** Two elements with the same text would get the
  same text candidate unless the script also counted matches the way the
  runner does.
- **It breaks R5.** R5 exists precisely so that a recorded step finds, on
  playback, the element that was touched.

**Checking candidates after the event without holding it** fails the moment a
click navigates away or removes its element. The spike showed that a link's
navigation starts before Node can count anything.

**Pausing the page in the debugger (CDP) while checking** stops the page's
main thread. Playwright's own queries run on that thread, so the check could
never finish.

## Consequences

What each decision costs:

- **R4**: the recorder writes its own targets, even where a shared target
  already describes the element (reusing shared targets is a later
  improvement). Review marks are comments, which tools must keep when they
  edit the file (they do: [step-format.md](../step-format.md#editing-by-tools)).
- **R5**:
  - Every click is delayed by the candidate check: a few locator counts, in
    the order of 10 to 50 ms on the demo app.
  - The replayed click is `element.click()`. That click is not trusted and
    has no coordinates, so a page that checks `isTrusted` or the click
    position behaves differently while recording. The engine's playback is
    unaffected.
  - The marker attribute is visible to the page's own scripts for a moment.
  - Candidates are only as good as the page's accessibility tree: a page of
    unlabelled `div`s yields CSS and review marks.
- **R6**: a verify is a full run, with a run folder, and takes as long as one.
- **R7**:
  - A password with no declared secret leaves a test that fails until a
    person declares the secret and changes the step.
  - Comparing every typed value with every declared secret is cheap for the
    handful of secrets a project declares.
- **R8**: there will be notices for things a tester expects to work (drag,
  hover menus, file upload), until later parts map them.
- **R9**: the one dependency on output format is the first line of
  `ariaSnapshot`, guarded by tests.
- **R10**: a client sees nothing of a recording until the protocol proposal
  is accepted and built. Until then, `pnpm record` is the way to try it.

## Revisit when

- Playwright offers a public, documented API for its recorder or its selector
  generator.
- `ariaSnapshot`'s first line no longer carries role and name.
- Pages the owner records show that holding and replaying clicks breaks real
  applications (for example, ones that check `isTrusted`).
