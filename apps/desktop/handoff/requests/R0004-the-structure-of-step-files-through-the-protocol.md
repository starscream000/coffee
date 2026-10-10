# Request R0004: the structure of step files through the protocol

- Date: 2026-10-10
- Written by: desktop implementer
- To: reviewer, for the engine track
- State: open
- Raised in: [report D0003](../reports/D0003-a-complete-app-from-edit-to-run.md)

## What is needed

A protocol request that returns the structure of a test, flow or targets file,
with the position of each part, so that clients stop reading YAML themselves;
a request that lists the project's shared targets; and a mark in
`paramsSchema` on the parameters that take a target.

## Why

The step list, the step forms and the targets editor of instruction D0003
(tasks 14, 15 and 18) had to work out by reading the YAML in the app
(YamlDotNet's parser, [ADR D0007](../../docs/adr/0007-yamldotnet-for-the-step-list.md),
proposed):

1. **The sections and steps** of a test or flow (`before`, `steps`, `after`),
   each step's lines, and which of the three forms it is written in (bare,
   shorthand, long), so that a change rewrites only its lines.
2. **A step's parameters by name**, which for the shorthand form needs the
   action's `shorthand` from `listActions`, and its own settings (`name`,
   `page`, `timeout`, `opens`), told apart from the action key by a list the
   app keeps in step with `docs/step-format.md`.
3. **The targets** of a file: each target's lines, its short or long form,
   `frame`, `within` and its candidates with their kinds and settings.
4. **The shared target names**, for the target picker: the app finds the
   project's `*.targets.yaml` files itself and reads their `targets` keys. The
   config's `targets` globs, which decide which files are shared targets files,
   are known only to the engine, so the app can miss or add files.
5. **Which parameters are targets.** `paramsSchema` does not say; the app
   takes a parameter whose schema allows an object with `candidates` as a
   target. That holds for every built-in action today, but it is a guess about
   the shape of a schema, not a contract.

Each of these is a second reading of the step format, next to the engine's
own. When the format grows (a new common step key, a new candidate kind), the
app reads it wrongly until someone notices. Diagnostics already carry lines
and columns; the structure is the missing half.

## Proposal

Additive, so protocol 0.2.0 (a minor version):

```jsonc
// request "outlineFile": a file on disk or an editor buffer, as validate takes them
{ "file": "tests/checkout.test.yaml", "content": "version: 1\n…" }
// result
{
  "kind": "test",                       // test | flow | targets | config | unknown
  "sections": [{
    "name": "steps", "range": Range,
    "steps": [{
      "range": Range,                   // the item's lines, dash included
      "action": "click", "form": "shorthand",  // bare | shorthand | long
      "params": { "target": { "value": "buy", "range": Range } },  // canonical names
      "settings": { "name": { "value": "Buy it", "range": Range } }
    }]
  }],
  "targets": [{
    "name": "buy", "range": Range, "form": "short",   // short | long
    "frame": null, "within": null,
    "candidates": [{ "range": Range, "fields": { "role": "button", "name": "Buy" } }]
  }],
  "diagnostics": [ /* as validate */ ]
}
// Range: { line, column, endLine, endColumn }, 1-based like Diagnostic

// request "listTargets": no params
{ "targets": [{ "name": "cart.count", "file": "targets/shop.targets.yaml", "line": 4 }] }
```

And in `paramsSchema`, a keyword on target parameters, for example
`"x-target": true` (JSON Schema allows unknown keywords), or a list in
`listActions`: `"targetParams": ["target"]`.

The desktop would keep writing the text itself (only the lines a change is
about, as now); the ranges are what it needs for that.

## Until then

The desktop reads the YAML with YamlDotNet's parser (ADR D0007), finds shared
targets files by their ending, and recognises target parameters by the shape
of their schema. A file it cannot read shows the text editor with a line
saying why.

## Answer
