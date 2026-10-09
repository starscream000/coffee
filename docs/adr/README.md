# Architecture decision records

Why we keep them: [0001](0001-record-architecture-decisions.md). New records
start from [template.md](template.md).

| ADR                                           | Title                                                      | Status   |
| --------------------------------------------- | ---------------------------------------------------------- | -------- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions                              | Accepted |
| [0002](0002-pnpm-workspace-monorepo.md)       | Use a pnpm workspace with tsc project references           | Accepted |
| [0003](0003-node-and-typescript-versions.md)  | Pin Node 24 LTS and TypeScript 6.0                         | Accepted |
| [0004](0004-vitest-for-all-tests.md)          | Use Vitest for unit and integration tests                  | Accepted |
| [0005](0005-json-rpc-over-stdio.md)           | Frame JSON-RPC as newline-delimited JSON on stdio          | Proposed |
| [0006](0006-zod-schemas.md)                   | Use Zod for schemas, and `yaml` for parsing                | Proposed |
| [0007](0007-page-snapshot-format.md)          | Save page snapshots as MHTML                               | Proposed |
| [0008](0008-loading-user-actions.md)          | Compile user actions with esbuild                          | Proposed |
| [0009](0009-clients-locate-engine.md)         | Clients locate the engine by path and start it with Node   | Proposed |
| [0010](0010-locator-candidates.md)            | Resolve targets by trying candidates in stored order       | Proposed |
| [0011](0011-protocol-versioning.md)           | Version the protocol separately, 0.x until the desktop app | Proposed |

ADRs 0002–0004 describe tooling already in place on `main`. They are marked
Accepted so that changing them takes a new ADR rather than a silent edit.
