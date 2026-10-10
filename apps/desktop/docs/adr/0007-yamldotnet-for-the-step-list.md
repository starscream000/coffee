# D0007. Read step files with YamlDotNet for the step list

- Status: Proposed
- Date: 2026-10-10

## Context

Instruction D0003 (task 14) adds a step list beside the text of a test or
flow file. The app has to find the steps itself: the engine's `validate`
reports problems, not the structure of a file, and the protocol has no
request for it. A change made in the step list must rewrite only the lines
of the step concerned, so that comments and layout elsewhere stay as they
were and a Git diff stays minimal ([step format](../../../../docs/step-format.md),
"Editing by tools"). The engine edits files through the `yaml` package's
document model; .NET has no package that keeps comments through a
read-modify-write cycle.

## Decision

- Use **YamlDotNet 18.1.0** (MIT), pinned in `Directory.Packages.props`, and
  only its low-level **parser** (`YamlDotNet.Core.Parser`), which reports
  every node with its start and end position in the text.
- `StepFiles/YamlTree` turns the parser's events into a small tree of
  scalars, mappings and sequences with their offsets and lines;
  `StepFiles/StepOutline` finds the sections (`before`, `steps`, `after`) and
  each step's lines and form (bare, shorthand, long).
- Changes are made **on the text**, not on a document model:
  `StepFiles/StepEdits` replaces the lines of the steps concerned and leaves
  everything else as it was; the editor applies the result as one
  replacement of the part that differs, so its undo takes a change back in
  one step and the text editor and the step list always show the same text.
- What the tree does not cover makes the step list say why and show the text
  only: invalid YAML, several documents, anchors and aliases, tags, complex
  keys, and sections written as flow lists (`steps: [back]`).

## Alternatives rejected

- **YamlDotNet's representation model or serializer**: they drop comments and
  rewrite the whole file in their own layout.
- **Asking the engine for the structure**: there is no such request in
  protocol 0.1.0; request R0004 proposes one.
- **Our own YAML scanner**: YAML's rules for indentation, quoting and flow
  collections are easy to get wrong; the parser gets them right.
- **SharpYaml**: a fork of an older YamlDotNet; less maintained.

## Consequences

One package more, used in one folder of the app. The writing side (the text
of a new or changed step) is the app's own and covers only what the step
list writes. The targets editor (task 18) reads targets the same way.

## Revisit when

The engine offers the structure of a file through the protocol, or a .NET
YAML package keeps comments through edits.
