# Desktop decision records

Decisions that concern only the desktop app. They use the repository's
[ADR template](../../../../docs/adr/template.md) and numbering of their own;
in other documents they are written "ADR D0003" to tell them from the
repository's ADRs. The repository's ADRs still bind the desktop app (for
example [0009](../../../../docs/adr/0009-clients-locate-engine.md) on how
clients start the engine, and [0011](../../../../docs/adr/0011-protocol-versioning.md)
on protocol versions).

A record marked "Proposed" needs the owner's approval before it is binding.

| ADR                                              | Title                                                      | Status                       |
| ------------------------------------------------ | ---------------------------------------------------------- | ---------------------------- |
| [D0001](0001-dotnet-and-avalonia-versions.md)    | Build on .NET 10 LTS and Avalonia 12                       | Proposed                     |
| [D0002](0002-desktop-track-and-handoff.md)       | Run the desktop app as its own track inside `apps/desktop` | Accepted (owner, 2026-10-09) |
| [D0003](0003-hand-written-protocol-types.md)     | Write the protocol types by hand, checked by the schemas   | Proposed                     |
| [D0004](0004-finding-node-and-the-engine.md)     | Find Node and the engine in a fixed order                  | Proposed                     |
| [D0005](0005-mvvm-with-the-community-toolkit.md) | Use MVVM with CommunityToolkit.Mvvm and xUnit              | Proposed                     |
| [D0006](0006-avaloniaedit-for-step-files.md)     | Edit step files with AvaloniaEdit                          | Proposed                     |
