# Request R0002: tell the engine track about the desktop track

- Date: 2026-10-09
- Written by: desktop implementer
- To: reviewer (changes to the root `CLAUDE.md`, `README.md` and
  `handoff/README.md`)
- State: accepted
- Raised in: [report D0001](../reports/D0001-desktop-foundation.md)

## What is needed

The root documents that the engine implementer reads at the start of every
session should say that `apps/desktop/` now belongs to a second implementer
with its own handoff folder.

## Why

Today the root `CLAUDE.md` says `apps/desktop` is "README only for now", and
the root handoff process knows one implementer. Without a note, the engine
implementer could edit `apps/desktop/` (for example during a repository-wide
rename or format run), or take a desktop instruction for its own. The desktop
implementer may not edit those files.

## Proposal

1. Root `CLAUDE.md`, "Repository layout": replace the `apps/desktop` line with

   ```
   apps/desktop        Avalonia app; its own implementer and handoff folder
                       (apps/desktop/handoff/)
   ```

   Under "How to work", add: "Do not change files under `apps/desktop/`; that
   folder has its own implementer. Ask through `apps/desktop/handoff/`
   instead."

2. Root `handoff/README.md`, "Who does what": add the desktop implementer and
   a link to [apps/desktop/handoff/README.md](../README.md).
3. Root `README.md`, layout table: `apps/desktop` → "Avalonia desktop app (in
   progress, see apps/desktop/README.md)".

## Until then

Nothing in the desktop app depends on this; it only prevents accidents.

## Answer

Accepted (reviewer, 2026-10-09). The root `handoff/README.md` describes both
tracks since review D0001. The changes to the root `CLAUDE.md` and `README.md`
are task 7 of the engine track's instruction 0006; this request is done when
that pull request is merged.
