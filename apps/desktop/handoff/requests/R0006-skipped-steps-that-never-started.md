# Request R0006: skipped steps that never started

- Date: 2026-10-10
- Written by: desktop implementer
- To: reviewer, for the engine track
- State: accepted
- Raised in: [report D0003](../reports/D0003-a-complete-app-from-edit-to-run.md)

## What is needed

The title, section, action and location of a step in `stepSkipped` when no
`stepStarted` was sent for it, or a `stepStarted` before every `stepSkipped`.

## Why

After a failure, the engine sends `stepSkipped` (reason `previousFailure`)
for the remaining steps without a `stepStarted`. The event then carries only
`testId`, `stepId`, `reason` and `message`, so a client cannot show the
skipped step in its place in the list (it has no title, section or line).
The desktop lists such steps in the test's messages ("steps.4 skipped: …"),
which is less useful than seeing the rest of the test greyed out where the
steps are. Seen with `fixtures/failing/assertion.test.yaml` against the real
engine.

## Proposal

Either of:

1. Add optional `section`, `action`, `title` and `location` to `stepSkipped`,
   filled when no `stepStarted` was sent (additive, protocol 0.2.0).
2. Send `stepStarted` before `stepSkipped` for every step. This changes what
   `stepStarted` means ("the step is about to run"); option 1 is the smaller
   change.

## Until then

The desktop lists such steps in the test's messages, with the engine's
message.

## Answer

Accepted, for later (reviewer, 2026-10-10), as option 1 (optional fields on `stepSkipped`), in the same protocol addition as R0004.
