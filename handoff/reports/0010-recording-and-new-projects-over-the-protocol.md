# Report 0010: recording and new projects over the protocol

- Date: 2026-10-10
- Written by: implementer
- Instruction: [0010](../instructions/0010-recording-and-new-projects-over-the-protocol.md)

## Summary

A client can now do everything `pnpm record` does over the protocol:

- start a recording and receive each step as it is recorded;
- stop it and verify it;
- create a new project.

The protocol is `0.1.1`. It adds `startRecording`, `stopRecording`,
`verifyRecording`, six recording events, `createProject`,
`capabilities.installCommand` and the skipped-step details.

The recorder scopes a repeated element `within` its container instead of
using its position. A click on the page background gives a clear notice, and
`pnpm record` names an argument it does not know.

There are four stacked pull requests, #53 to #56. On #55 and #56 the
`desktop` job fails, as R12 says, only in the desktop contract tests;
everything else is green. Next: desktop instruction D0004, whose first branch
makes those tests pass.

## Branches and pull requests

| Branch                      | Based on                  | Last commit | Pull request | Pushed | Changed lines (against its base)                     |
| --------------------------- | ------------------------- | ----------- | ------------ | ------ | ---------------------------------------------------- |
| `chore/one-implementer`     | `main`                    | `3b097e4`   | #53          | yes    | +47 / −13                                            |
| `feat/recorder-scoping`     | `chore/one-implementer`   | `e852505`   | #54          | yes    | +423 / −44                                           |
| `feat/protocol-recording`   | `feat/recorder-scoping`   | `66297ca`   | #55          | yes    | about +1,050 / −80, plus 56 regenerated schema files |
| `feat/protocol-new-project` | `feat/protocol-recording` | this report | #56          | yes    | +558 / −42 before this report, plus 58 schema files  |

### CI (verify, integration, desktop; each on Linux, Windows, macOS)

| Pull request | `verify` | `integration`       | `desktop`                                               |
| ------------ | -------- | ------------------- | ------------------------------------------------------- |
| #53          | pass     | pass, twice (in PR) | pass                                                    |
| #54          | pass     | pass, twice (in PR) | pass                                                    |
| #55          | pass     | pass, twice (in PR) | fails only in the R12 tests below, on all three systems |
| #56          | pass     | pass, twice (in PR) | fails only in the R12 tests below, on all three systems |

The desktop tests that fail on #55 and #56 (R12) are all in
`Desktop.Protocol.Tests`, which compares the C# types with the schema files
and the TypeScript constants:

