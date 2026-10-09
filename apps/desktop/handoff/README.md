# Desktop handoff

How work on the desktop app is passed between the owner, the reviewer and the
desktop implementer. It follows the repository's
[handoff process](../../../handoff/README.md) in everything not said here; this
page lists only what differs.

## Why a separate folder

The desktop app is built by its own implementer, in parallel with the engine.
The owner confined that implementer to `apps/desktop/` (owner decision,
2026-10-09). So the desktop track keeps its own instructions, reports, reviews
and status here, and the root `handoff/` stays the engine's.

## Who does what

| Party               | Who it is                                   | Writes here                                               |
| ------------------- | ------------------------------------------- | --------------------------------------------------------- |
| Owner               | The person who owns the project             | Decisions. Starts each step with a short word.            |
| Reviewer            | Claude in the owner's chat session          | Instructions, reviews, `STATUS.md`, answers to requests   |
| Desktop implementer | Claude Code agent working on `apps/desktop` | Code and docs under `apps/desktop/`, reports and requests |

The engine implementer does not write in this folder. When the desktop track
needs something from the engine track, the reviewer carries it across (see
[Requests](#requests)).

## The folder

```
apps/desktop/handoff/
  README.md          this file
  STATUS.md          where the desktop track stands (reviewer; see "Bootstrapping")
  instructions/      DNNNN-short-title.md   written by the reviewer
  reports/           DNNNN-short-title.md   written by the desktop implementer
  reviews/           DNNNN-short-title.md   written by the reviewer
  requests/          RNNNN-short-title.md   needs outside apps/desktop, from the implementer
  templates/         the request layout (the others are in the root templates/)
```

Numbers carry a `D` (instructions, reports, reviews) or `R` (requests) so that
they never collide with the engine track's `NNNN` files.

Templates: instructions, reports and reviews use the root
[templates](../../../handoff/templates/); requests use
[templates/request.md](templates/request.md).

## One cycle

The same as the root cycle, with these paths:

1. The reviewer publishes `instructions/DNNNN-…md` and updates `STATUS.md`.
2. The owner tells the desktop implementer: "Pull main and carry out the open
   desktop instruction."
3. The desktop implementer works on the branches the instruction names, writes
   `reports/DNNNN-…md` on the last branch, runs the desktop checks and the root
   `pnpm verify`, pushes, opens the pull requests and stops.
4. The owner tells the reviewer `check desktop`; the reviewer writes
   `reviews/DNNNN-…md`. `merge` and `next` work as in the root process.

## Rules that differ from the root process

1. **The desktop implementer writes only under `apps/desktop/`.** It reads
   anything in the repository, but never changes a file outside this folder,
   not even the root `handoff/`, `CHANGELOG.md`, CI or `.prettierignore`.
2. **Needs outside the folder become requests.** See below.
3. **Branches** are named `desktop/<type>/<name>`, for example
   `desktop/feat/protocol-client`, so they are easy to tell from the engine's.
   Types are those of the root rules: `feat`, `fix`, `docs`, `chore`, `test`.
4. **Changelog**: the desktop app keeps [its own](../CHANGELOG.md), in the same
   format.
5. **Checks**: `apps/desktop/scripts/verify.sh` (or `verify.ps1`) and the root
   `pnpm verify`, which also checks this folder's Markdown and JSON with
   Prettier. Both must pass before a pull request.

## Requests

A request is how the desktop track asks for something it may not do itself:
a change outside `apps/desktop/` (CI, the protocol, the engine), an owner
decision, or the engine track's priorities.

- The desktop implementer writes `requests/RNNNN-short-title.md` from
  [the template](templates/request.md), with state `open`, and lists it in its
  report.
- The reviewer answers in the same file's "Answer" section (the only part of a
  request the reviewer edits), sets the state to `accepted`, `declined` or
  `done`, and, if accepted, carries the work into a root or desktop
  instruction.
- The desktop implementer never assumes a request is granted until its state
  says so.

## Bootstrapping

The desktop track started from the owner's brief in chat (2026-10-09), before
the reviewer had written anything here. To keep the record honest, the desktop
implementer transcribed that brief as
[instruction D0001](instructions/D0001-desktop-foundation.md) and wrote the
first `STATUS.md`. Both say so at the top. From the first review on, only the
reviewer writes instructions and `STATUS.md`.
