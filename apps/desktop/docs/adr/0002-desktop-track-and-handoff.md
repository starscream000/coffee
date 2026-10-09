# D0002. Run the desktop app as its own track inside `apps/desktop`

- Status: Accepted (owner, 2026-10-09, in chat)
- Date: 2026-10-09

## Context

The engine is built by one implementer through the root `handoff/` folder. The
owner asked a second implementer to build the desktop app in parallel, with
the same method, and confined it to `apps/desktop/`.

## Decision

- The desktop implementer changes files only under `apps/desktop/`.
- The desktop track has its own handoff folder,
  [apps/desktop/handoff/](../../handoff/README.md), with instructions,
  reports and reviews numbered `DNNNN`, and requests (`RNNNN`) for anything
  needed outside the folder.
- Branches are named `desktop/<type>/<name>`. They are merged into `main` by
  the reviewer on the owner's word, as merge commits, like every other branch.
- The desktop app keeps its own changelog, plan and decision records under
  `apps/desktop/`.

## Alternatives rejected

- **One shared handoff folder**: the two implementers would read each other's
  instructions and could take the wrong one; the root folder is outside the
  desktop implementer's reach anyway.
- **A separate repository**: the desktop's contract tests read the protocol's
  JSON Schemas and start the engine from the same checkout; a second
  repository would need a published protocol package first.

## Consequences

Changes the desktop needs outside its folder (CI, root documents, protocol or
engine changes) are slower: they go through a request, the reviewer and the
engine track. The root documents must tell the engine implementer that the
folder is not theirs ([request R0002](../../handoff/requests/R0002-tell-the-engine-track-about-the-desktop-track.md)).

## Revisit when

The desktop app and the engine are released separately, or one implementer
takes over both.
