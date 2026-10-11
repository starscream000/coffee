# D0008. Name every colour, size and spacing once, and use four kinds of button

- Status: Accepted (instruction D0004, tasks 6 and 7)
- Date: 2026-10-11

## Context

The owner used the app on 2026-10-10. In his words, the window is all black,
which is fine, but "the boundaries of the subsections and their hierarchies
are unclear and confusing". The screenshot showed:

- every surface in the same black, with no line or tone between the explorer,
  the step list, the form and the text;
- an unexplained grey block at the bottom;
- every button looking the same, so nothing said what the main action was;
- a barely marked active tab.

Under the surface, colours and spacing were written in each view (`#80000000`,
`Padding="8,2"`, `FontSize="14"`), and Fluent's light and dark system brushes
were mixed in, so no two parts of the window agreed.

## Decision

**One token file.** `src/Desktop.App/Themes/Tokens.axaml` names every colour,
spacing step, corner radius, text size and fixed size once. The dark theme
stays (`RequestedThemeVariant="Dark"`).

| Token                                    | Value                           | Used for                                                      |
| ---------------------------------------- | ------------------------------- | ------------------------------------------------------------- |
| `Window`                                 | `#0F1115`                       | behind everything                                             |
| `Panel`                                  | `#161A20`                       | every panel's body                                            |
| `Raised`                                 | `#1D222A`                       | panel headers, cards, inputs, the selected tab                |
| `Hover`, `Pressed`                       | `#252B35`, `#2D3440`            | a raised surface under the pointer, and pressed               |
| `Line`, `LineStrong`                     | `#2A303A`, `#3A4250`            | the line between panels and around cards; borders of controls |
| `Text`, `TextMuted`, `TextDisabled`      | `#E6E8EB`, `#9AA3AF`, `#5C6573` | normal text; labels, hints, counts; disabled controls         |
| `Accent`, `AccentHover`, `AccentPressed` | `#4C8DFF`, `#6A9FFF`, `#3B78E6` | the main action, selection, focus                             |
| `AccentSubtle`                           | `#4C8DFF` at 20 %               | the background of a selected row                              |
| `Passed`, `Warning`, `Failed`            | `#3FB950`, `#D29922`, `#F85149` | outcomes and problems                                         |
| `…Subtle`                                | the same at 15–20 %             | the background of a problem line or a failure                 |
| `Overlay`                                | `Window` at 70 %                | behind a dialog                                               |

- **Spacing** in steps of 4: `Space1` to `Space6` (4, 8, 12, 16, 24) for gaps,
  and `Inset…` thicknesses for padding and margins.
- **Text sizes:** 12 for labels and counts (`TextLabel`), 13 for body text
  (`TextBody`), 15 for panel titles (`TextTitle`), 20 for page titles
  (`TextPage`). The monospaced `CodeFont` is used only for file text and code.
- **Corners:** `RadiusSmall` (4) for controls, `Radius` (6) for cards,
  `RadiusPill` for badges.

`Accent` is passed to Fluent as its `SystemAccentColor`, so its own controls
(check boxes, progress bars) use the same blue.

**One look per kind of control.** `src/Desktop.App/Themes/Controls.axaml`
styles the window, text, inputs, lists, trees, tabs, cards, badges and
splitters from the tokens. Buttons come in exactly four kinds:

| Kind   | Class    | Looks like                         | Used for                                               |
| ------ | -------- | ---------------------------------- | ------------------------------------------------------ |
| main   | `main`   | filled with the accent, semi-bold  | the one main action of a region (Save, Add step, Open) |
| normal | none     | raised, with a line                | every other action                                     |
| quiet  | `quiet`  | no fill or line until hovered      | toolbars, list rows, tab close buttons                 |
| danger | `danger` | the failed colour as text and line | Delete, Remove; set apart, and they ask first          |

Every control shows keyboard focus: buttons and inputs draw their border in
the accent (the text colour on a main or danger button).

**Views only pick.** A view uses classes (`Classes="card"`, `Classes="label"`)
and token resources. It never writes a colour, a text size, a spacing or a
fixed size. A test (`DesignTokenTests`) reads every view and fails when it
does, and also when a view's code builds a colour or names a token that does
not exist. Grid rows and columns may share out space (`*`, `Auto`); a fixed
size comes from a token.

## Alternatives rejected

- **Fluent's own palette, restyled through its resource keys.** Fluent's keys
  are internal to its compiled theme and change between versions. Styling the
  template parts (`PART_ContentPresenter`, `PART_BorderElement`) from the
  tokens is explicit and is checked by the screenshots of the UI tests.
- **A light theme as well.** Out of scope (instruction D0004); the token file
  makes it a second set of values later.
- **A check by convention only.** Without the test, the next view would bring
  a `Padding="8,2"` back within a week.

## Consequences

- One place to change the look. A new view has to find its spacing in the
  tokens, which is slower than writing a number but keeps the window coherent.
- When Fluent renames a template part, a style stops applying. The UI tests'
  screenshots show it.
- Some views need a token for a one-off size (`ColumnName`, `ListMaxHeight`).
  That is deliberate: such sizes are visible and reviewed in one file.

## Revisit when

- The owner asks for a light theme, or for high contrast.
- Avalonia's Fluent theme offers public, stable resource keys for its control
  states.
