# D0005. Use MVVM with CommunityToolkit.Mvvm and xUnit

- Status: Proposed
- Date: 2026-10-09

## Context

The app needs testable presentation logic, and the repository requires unit
tests for all logic.

## Decision

- **MVVM**: views (`.axaml`) only bind; view models hold state and logic;
  services wrap the operating system (dialogs, files, settings, time, the UI
  dispatcher) behind interfaces.
- **CommunityToolkit.Mvvm** for observable properties and commands, through
  its source generators. Compiled bindings are on by default, so every view
  declares `x:DataType` and binding errors fail the build.
- **xUnit v3** for all tests; **Avalonia.Headless.XUnit** for the few tests
  that need real controls (a window opens, a view binds). View models are
  tested without Avalonia.
- No dependency-injection container: the app wires its objects in one place
  (`AppComposition`), which tests replace piece by piece.

## Alternatives rejected

- **ReactiveUI**: powerful, but a second programming model (observables) on top
  of MVVM, and more to learn for a reviewer.
- **Code-behind logic**: hard to test without a window.
- **A DI container** (Microsoft.Extensions.DependencyInjection): one more
  dependency for a graph of a dozen objects.

## Consequences

View models stay plain C#. Tests run fast and without a display.

## Revisit when

The object graph grows to where wiring by hand is error-prone (more than about
40 services), or plug-ins need to add services.
