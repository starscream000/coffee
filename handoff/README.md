# Handoff

How work is passed between the three parties on this project, and where the
record of it lives. Everything said between the reviewer and the implementer
goes through files in this folder, so nothing depends on a chat that can be
lost.

## Who does what

| Party       | Who it is                                 | Writes                                         |
| ----------- | ----------------------------------------- | ---------------------------------------------- |
| Owner       | The person who owns the project           | Decisions. Starts each step with a short word. |
| Reviewer    | Claude in the owner's chat session        | Instructions, reviews, `STATUS.md`             |
| Implementer | Claude Code agent on the owner's computer | Code, docs and one report per instruction      |

The owner decides. The reviewer and the implementer never talk to each other
directly: the reviewer writes an instruction, the implementer answers with a
report, the reviewer answers with a review.

## The folder

```
handoff/
  README.md          this file
  STATUS.md          where things stand right now (reviewer only)
  instructions/      NNNN-short-title.md   written by the reviewer
  reports/           NNNN-short-title.md   written by the implementer
  reviews/           NNNN-short-title.md   written by the reviewer
  templates/         the three file layouts
```

An instruction, its report and its review share the same number and file name.
Numbers have four digits and go up by one.

## One cycle

1. **Reviewer publishes instruction NNNN** on `main` and updates `STATUS.md`.
2. **Owner tells the implementer:** "Pull main and carry out the open
   instruction."
3. **Implementer:**
   1. `git switch main`, then `git pull --ff-only`.
   2. Reads `STATUS.md` and opens the instruction it names. If none is open,
      does nothing and says so. Usually one instruction is open. If
      `STATUS.md` lists several, it carries them out in the listed order, each
      with its own report; see [Several open instructions](#several-open-instructions).
   3. Works on the branch or branches the instruction names.
   4. Writes `handoff/reports/NNNN-…md` on that branch (on the last branch if
      there are several).
   5. Runs `pnpm verify`, pushes the branches, opens a pull request into `main`
      for each, and stops. It does not merge.
4. **Owner tells the reviewer:** `check`.
5. **Reviewer** reads the report and the changes, runs what checks it can, looks
   at the CI result, writes `handoff/reviews/NNNN-…md` with a verdict, updates
   `STATUS.md`, and tells the owner what it found.
6. **Owner tells the reviewer** `merge`, `next`, or both.

## The owner's words

To the reviewer:

| Word    | What the reviewer does                                                              |
| ------- | ----------------------------------------------------------------------------------- |
| `check` | Reviews the newest report and its pull requests. Writes the review. Merges nothing. |
| `merge` | Merges the pull requests the last review approved, each as a merge commit.          |
| `next`  | Publishes the next instruction.                                                     |

Words can be combined ("check and next"). They always run in the order check,
merge, next. `merge` is skipped, and said to be skipped, when the review did not
approve.

To the implementer, one sentence is enough: "Pull main and carry out the open
instruction."

## Several open instructions

The owner may ask for more than one instruction to be open at once, so that
the implementer can keep working without a review in between.

- `STATUS.md` lists the open instructions in the order to carry them out.
- Nothing is merged in between, so the branches stack: the first branch of a
  later instruction is based on the last branch of the earlier one. Each
  instruction's "Branches" table says what to base on.
- Each instruction still gets its own report, on its own last branch.
- If an earlier instruction cannot be finished, the implementer stops there,
  reports, and does not start the later ones.
- `check` reviews every report that has no review yet, in order, and writes one
  review per instruction. `merge` merges approved pull requests in order and
  stops at the first one that is not approved.

The cost: if a review asks for changes low in the stack, everything above it
has to take those changes in too (by merging the fixed branch upward, never by
rewriting pushed history).

## Verdicts

- **Approved**: the pull requests can be merged as they are.
- **Changes requested**: the pull requests stay open. The next instruction lists
  the changes and names the same branch; the implementer adds commits to it and
  writes a new report under the new number.
- **Blocked on owner**: the work cannot be judged until the owner decides
  something. The review says exactly what.

## Rules

1. **Published files are not edited.** A correction is a new file with the next
   number that says which one it replaces. This keeps the record honest.
2. **Each party writes only its own files.** The reviewer never edits a report;
   the implementer never edits an instruction, a review or `STATUS.md`.
3. **Only the reviewer puts handoff files on `main` directly**, and only files
   under `handoff/`. Everything else reaches `main` through a pull request.
4. **Only the reviewer merges into `main`**, on the owner's word, after CI is
   green on Linux, Windows and macOS.
5. **Nobody force-pushes `main`.** If it ever seems necessary, stop and ask the
   owner.
6. **An instruction is the whole brief.** The implementer does not rely on
   earlier chat. If an instruction is unclear or seems wrong, the implementer
   does the parts that are clear, writes the question in the report, and stops.
7. **No work outside the open instruction.** Ideas go in the report under
   "Suggestions".
8. **The report says what is true, not what was intended.** Anything not done,
   not pushed or not verified is listed as such.
9. **Decisions made in chat are written down.** When the owner settles something
   in conversation, the reviewer records it in the next instruction or review
   under "Owner decisions".

## Commits in this folder

- Reviewer, on `main`: `docs(handoff): publish instruction NNNN`,
  `docs(handoff): review NNNN`.
- Implementer, on the work branch: `docs(handoff): report NNNN`.

Files here are checked by Prettier like every other file, so run
`pnpm format` before committing.
