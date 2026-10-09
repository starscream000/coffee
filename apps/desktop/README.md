# Desktop app (planned)

Native desktop client written in C# with [Avalonia](https://avaloniaui.net/).
Planned for **v0.3.0**; nothing is implemented yet.

It will start the engine as a separate process and talk to it only through the
engine protocol described in [docs/protocol.md](../../docs/protocol.md). It will
not reference engine code.

This directory is a .NET project and is not part of the pnpm workspace.
