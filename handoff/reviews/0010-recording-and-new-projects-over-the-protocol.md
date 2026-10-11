# Review 0010: recording and new projects over the protocol

- Date: 2026-10-11
- Written by: reviewer
- Instruction: [0010](../instructions/0010-recording-and-new-projects-over-the-protocol.md)
- Report: `handoff/reports/0010-recording-and-new-projects-over-the-protocol.md`
  on the branch `feat/protocol-new-project` (it reaches `main` when pull
  request #56 is merged)
- Verdict: **Approved**, all four pull requests (#53 to #56).

## Pull requests

| Pull request | Branch                      | Reviewed at | CI                                                                        | Verdict  |
| ------------ | --------------------------- | ----------- | ------------------------------------------------------------------------- | -------- |
| #53          | `chore/one-implementer`     | `3b097e4`   | all nine jobs green                                                       | Approved |
| #54          | `feat/recorder-scoping`     | `e852505`   | all nine jobs green                                                       | Approved |
| #55          | `feat/protocol-recording`   | `66297ca`   | `verify` and `integration` green; `desktop` fails as decision R12 expects | Approved |
| #56          | `feat/protocol-new-project` | `fce29f6`   | `verify` and `integration` green; `desktop` fails as decision R12 expects | Approved |

Merge in the order of their numbers, and straight after them the desktop
track's pull requests #57 to #66 ([review D0004](../../apps/desktop/handoff/reviews/D0004-recording-new-projects-and-a-clear-layout.md)).
`main` has a failing `desktop` job only between #55 and #57, a few seconds.

## What I checked

- **CI.** On #55 and #56 the `desktop` job fails only in
  `Desktop.Protocol.Tests` (`ContractTests` and, on #56,
  `VersionAndErrorTests.Error_table_equals_the_TypeScript_ERROR_CODES`): the
  C# types do not know the new schema files yet. That is what R12 says. The
  desktop track's first branch (#57) is green on all nine jobs.
- **One session of my own through a real engine process**, on the top of the
  stack, as a client that says it speaks `0.1.0`:
  - `initialize` answers `0.1.1` and accepts the older client;
  - `createProject` refuses a folder that is not empty and names what is in
    it, refuses a bad environment name in plain words, and creates a project
    that opens with no diagnostics. A project name that holds a line break
    cannot add a line to the config file;
  - a recording of the demo login, typed with real key presses: the steps
    arrive as `stepRecorded` in order; the password became
    `${vars.loginPassword}` with the review reason, the same in the event and
    in the file; **the typed password is in no message, no log and no file**;
  - a second `startRecording` and a `startRun` during the recording are
    refused with `RecordingInProgress`;
  - `stopRecording`, then `verifyRecording`: the usual run events, then
    `recordingVerified`;
  - a recording left open while the client goes away: the engine ends with
    code 0, the recording browser is closed, the file is kept.
- **By search over the changes:** no dependency added, no lint or type
  suppression, no `any`, `PROTOCOL_VERSION` is `0.1.1`, nothing under
  `apps/desktop/` changed.
- **Read:** `project/create.ts` in full, and the two test-only switches of
  `recorder/manager.ts`.

## What I did not check

- The rest of the source and the tests; I relied on my session and on CI.
- The scoping with `within` (task 3) by hand. Its test passes in CI on three
  systems.
- A visible recording browser. My session ran it without a window, through
  the test switch.

## Findings

None that block a merge.

1. **Note. "It runs" is not yet "it is right".** My recorded login passed its
   check although the password placeholder is empty and the demo app refused
   the login: every step could be carried out, and the recording holds no
   check that would notice. This is decision R6 as written, and the step is
   marked for review. It is the strongest reason for the recorder's next
   part: recording checks ("this text should be here").
2. **Note.** The four folders `createProject` makes are empty, and Git does
   not keep empty folders. Nothing breaks (the globs then match nothing), so
   no change is asked for.
3. **Note.** The two test-only environment variables (no window, remote
   debugging port) are read by the shipped engine. They are named as test
   switches and the port listens on this machine only. Keep it that way: no
   client may ever set them.

## Rulings on the report

- All nine "Decisions I made" are accepted, the two new error names included.
- **Question 1: `startRecording` gets `env`, like `startRun`** (reviewer
  decision R13). One name for one thing; my proposal caused the difference.
  `createProject` keeps `environment`: there it names the environment to
  create. Nothing of the protocol is released, so the rename is cheap now. It
  goes into the next engine instruction, as a stacked pair by R12.
- **Question 2 (a `name` key in the config)** is the owner's: it adds a key to
  the project file. Recommended: yes, optional.

## Done-when checks

| Check                                                              | Result                                |
| ------------------------------------------------------------------ | ------------------------------------- |
| `pnpm verify` and `integration` on every branch, three systems     | Yes                                   |
| `desktop` green on #53 and #54; fails only as R12 says on the rest | Yes                                   |
| Schema files match the generator; protocol `0.1.1`                 | Yes (CI checks the generator)         |
| A session from start to verify; `createProject` opens cleanly      | Yes (their tests, and my own session) |
| No password reaches any message                                    | Yes (their test, and my own session)  |
| No dependency added; nothing under `apps/desktop/`                 | Yes                                   |
| `CHANGELOG.md` updated; the report follows the template            | Yes                                   |

## Owner decisions needed

- A `name` key in the project's config file, so the app can show the name
  given in "New project"? Recommended: yes.

## For the next instruction

The recorder's next part (checks first, see finding 1; then hover, drag,
upload and reuse of shared targets); `env` in `startRecording` (R13); desktop
request R0008 (the environments' addresses); the config's `name` key if the
owner agrees. Plan branches 14 to 16 after that.
