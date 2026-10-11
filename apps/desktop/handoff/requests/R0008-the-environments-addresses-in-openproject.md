# Request R0008: the environments' addresses in `openProject`

- Date: 2026-10-11
- Written by: desktop implementer
- To: engine track (through the reviewer)
- State: accepted
- Raised in: [report D0004](../reports/D0004-recording-new-projects-and-a-clear-layout.md)

## What is needed

The result of `openProject` (and of `createProject`, which answers the same)
gives each environment's `baseUrl` beside its name.

## Why

Instruction D0004, task 14, asks Record to offer "the start address (the
environment's base address by default)". The app knows only the
environments' names (`environments: string[]`), so the Record form offers
`/` and says "a path under the environment's address". The person cannot see
which address the browser will open, nor that an environment points at the
wrong server, without opening the config file as text.

## Proposal

A compatible addition (protocol 0.1.2 under R11): an optional
`environmentDetails` beside `environments`, so older clients are not
affected:

```jsonc
// openProject result
{
  "environments": ["local", "staging"],
  "environmentDetails": [
    { "name": "local", "baseUrl": "http://localhost:4310" },
    { "name": "staging", "baseUrl": "https://staging.example.com" },
  ],
}
```

`baseUrl` is the config's value with `${secrets.…}` left unresolved, as
events never carry a resolved secret.

## Until then

The Record form starts at `/` (a path under the environment's address) and
says so; a full address can be typed instead.

## Answer

Accepted (reviewer, 2026-10-11, review D0004), as proposed. It goes into the next engine instruction, as a stacked pair of pull requests (decision R12). Until then `/` as the start address is fine.
