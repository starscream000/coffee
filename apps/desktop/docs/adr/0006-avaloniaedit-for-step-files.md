# D0006. Edit step files with AvaloniaEdit

- Status: Proposed
- Date: 2026-10-10

## Context

Instruction D0002 makes step files editable (milestone D4 of the
[plan](../plan.md)): line numbers, undo and redo, problem lines marked, and
saving. Avalonia has no code editor of its own; a `TextBox` has no line
numbers, no per-line decorations and slows down on long text.

## Decision

- Use **Avalonia.AvaloniaEdit 12.0.0** (MIT), the Avalonia port of the
  editor of SharpDevelop and ILSpy, pinned in `Directory.Packages.props`. It
  depends on Avalonia ≥ 12.0.0 and works with the pinned Avalonia 12.1.3; its
  Fluent theme is included from `avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`.
- The view model owns the text as an AvaloniaEdit `TextDocument`, which also
  holds the undo history; the view binds the editor's `Document` to it. The
  document belongs to the thread that created it (the UI thread), so the view
  model touches it only there, and its tests run on Avalonia's headless UI
  thread.
- Problem lines are drawn by a small background renderer in the view from the
  view model's `LineMarks`; no syntax colouring, and no package for it
  (AvaloniaEdit.TextMate would add native code).

## Alternatives rejected

- **A plain `TextBox`**: no line numbers or line marks; slow with long files.
- **Our own editor control**: far more work than the whole of D4 for the same
  result.
- **Keeping the text as a `string` in the view model** and syncing it into the
  editor: every sync from the view model would reset the editor's undo
  history and caret.

## Consequences

The view model depends on one type of the editor package (`TextDocument`),
which is not a control and needs no window. Completion and colouring (later
in D4) can build on the same editor.

## Revisit when

AvaloniaEdit stops following Avalonia's releases, or completion needs an
editor model it does not offer.
