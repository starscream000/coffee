# Recording

> Status: **Accepted design** (2026-10-10), decisions in
> [ADR 0022](adr/0022-how-recording-works.md). The protocol messages at the end
> are **Proposed, not built**.

A person uses a web page in a browser the engine opened, and the engine writes
what they did as a test file that runs. This document says what a recording
session is, which interaction becomes which step, how targets are chosen and
checked, and what is not recorded yet.

## A recording session

A session starts with:

| Input         | Required | Meaning                                                                                                  |
| ------------- | -------- | -------------------------------------------------------------------------------------------------------- |
| `project`     | yes      | The project's root folder                                                                                |
| `file`        | yes      | The test file to write, relative to the project root (`tests/add-todo.test.yaml`); it must not exist yet |
| `startUrl`    | no       | Where the browser starts: a full URL, or a path relative to the environment's `baseUrl` (default `/`)    |
| `environment` | no       | The environment, as for a run (default: the config's default)                                            |
| `login`       | no       | A saved login for the main page ([step-format.md](step-format.md#saved-logins)); the test gets `login:`  |
| `name`        | no       | The test's `name` (default: made from the file name, `add-todo.test.yaml` → "Add todo")                  |

The engine opens a **visible** Chromium with the same context settings a run
uses (`baseUrl`, viewport, locale, timezone, and the saved login's state when
one is given), goes to the start URL and starts listening. The first step of
every recording is that `goto` (`- goto: /todos`; a start URL outside
`baseUrl` is written in full).

The session ends when it is stopped, or when the person closes the browser.
The file is complete at that moment: the recorder writes it after every
change, and only when the engine's own validator finds no error in it, so the
file on disk is a valid test file at every moment of the session.

## What becomes which step

| The person…                                                     | Recorded as                                                                                             |
| --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| clicks a button, link or other element                          | `- click: <target>`                                                                                     |
| types into a text field or text area                            | one `- fill: { target: <target>, value: <final text> }` per field                                       |
| clicks into a text field and then types into it                 | the `fill` only; the click is not a step                                                                |
| clicks into a text field and does not type                      | `- click: <target>`, written when the next interaction comes                                            |
| types into a password field, or types a declared secret's value | `fill` with `${secrets.NAME}`, or a placeholder variable (see [Secrets](#secrets))                      |
| chooses an option of a `<select>`                               | `- select: { target: <target>, option: <label> }` (the option's value when two options share a label)   |
| ticks a checkbox                                                | `- check: <target>`                                                                                     |
| unticks a checkbox                                              | `- check: { target: <target>, checked: false }`                                                         |
| picks a radio button                                            | `- check: <target>`                                                                                     |
| clicks the label of a checkbox or radio button                  | the same as clicking the control itself                                                                 |
| presses Enter, Tab or Escape in a field                         | the field's `fill`, then `- press: { target: <target>, key: Enter }`                                    |
| presses Enter in a form field, which submits the form           | the `press` only; the click the browser makes on the form's submit button is not a step                 |
| does something that opens a new tab or pop-up                   | that step gets `opens: <name>`, the name made from the new page's path (`/receipt` → `receipt`)         |
| acts in that tab                                                | its steps get `page: <name>`                                                                            |
| acts inside a frame                                             | the target gets `frame:` (a target for the `<iframe>`, itself with `frame:` for a frame inside a frame) |

### Typing becomes one `fill` per field

Typing is not recorded key by key. The recorder waits until the person is done
with the field and writes one `fill` with the field's final text. The person
is done when:

- the field loses the focus;
- they press Enter, Tab or Escape in it;
- or any other interaction is recorded.

When the person comes back to the same field and types again, with no other
step in between, the same `fill` step changes; otherwise a new `fill` is
written.

## Targets

Every step that touches an element gets a target in the test file's own
`targets`. The same element used again reuses its target.

### Candidates, and how they are checked

For the element the person touched, the recorder proposes candidates in this
order (as [step-format.md](step-format.md#candidates) lists them):

1. `role` and `name`: the element's role and accessible name, as Playwright's
   accessibility tree gives them;
2. `label`: for form fields, the accessible name;
3. `placeholder`;
4. `testId`: the value of the project's `testIdAttribute`;
5. `text`: the element's visible text, whitespace collapsed (80 characters at
   most);
6. `css`: `#id` when the element has an id, otherwise a path of tag names,
   classes and `:nth-of-type()`.

**A candidate is written only if it finds exactly that element.** At the
moment of the interaction, the recorder builds the same Playwright locator
`ctx.locate` would build for the candidate, in the element's frame. It keeps
the candidate only if:

- the locator matches exactly one attached element (hidden ones count, except
  for `role`, as in a run; [ADR 0010](adr/0010-locator-candidates.md));
- and that element is the one the person touched.

A click is held until its check is done, so an element that disappears
because of the click is still there to check.

Of the candidates that pass, the recorder writes the first two that are not
CSS, then CSS if it passes, so a target has at most three candidates. When
only CSS passes, the step is written and **marked for review** ("only CSS
identifies this element"). `nth` and `within` are never written yet.

A frame's target gets CSS candidates on its `<iframe>` element:
`iframe#id`, `iframe[title="…"]`, `iframe[name="…"]`, in that order, checked
the same way. A frame target is not marked for review for being CSS only.

### Names of targets

A target's name has the form `<page>.<element>`, for example `todos.add` or
`login.password`:

- **`<page>`** is the first segment of the path of the page the element is on
  (`/todos` → `todos`, `/frames/payment` → `frames`, `/` → `home`). For an
  element inside a frame, it is the path of the tab's top page.
- **`<element>`** comes from the first of these the element has: accessible
  name, placeholder, test ID, text, tag name. It is written in lower camel
  case, at most four words, letters and digits only ("New to-do" → `newTodo`,
  "Sign in" → `signIn`).
- **Frame targets** end in `Frame` (`frames.checkoutFrame`).

A name already used for another element, in the file or among the project's
shared targets, gets a number: `todos.add2`, `todos.add3`.

## Secrets

What is typed into a password field never reaches the file, an event, a log
or the recorder's output:

- When a **declared secret** has exactly the typed value, the step gets
  `${secrets.NAME}`. This applies to any field, not only password fields.
- When **no declared secret** has the value of a password field, the step gets
  a **placeholder variable** made from the target's name (`login.password` →
  `${vars.loginPassword}`). The test's own `vars` declares it with an empty
  value, and the step is marked for review. Declare the secret, then replace
  the placeholder with `${secrets.NAME}`.

The typed value is compared with the secrets in the engine's memory and then
dropped. A verify of a test that still uses a placeholder fails, as it should.

## Marked for review

A step the recorder is not sure of is written, with a comment directly above
it that starts with `# review:` and gives the reason:

```yaml
steps:
  - goto: /login
  - fill: { target: login.username, value: alice }
  # review: typed into a password field, and no declared secret has this value; declare a secret and use ${secrets.NAME}
  - fill: { target: login.password, value: '${vars.loginPassword}' }
  # review: only CSS identifies this element
  - click: login.thing
```

The comment changes nothing when the test runs. A person deletes it once the
step is right. The reasons:

| Reason                                                                  | When                                         |
| ----------------------------------------------------------------------- | -------------------------------------------- |
| `only CSS identifies this element`                                      | no candidate other than CSS passed the check |
| `typed into a password field, and no declared secret has this value; …` | a password with no matching declared secret  |

## Notices

An interaction the recorder sees but cannot map to an action is reported as a
**notice** that says what it saw; no step is written. Notices go to the
session's listener (and, with the protocol proposal, to the client). They are
never written into the file.

## Not recorded yet

| The person…                                      | What happens                                                                                                                         |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------ |
| drags an element                                 | a notice; no step                                                                                                                    |
| chooses a file in a file field                   | a notice; no step                                                                                                                    |
| right-clicks                                     | a notice; no step                                                                                                                    |
| double-clicks                                    | the two clicks are recorded, and a notice says a double-click is not                                                                 |
| presses a keyboard shortcut outside a text field | a notice; no step (shortcuts inside a text field only change its text, which the `fill` holds)                                       |
| presses other keys outside a text field          | a notice; no step                                                                                                                    |
| goes back or forward with the browser            | a notice; no step                                                                                                                    |
| hovers to open a menu                            | nothing is recorded for the hover; a click inside the menu is recorded, and the test may fail on playback because the menu is closed |
| types an address into the address bar            | not recognised; the next interaction is recorded on the new page                                                                     |
| edits a `contenteditable` element                | a notice; no step                                                                                                                    |
| wants a check ("this text should be here")       | not available yet: it needs a way to say so, which the desktop app's Record screen will bring                                        |

Recording into an existing test, or into a flow, is not available yet: the
file must not exist.

## Verify

A recording counts as **runnable** only after a verify that passed
([ADR 0022](adr/0022-how-recording-works.md), R6). Verify runs the recorded
file with the normal runner, exactly as `startRun` does: a fresh browser
context, the environment and saved login of the recording, a run folder, and
the usual events. It reports each step. After a failed verify, the report
names the step that failed, its error, and the candidates that matched no
element or several.

## Protocol proposal

**Proposed, not built.** No schema file contains these messages yet; the
engine does not answer them. They follow the conventions of
[protocol.md](protocol.md).

### Requests

| Method            | Params                                                 | Result                  |
| ----------------- | ------------------------------------------------------ | ----------------------- |
| `startRecording`  | `file`, `startUrl?`, `environment?`, `login?`, `name?` | `{ recordingId, file }` |
| `stopRecording`   | `recordingId`                                          | `{ file, steps }`       |
| `verifyRecording` | `recordingId`                                          | `{ runId, resultsDir }` |

`startRecording` is refused with `RecordingInProgress` while a recording or a
run is going on, with `FileExists` when the file exists, and with the errors
of `startRun` for the environment, the login and the browser. `verifyRecording`
starts an ordinary run of the recorded file, whose events are the usual run
events, followed by `recordingVerified`.

### Events

| Event               | Params                                                                                                                                                             |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `recordingStarted`  | `recordingId`, `file`, `startUrl`                                                                                                                                  |
| `stepRecorded`      | `recordingId`, `index`, `step` (as written, canonical long form), `targets` (new targets), `review?`                                                               |
| `stepChanged`       | `recordingId`, `index`, `step`, `review?`: a `fill` whose text changed, or a step that got `opens`                                                                 |
| `recordingNotice`   | `recordingId`, `kind` (`drag`, `fileChooser`, `contextMenu`, `doubleClick`, `shortcut`, `key`, `history`, `contentEditable`, `unmapped`), `message`, `page`, `url` |
| `recordingStopped`  | `recordingId`, `file`, `reason` (`stopped`, `browserClosed`), `steps`                                                                                              |
| `recordingVerified` | `recordingId`, `runId`, `status` (`passed`, `failed`)                                                                                                              |

Every message is masked like every other message of the engine; a recorded
`fill` never carries a typed password in the first place.

### Example messages

```json
{"jsonrpc":"2.0","id":7,"method":"startRecording","params":{"file":"tests/add-todo.test.yaml","startUrl":"/todos"}}
{"jsonrpc":"2.0","id":7,"result":{"recordingId":"rec-20261010-101500-1a2b","file":"tests/add-todo.test.yaml"}}
{"jsonrpc":"2.0","method":"stepRecorded","params":{"recordingId":"rec-20261010-101500-1a2b","index":1,"step":{"action":"fill","params":{"target":"todos.newTodo","value":"Buy milk"}},"targets":{"todos.newTodo":[{"role":"textbox","name":"New to-do"},{"label":"New to-do"},{"css":"#new-todo"}]}}}
{"jsonrpc":"2.0","method":"stepRecorded","params":{"recordingId":"rec-20261010-101500-1a2b","index":2,"step":{"action":"press","params":{"target":"todos.newTodo","key":"Enter"}},"targets":{}}}
{"jsonrpc":"2.0","method":"recordingNotice","params":{"recordingId":"rec-20261010-101500-1a2b","kind":"drag","message":"A drag of listitem \"Banana\" is not recorded yet.","page":"main","url":"http://localhost:4310/interactions"}}
{"jsonrpc":"2.0","id":8,"method":"stopRecording","params":{"recordingId":"rec-20261010-101500-1a2b"}}
{"jsonrpc":"2.0","method":"recordingStopped","params":{"recordingId":"rec-20261010-101500-1a2b","file":"tests/add-todo.test.yaml","reason":"stopped","steps":3}}
{"jsonrpc":"2.0","id":8,"result":{"file":"tests/add-todo.test.yaml","steps":3}}
```
