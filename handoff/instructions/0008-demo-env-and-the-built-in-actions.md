# Instruction 0008: the demo `.env` and the built-in actions

- Date: 2026-10-10
- Written by: reviewer
- Based on `main` at: `786191d` or later
- Replaces: none
- Follows review: [0007](../reviews/0007-a-build-then-fixes-and-pages-and-logins.md)

## Goal

The demo project opens without errors for anyone, and plan branches 11, 12 and
13 exist: every built-in action of `docs/actions.md` runs. After this
instruction a tester can write a real test with the built-in actions alone.

## Owner decisions

1. Pull requests #25, #27, #28 and #29 are merged. Plan branches 1 to 10 are
   on `main`.
2. **The demo project gets a committed, demo-only `.env`** (2026-10-10). It is
   the one exception to "never commit `.env` files".
3. **The desktop track is working again**, on a large instruction of its own
   (D0003): it now starts runs and shows them. Nothing under `apps/desktop/`
   is yours to change.
4. **Not now:** the change to `ErrorInfo.candidates` proposed in report 0006.

## The demo project is shared

The desktop app's tests open and run `examples/demo-app` through the real
engine, and the `desktop` CI job runs on your pull requests. Until the desktop
track says otherwise:

- do not change the list of environments in the demo config (it must stay
  `local` only; put other environments in a test's own copy, as S17 does);
- do not rename or remove the existing samples, fixtures, targets or the
  action `demo.addTodo`; add new ones beside them.

If the `desktop` job fails on one of your pull requests, change nothing under
`apps/desktop/`. Say in the report which test failed and why, and go on with
what does not depend on it.

## Branches

| Branch                         | Based on                   | Plan branch | What it holds          |
| ------------------------------ | -------------------------- | ----------- | ---------------------- |
| `chore/demo-env`               | `main`                     | none        | Task 2                 |
| `feat/actions-interaction`     | `chore/demo-env`           | 11          | Tasks 3 to 5           |
| `feat/actions-wait-expect`     | `feat/actions-interaction` | 12          | Tasks 6 to 9           |
| `feat/actions-data-http-flows` | `feat/actions-wait-expect` | 13          | Tasks 10 to 15, report |

Open one pull request per branch into `main` and say in each description which
branch it sits on. Push `chore/demo-env` and open its pull request as soon as
it is green, before starting the next branch: the owner is waiting for it.

If a `feat/` branch passes roughly 1,500 changed lines (not counting fixtures,
generated files and the lockfile), split it in two stacked branches and say so
in the report. If a plan branch cannot be finished, stop there and report; do
not start the next one.

`docs/` is the specification. Where this instruction and a document under
`docs/` disagree, stop and say so in the report.

## Tasks

### Part A: housekeeping (no files change)

1. `git switch main` and `git pull --ff-only`. Delete the local branches that
   are now contained in `main`.

### Part B: the demo `.env` (`chore/demo-env`)

2. Commit `examples/demo-app/.env` with `DEMO_PASSWORD` set to a value that is
   plainly a demo value. The file's first lines say that it is for the demo
   only, that it protects nothing, and that a real project keeps its `.env`
   out of Git. Allow exactly this one file in `.gitignore`; every other `.env`
   stays ignored. Say the same in `examples/demo-app/README.md` and, in one
   sentence, in `CONTRIBUTING.md`. Opening the demo project with no
   environment variable set must give no diagnostics; add a test for that.
   Tests that set `DEMO_PASSWORD` themselves keep working.

### Part C: interaction actions (`feat/actions-interaction`, plan branch 11)

The specification for Parts C to E is `docs/actions.md` ("Built-in actions"
and the sections it refers to), `docs/step-format.md` and `docs/protocol.md`.
Every action goes through `defineAction` like a user action, honours
`ctx.signal`, and fails with errors a tester can read.

3. `select`, `check` (and uncheck), `hover`, `press`, `upload`, `drag`.
4. The demo app gets the pages these need, documented in its README.
5. Sample `tests/forms.test.yaml` with its integration test: check **S2**. The
   parts of S2 that need `expect.value` wait for branch 12; say in the report
   how you split it.

### Part D: waits and checks (`feat/actions-wait-expect`, plan branch 12)

6. `back`, `reload`, `wait.element`, `wait.url`, `expect.visible`,
   `expect.value`, `expect.url`, `expect.count`. Every wait inside the engine
   checks the signal on each poll.
7. URL patterns at run time follow the owner's rule: `regex:` marks a regular
   expression, anything else is a glob.
8. `expect.visible: false` and `expect.count` are the documented exceptions to
   "exactly one element" (ADR 0010); build them as the ADR says.
9. Samples and tests: checks **S1**, **S3**, **S16**, **I3** and **I4**, and
   the rest of S2.

### Part E: data, HTTP and flows (`feat/actions-data-http-flows`, plan branch 13)

10. `set` and `extract`.
11. `api`, with the context's request object, and `mock`.
12. The response log behind `wait.response` and `expect.response`, so that
    "click, then wait for the response" cannot miss a response that arrived
    during the click.
13. `call`: a flow with parameters and `outputs`, a flow calling a flow, and
    nested `stepId`s in the events.
14. Sensitive response headers (`Cookie`, `Set-Cookie`, `Authorization`) are
    registered as secrets when a step stores or reads them, as ADR 0014 says.
15. Samples and tests: checks **S4**, **S5**, **S9**, **S19** and **I2**. The
    parts of S9 and S19 that A9 checks through the command line wait for plan
    branch 15; check the same thing through the events and the run folder.

## Done when

- [ ] `pnpm verify` passes on every branch, with no browser installed.
- [ ] `pnpm test:integration` passes on every branch, and the `integration` job
      passed twice in a row on each pull request.
- [ ] Every pull request is open, and CI is green on Linux, Windows and macOS
      for each, or the report says which `desktop` test failed and why.
- [ ] The demo project opens with no diagnostics and no environment variable
      set.
- [ ] Checks S2 on `feat/actions-interaction` (as far as it can go there); S1,
      S3, S16, I3, I4 on `feat/actions-wait-expect`; S4, S5, S9, S19, I2 on
      `feat/actions-data-http-flows`.
- [ ] No built-in action of `docs/actions.md` answers `NotImplemented` any
      more. If one must, the report says which and why.
- [ ] No browser process is left behind after any test.
- [ ] Committed JSON Schema files match the generator. `PROTOCOL_VERSION`
      stays `0.1.0`.
- [ ] File header comments and TSDoc on every export, no `any`, and every lint
      or type suppression listed in the report with its reason.
- [ ] No npm dependency is added.
- [ ] No client package imports engine code. Nothing under `apps/desktop/`
      changed.
- [ ] `CHANGELOG.md` is updated in each branch.
- [ ] `handoff/reports/0008-demo-env-and-the-built-in-actions.md` exists on the
      last branch and follows [the template](../templates/report.md).

## Out of scope

- Screenshots, page snapshots and `openSnapshot` (plan branch 14).
- The CLI's `run` command (plan branch 15).
- The recorder.
- The `ErrorInfo.candidates` change (owner decision 4).
- Anything under `apps/desktop/`.
- Raising the Node pin.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- For each branch: its pull request number, the CI result of all three jobs,
  and its changed-line count.
- For each action: the sample that exercises it.
- Every place where a document under `docs/` was unclear, wrong or silent and
  you had to choose. For the actions in particular: anything `docs/actions.md`
  promises that Playwright cannot do as described.
- Anything in this instruction you think is wrong.