- `ContractTests.Every_schema_file_has_a_CSharp_type_and_every_type_a_schema_file`
  (the new messages have no C# type yet);
- `ContractTests.CSharp_type_has_the_schemas_properties_with_matching_optionality`
  (12 cases on #55, 16 on #56: the new fields);
- `ContractTests.Example_reads_into_its_CSharp_type_and_writes_back_valid`
  (9 and 11 cases: the new example fixtures);
- `ContractTests.Schema_title_names_this_clients_protocol_version` (56 and 58
  cases: every title now says `0.1.1`);
- `VersionAndErrorTests.Current_version_equals_the_TypeScript_PROTOCOL_VERSION`;
- `VersionAndErrorTests.Error_table_equals_the_TypeScript_ERROR_CODES`.

No other desktop test fails; the app's own tests pass.

## Tasks

| Task | State | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| ---- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done  | `main` pulled; the five merged local branches deleted.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 2    | done  | `CLAUDE.md` ("Repository layout", "How to work"), `README.md` and `CONTRIBUTING.md` state owner decision 2.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 3    | done  | **Scoping within a container**, used when nothing but CSS identifies the element. The nearest list item, table row, form, dialog, section, fieldset or article is the container. Its candidates are checked first: role with its label as part of the name, role with full name, label as text, full text, test ID. Then the element's non-CSS candidates are checked inside it. Samples: the products table's second Delete gives `products.officeChairDelete` within `products.officeChairRow` (`role: row, name: Office chair, exact: false`); a list's second Delete is scoped within `details.officeItem`. `docs/recording.md` has a section on it. |
| 4    | done  | A `background` notice for a click on the page background; `pnpm record` names an unknown argument, an option without a value, or missing options.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| 5    | done  | Built as the proposal was written: three requests, six events, Zod schemas, generated JSON Schema files, example fixtures, masking rules. The proposal moved into `docs/protocol.md`; `docs/recording.md` links to it.                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 6    | done  | `PROTOCOL_VERSION` is `0.1.1`; ADR 0011 records R11.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 7    | done  | `RecordingManager` beside `RunManager` (details below).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 8    | done  | `recording.protocol.integration.test.ts`: a whole session, a failed verify, the refusals, the client going away, and a password in no message; `pnpm record` keeps working (its unit tests and the recorder tests).                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| 9    | done  | `createProject`; documented in `docs/protocol.md` and `docs/step-format.md` ("A new project").                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| 10   | done  | `capabilities.installCommand`, present only while `browsers` is empty.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 11   | done  | `stepSkipped` gets `section`, `action`, `title`, `location` whenever no `stepStarted` was sent: after a failure or a cancellation, in tests and in flows, and for `after` steps whose variable was never set.                                                                                                                                                                                                                                                                                                                                                                                                                                            |

### Recording over the protocol: the rules as built

- **Order of messages.** The answer to `startRecording` comes first. The
  recording's events are held back until it is sent, then sent in order.
- **One at a time.**
  - A second `startRecording` is refused with `RecordingInProgress`, and so
    are `startRun`, `openProject`, `createProject` and `verifyRecording` of
    the recording in progress.
  - `startRecording` and `createProject` during a run are refused with
    `RunInProgress`.
- **The browser is always visible.** Two environment variables for tests
  only (named from `PRODUCT.envPrefix`) run it headless and open its remote
  debugging port, so a test can drive it from its own process.
- **Ending.** `shutdown`, and the client closing stdin, stop the recording
  and close its browser; the file keeps what was recorded. If the engine
  dies, Playwright's own exit handling closes the browser it launched.
- **`stepRecorded.review`** carries the same reason as the `# review:`
  comment in the file.
- **Masking.** Every message is masked like any other. `targets`, `review`,
  `url` and `startUrl` are masked as free text; `step` is walked, so its
  `params` are masked as user data.
- **Verify** uses the shared run manager, so its events are the usual run
  events; `recordingVerified` follows `runFinished`.

## One whole recording session as a client sees it

From the protocol test. The test added a to-do, typed "Walk", clicked the
page background, came back and typed " the dog", then clicked Add.
Afterwards it stopped and verified the recording.

```json
{"jsonrpc":"2.0","id":2000,"result":{"recordingId":"rec-20261010-161308-243-6245","file":"tests/recorded/p1.test.yaml"}}
{"jsonrpc":"2.0","method":"recordingStarted","params":{"recordingId":"rec-20261010-161308-243-6245","file":"tests/recorded/p1.test.yaml","startUrl":"/todos"}}
{"jsonrpc":"2.0","method":"stepRecorded","params":{"recordingId":"rec-20261010-161308-243-6245","index":0,"step":{"action":"goto","params":{"url":"/todos"}},"targets":{}}}
{"jsonrpc":"2.0","method":"stepRecorded","params":{"recordingId":"rec-20261010-161308-243-6245","index":1,"step":{"action":"fill","params":{"target":"todos.newTodo","value":"Buy milk"}},"targets":{"todos.newTodo":[{"role":"textbox","name":"New to-do"},{"label":"New to-do"},{"css":"#new-todo"}]}}}
{"jsonrpc":"2.0","method":"stepRecorded","params":{"recordingId":"rec-20261010-161308-243-6245","index":2,"step":{"action":"press","params":{"target":"todos.newTodo","key":"Enter"}},"targets":{}}}
{"jsonrpc":"2.0","method":"stepRecorded","params":{"recordingId":"rec-20261010-161308-243-6245","index":3,"step":{"action":"fill","params":{"target":"todos.newTodo","value":"Walk"}},"targets":{}}}
{"jsonrpc":"2.0","method":"recordingNotice","params":{"recordingId":"rec-20261010-161308-243-6245","kind":"background","message":"A click on the page background (on no element) is not recorded.","page":"main","url":"http://127.0.0.1:58331/todos"}}
{"jsonrpc":"2.0","method":"stepChanged","params":{"recordingId":"rec-20261010-161308-243-6245","index":3,"step":{"action":"fill","params":{"target":"todos.newTodo","value":"Walk the dog"}}}}
{"jsonrpc":"2.0","method":"stepRecorded","params":{"recordingId":"rec-20261010-161308-243-6245","index":4,"step":{"action":"click","params":{"target":"todos.add2"}},"targets":{"todos.add2":[{"role":"button","name":"Add"},{"text":"Add"},{"css":"#add-form > button"}]}}}
{"jsonrpc":"2.0","method":"recordingStopped","params":{"recordingId":"rec-20261010-161308-243-6245","file":"tests/recorded/p1.test.yaml","reason":"stopped","steps":5}}
{"jsonrpc":"2.0","id":2001,"result":{"file":"tests/recorded/p1.test.yaml","steps":5}}
{"jsonrpc":"2.0","id":2002,"result":{"runId":"20261010-161309-351-d94d","resultsDir":"C:/Users/Harsh/AppData/Local/Temp/cfe-demo-w9pNNA/.cfe/runs/20261010-161309-351-d94d"}}
{"jsonrpc":"2.0","method":"runStarted","params":{"runId":"20261010-161309-351-d94d","seq":1,"env":"local","browser":"chromium","settings":{"viewport":{"width":1280,"height":720},"locale":"en-US","timezone":"UTC"},"startedAt":"2026-10-10T16:13:09.353Z","tests":[{"testId":"tests/recorded/p1.test.yaml#0","file":"tests/recorded/p1.test.yaml","name":"P1"}]}}
{"jsonrpc":"2.0","method":"testStarted","params":{"runId":"20261010-161309-351-d94d","seq":2,"testId":"tests/recorded/p1.test.yaml#0","startedAt":"2026-10-10T16:13:09.442Z"}}
{"jsonrpc":"2.0","method":"stepStarted","params":{"runId":"20261010-161309-351-d94d","seq":3,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.0","section":"steps","action":"goto","params":{"url":"/todos"},"page":"main","title":"goto /todos","location":{"file":"tests/recorded/p1.test.yaml","line":15,"column":5}}}
{"jsonrpc":"2.0","method":"stepPassed","params":{"runId":"20261010-161309-351-d94d","seq":4,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.0","durationMs":110,"locators":[],"snapshot":{"state":"skipped"}}}
{"jsonrpc":"2.0","method":"stepStarted","params":{"runId":"20261010-161309-351-d94d","seq":5,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.1","section":"steps","action":"fill","params":{"target":"todos.newTodo","value":"Buy milk"},"page":"main","title":"fill todos.newTodo","location":{"file":"tests/recorded/p1.test.yaml","line":16,"column":5}}}
{"jsonrpc":"2.0","method":"stepPassed","params":{"runId":"20261010-161309-351-d94d","seq":6,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.1","durationMs":44,"locators":[{"param":"target","target":"todos.newTodo","candidateIndex":0,"candidate":{"role":"textbox","name":"New to-do"}}],"snapshot":{"state":"skipped"}}}
{"jsonrpc":"2.0","method":"stepStarted","params":{"runId":"20261010-161309-351-d94d","seq":7,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.2","section":"steps","action":"press","params":{"key":"Enter","target":"todos.newTodo"},"page":"main","title":"press todos.newTodo","location":{"file":"tests/recorded/p1.test.yaml","line":17,"column":5}}}
{"jsonrpc":"2.0","method":"stepPassed","params":{"runId":"20261010-161309-351-d94d","seq":8,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.2","durationMs":12,"locators":[{"param":"target","target":"todos.newTodo","candidateIndex":0,"candidate":{"role":"textbox","name":"New to-do"}}],"snapshot":{"state":"skipped"}}}
{"jsonrpc":"2.0","method":"stepStarted","params":{"runId":"20261010-161309-351-d94d","seq":9,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.3","section":"steps","action":"fill","params":{"target":"todos.newTodo","value":"Walk the dog"},"page":"main","title":"fill todos.newTodo","location":{"file":"tests/recorded/p1.test.yaml","line":18,"column":5}}}
{"jsonrpc":"2.0","method":"stepPassed","params":{"runId":"20261010-161309-351-d94d","seq":10,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.3","durationMs":11,"locators":[{"param":"target","target":"todos.newTodo","candidateIndex":0,"candidate":{"role":"textbox","name":"New to-do"}}],"snapshot":{"state":"skipped"}}}
{"jsonrpc":"2.0","method":"stepStarted","params":{"runId":"20261010-161309-351-d94d","seq":11,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.4","section":"steps","action":"click","params":{"target":"todos.add2"},"page":"main","title":"click todos.add2","location":{"file":"tests/recorded/p1.test.yaml","line":19,"column":5}}}
{"jsonrpc":"2.0","method":"stepPassed","params":{"runId":"20261010-161309-351-d94d","seq":12,"testId":"tests/recorded/p1.test.yaml#0","stepId":"steps.4","durationMs":45,"locators":[{"param":"target","target":"todos.add2","candidateIndex":0,"candidate":{"role":"button","name":"Add"}}],"snapshot":{"state":"skipped"}}}
{"jsonrpc":"2.0","method":"testFinished","params":{"runId":"20261010-161309-351-d94d","seq":13,"testId":"tests/recorded/p1.test.yaml#0","status":"passed","durationMs":257}}
{"jsonrpc":"2.0","method":"runFinished","params":{"runId":"20261010-161309-351-d94d","seq":14,"status":"passed","durationMs":382,"totals":{"passed":1,"failed":0,"cancelled":0,"skipped":0}}}
{"jsonrpc":"2.0","method":"recordingVerified","params":{"recordingId":"rec-20261010-161308-243-6245","runId":"20261010-161309-351-d94d","status":"passed"}}
```

## What `createProject` writes, file by file

For `{ "root": "…/shop-tests", "name": "Shop tests", "baseUrl": "http://localhost:5173" }`:

| File or folder                             | Contents                                                                                                                    |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------- |
| `cfe.config.yaml`                          | as below                                                                                                                    |
| `.gitignore`                               | `# Coffee's data folder: runs, saved logins and caches.`, `.cfe/`, `# Secrets for this machine; never commit them.`, `.env` |
| `tests/`, `flows/`, `targets/`, `actions/` | empty folders                                                                                                               |

```yaml
# Shop tests: the Coffee project configuration.
version: 1
tests: ['tests/**/*.test.yaml']
flows: ['flows/**/*.flow.yaml']
targets: ['targets/**/*.targets.yaml']
actions: ['actions/**/*.ts']

defaults:
  environment: local

environments:
  local:
    baseUrl: http://localhost:5173
```

The project opens with no diagnostics (a unit test and a protocol test check
this). A folder that is not empty is refused with `FolderNotEmpty`. The
message names up to five entries, folders with a trailing `/`, and how many
more there are.

## Departures from the instruction

- None.

## Decisions I made

Places where the documents were silent, and what I chose.

1. **New error names.** The proposal named `RecordingInProgress` and
   `FileExists` only. I added:
   - `RecordingNotFound` (-32012), for `stopRecording` and `verifyRecording`
     with an unknown id, like `RunNotFound`;
   - `FolderNotEmpty` (-32014), for `createProject`.
     Codes `-32011` to `-32014`.
2. **`startRecording` uses `environment`, `startRun` uses `env`.** The
   proposal said `environment`, and the instruction said to build it as
   written. The two requests now name the same thing differently; see
   Questions.
3. **No `seq` on recording events.** The proposal gave recording events no
   `seq`, so they have none. They come in order over one connection.
4. **`stopRecording` on a recording that already ended** (the person closed
   the browser) answers `{ file, steps }` without error.
5. **`verifyRecording`** accepts any recording of this engine session that
   has ended. Ids are not kept across engine restarts.
6. **The project's name** is written as the config's first comment line. The
   config schema has no `name` key, and adding one would change the step-file
   format, which this instruction does not ask for. `openProject` does not
   return the name; see Questions.
7. **`createProject` with a base URL like `localhost:5173`** (no scheme) is
   accepted, because the config's own URL check accepts it. Environment names
   are checked with the config's rule but with a plain message, since the
   schema's own message was "Invalid key in record".
8. **Scoping (task 3) is tried only when nothing but CSS identifies the
   element**, as the finding says. A target that has a good candidate stays
   unscoped. A scoped target has no CSS candidate, because a selector from
   the top of the page cannot match inside the container.
9. **R12's failing tests** include
   `CSharp_type_has_the_schemas_properties_with_matching_optionality`, which
   my first pull request descriptions did not name. They are corrected.

## Questions for the owner

1. **Should `startRecording` say `env`, like `startRun`, instead of
   `environment`?**
   - `env` (recommended): one name for one thing. It is a rename of a
     parameter nobody uses yet, so it is still cheap, but it changes the
     protocol again.
   - Leave it as the proposal wrote it.
2. **Should the config get a `name` key?** That way `openProject` and
   `createProject` could return the project's name for the desktop app to
   show.
   - Yes, as an optional key (recommended): a small, compatible addition to
     the step format and the protocol.
   - No: the app shows the folder name.

## Not done, not pushed, not verified

- **The desktop contract tests** fail on #55 and #56 until D0004's first
  branch adds the C# types (R12). Nothing under `apps/desktop/` changed here.

## Suggestions

- None beyond the questions.
