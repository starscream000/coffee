# Report 0008: the demo .env and the built-in actions

- Date: 2026-10-10
- Written by: implementer
- Instruction: [0008](../instructions/0008-demo-env-and-the-built-in-actions.md)

## Summary

The demo project has its demo-only `.env` and opens with no diagnostics and no
environment variable set. Plan branches 11, 12 and 13 are done: every built-in
action of `docs/actions.md` runs, and none answers `NotImplemented` any more. A
unit test now guards that. Plan branch 13 was over the ~1,500-line limit, so it
is split into two stacked branches. Five pull requests are open (#30, #43, #44,
#45, #46); all of them are green on Linux, Windows and macOS for all three jobs.
Next: the owner's review and the merges, in stack order.

## Branches and pull requests

Each branch sits on the one above it; every pull request targets `main` and
names the branch it sits on.

| Branch                         | Based on                       | Last commit | Pull request | Pushed | Changed lines (against its base)                             |
| ------------------------------ | ------------------------------ | ----------- | ------------ | ------ | ------------------------------------------------------------ |
| `chore/demo-env`               | `main`                         | `a70719f`   | #30          | yes    | +37 / −3                                                     |
| `feat/actions-interaction`     | `chore/demo-env`               | `a00af62`   | #43          | yes    | +624 / −180 (the −180 is mostly moving built-ins into files) |
| `feat/actions-wait-expect`     | `feat/actions-interaction`     | `aafa3b5`   | #44          | yes    | +817 / −13                                                   |
| `feat/actions-data-http-flows` | `feat/actions-wait-expect`     | `caf7362`   | #45          | yes    | +1,082 / −12                                                 |
| `feat/actions-flows`           | `feat/actions-data-http-flows` | this report | #46          | yes    | +1,136 / −126, about 430 of them the I2 expectation (JSON)   |

### CI (verify, integration, desktop; each on Linux, Windows, macOS)

| Pull request | First run       | Second run      |
| ------------ | --------------- | --------------- |
| #30          | all 9 jobs pass | all 9 jobs pass |
| #43          | all 9 jobs pass | all 9 jobs pass |
| #44          | all 9 jobs pass | all 9 jobs pass |
| #45          | all 9 jobs pass | all 9 jobs pass |
| #46          | all 9 jobs pass | see below       |

#46's second run is the run on the commit that adds this report (no code
change); its result is in the pull request.

The `desktop` job passed on every run. No `desktop` test failed.

## Tasks

| Task | State | Notes                                                                                                                                                                                                                                                                                                                                                                 |
| ---- | ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done  | `main` pulled; no local branch contained in `main` was left.                                                                                                                                                                                                                                                                                                          |
| 2    | done  | `examples/demo-app/.env` with `DEMO_PASSWORD=demo-only-not-a-secret` and a 3-line header; `.gitignore` allows only this file; README, CONTRIBUTING and CHANGELOG say so. Test: "the demo project opens without setup" (`fixtures.protocol.test.ts`). The desktop helper `DemoEnvironment.Ensure()` sets `DEMO_PASSWORD` only when it is missing, so it keeps working. |
| 3    | done  | `select`, `check` (and `checked: false`), `hover`, `press` (with no target: the keyboard), `upload` (paths relative to the step file, `[]` clears), `drag`.                                                                                                                                                                                                           |
| 4    | done  | `/form` and `/interactions`, documented in the demo README.                                                                                                                                                                                                                                                                                                           |
| 5    | done  | S2 on branch 11 without its `expect.value` steps; branch 12 adds the three `expect.value` steps to `tests/forms.test.yaml`.                                                                                                                                                                                                                                           |
| 6    | done  | Every wait polls every 100 ms and checks the signal on each round; a failing wait is `ActionTimeout` with a message that says what did not happen.                                                                                                                                                                                                                    |
| 7    | done  | `regex:` prefix, otherwise a glob (`*`, `**`, `{a,b}`), relative to `baseUrl` when it starts with `/`. Unit tests in `url-pattern.test.ts`.                                                                                                                                                                                                                           |
| 8    | done  | `countCandidates` counts matches without the exactly-one rule for `expect.visible: false`, `expect.count` (first candidate with at least one match) and `wait.element` `hidden`/`detached`.                                                                                                                                                                           |
| 9    | done  | S1 `tests/navigation.test.yaml`, S3 `tests/interactions.test.yaml`, S16 `tests/within.test.yaml` (`/products`), I3 and I4 in `wait-expect.integration.test.ts`.                                                                                                                                                                                                       |
| 10   | done  | `set`, `extract` (text with whitespace collapsed, value or attribute; `pattern` with one group).                                                                                                                                                                                                                                                                      |
| 11   | done  | `api` with `ctx.request`; `mock` with `context.route`.                                                                                                                                                                                                                                                                                                                |
| 12   | done  | `ResponseLog` per test, fed by every browser context of the test; `wait.response` and `expect.response` read it from the start of the previous step on.                                                                                                                                                                                                               |
| 13   | done  | On the stacked branch `feat/actions-flows`: `StepRunner` runs `call` with nested `stepId`s and `parentStepId`, parameters, `outputs`, flows calling flows.                                                                                                                                                                                                            |
| 14   | done  | `Cookie`, `Set-Cookie` (each cookie's value) and `Authorization` of a response a step stores or reads are registered as secrets; a value under 4 characters gives a `warn` naming the header.                                                                                                                                                                         |
| 15   | done  | S4, S9, S19 in `http.integration.test.ts` (#45); S5 and I2 in `flows.integration.test.ts` (#46). The A9 parts of S9 and S19 are checked through the events and the data folder (`.cfe/runs`, `.cfe/logins`).                                                                                                                                                          |

### The sample for each action

| Action                     | Sample                                                                          |
| -------------------------- | ------------------------------------------------------------------------------- |
| `goto`                     | S1 (and nearly every other)                                                     |
| `back`, `reload`           | S1                                                                              |
| `click`, `fill`, `select`  | S2                                                                              |
| `check`, `press`, `upload` | S2 (`check` with `checked: '${params.done}'` also in S5)                        |
| `hover`, `drag`            | S3                                                                              |
| `wait.element`, `wait.url` | S1, S3; I4 cancels a 30-second `wait.element`                                   |
| `wait.response`            | S4, S19                                                                         |
| `expect.visible`           | S3 (and `visible: false`)                                                       |
| `expect.text`              | S2, S4, S5, S9, S16                                                             |
| `expect.value`             | S2                                                                              |
| `expect.url`               | S1                                                                              |
| `expect.count`             | S4, S16                                                                         |
| `expect.response`          | S4                                                                              |
| `set`, `extract`           | S4 (`extract` with `pattern`); `extract` also in S5                             |
| `api`                      | S4 (`POST` with `json`, `as`, `status`), S9 (secret header), S19 (token header) |
| `mock`                     | S4 (`json`, `times: 1`); a mocked header in the short-token test                |
| `call`                     | S5 (a flow calling a flow)                                                      |

## Checks

Run on `feat/actions-flows`, the top of the stack, on Windows; each lower branch
passed the same commands before it was pushed.

| Command                 | Result                    |
| ----------------------- | ------------------------- |
| `pnpm verify`           | pass: 41 files, 400 tests |
| `pnpm test:integration` | pass: 13 files, 86 tests  |

No lint or type suppression was added in this instruction.

## Departures from the instruction

- Plan branch 13 is split in two stacked branches:
  `feat/actions-data-http-flows` (tasks 10, 11, 12, 14; S4, S9, S19) and
  `feat/actions-flows` (task 13; S5, I2). Together they were over 2,200
  changed lines. So this report is on `feat/actions-flows`, the last branch,
  not on `feat/actions-data-http-flows`.

## Decisions I made

Places where `docs/` was unclear, wrong or silent, and what I chose.

**Things `docs/actions.md` promises that Playwright cannot do as described**

1. **Bodies the page never reads.** Chromium never finishes loading the body
   of a `fetch` whose body the page does not read. Playwright's
   `response.text()` and `response.finished()` then never resolve. So
   `wait.response … as` cannot always store `{ status, headers, json, text }`.
   What I built:
   - Body reads are bounded by the step's time.
   - `wait.response … as` then stores the response with an empty `text` and a
     `null` `json`, and logs a `warn`.
   - `expect.response` with `json` or `contains` fails, and says why.
   - A body is read only when a step needs it.
   - The demo `/token` page reads its body, as real pages do.
   - This is documented in `docs/actions.md`.
2. **`select` with an option that does not exist** waits for the option until
   the step times out. Playwright waits for options to appear. So the step
   fails with `ActionTimeout`, not with an immediate "no such option" error.

**Silences filled**

3. **The `Authorization` header** is masked both as its whole value and as the
   credential after its scheme (`Bearer …`). ADR 0014 says only "its value".
   Masking only the whole value would leave the bare token visible once a
   test sends it on.
4. **Header names in a stored response** are in lower case
   (`${vars.issued.headers.authorization}`), as Playwright gives them.
   Documented.
5. **`api … as` and the status check.** The response is stored before its
   status is checked, so it is still there after a failure.
6. **`mock`.**
   - `file` is relative to the step file.
   - A request that `mock` does not answer (another method, or past `times`)
     goes on to the next handler or the network.
   - `times` counts per `mock` step.
7. **The response window.** `wait.response` and `expect.response` look from
   the start of the previous step on. So one response can satisfy two steps in
   a row. `expect.response` passes when any response in the window fits; its
   failure shows the last one it examined.
8. **Flows: the failure code.** A failing step fails its `call` with the code
   `FlowFailed`. The docs name no code for this. The message names the failing
   step and its place in the flow file (`… failed at steps.1/steps.0
(flows/add-todo.flow.yaml:14:5): …`). The failing step's own `stepFailed`
   keeps its precise error.
9. **Flows: nesting and timing.**
   - The steps of a flow get the call's `section` in `stepStarted`.
   - A `call` has no timeout of its own, like the wait for a saved login.
   - After a cancellation, the steps of a flow called from `after` share the
     30-second limit.
10. **Flows: outputs.**
    - Outputs a flow set are copied back even when the flow fails, so `after`
      steps can use them.
    - An output that a passing flow never set gives a `FlowOutputNotSet`
      warning.
11. **Flows: checks repeated at run time.** `FlowCycle`, `FlowNotFound`,
    `MissingParameter`, `InvalidParameterType` and `UnknownParameter` are
    checked again after interpolation, with the same codes as validation.
12. **Login flows can call flows.** The login cache key already hashed called
    flows, so this matches what the code assumed.
13. **The `NotImplemented` check stays in `runner/step.ts`.** It guards a
    registry that would hold a spec without a `run`; built-ins can no longer
    reach it. The integration case for it is removed, and a unit test checks
    that every built-in has a `run`.

**Earlier parts**

14. **Text checks** (`expect.text`, `extract`) collapse whitespace.
15. **`expect.url` with `equals: /path`** resolves against `baseUrl`.
16. **Assertion polling.** Assertions keep a 150 ms margin before the step's
    deadline, so they fail with `AssertionFailed` rather than
    `ActionTimeout`.

## Questions for the owner

1. **Should a failing flow keep `FlowFailed`, or pass the failing step's own
   code up to the `call` step?**
   - `FlowFailed` (recommended, built): the call says where the failure is, and
     the step inside keeps its own code.
   - The inner code: clients would show "AssertionFailed" on the call, but the
     message and location would then point at two places.
2. **Should a body the page never reads stay a warning in `wait.response … as`?**
   - A warning (recommended, built): a test that only needs headers still
     passes.
   - A failure: stricter, but a test reading a token from a header would fail
     on such pages.

## Not done, not pushed, not verified

- The A9 checks themselves go through the command line, which comes with plan
  branch 15. Their S9 and S19 parts are checked here through the events and
  the data folder.
- No integration test runs the unread-body path of `wait.response` against a
  real page. It needs a page that does not read its body; the demo pages all
  read theirs.

## Suggestions

- The demo server's `GET /api/token/last` exists only so a test can find the
  token in the output. If the A9 command-line check comes with plan branch 15,
  it can use the same endpoint.
