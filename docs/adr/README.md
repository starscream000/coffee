# Architecture decision records

Why we keep them: [0001](0001-record-architecture-decisions.md). New records
start from [template.md](template.md); every record ends with a "Revisit when"
condition.

| ADR                                           | Title                                                             | Status             |
| --------------------------------------------- | ----------------------------------------------------------------- | ------------------ |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions                                     | Accepted           |
| [0002](0002-pnpm-workspace-monorepo.md)       | Use a pnpm workspace with tsc project references                  | Accepted           |
| [0003](0003-node-and-typescript-versions.md)  | Pin Node 24 LTS and TypeScript 6.0                                | Accepted           |
| [0004](0004-vitest-for-all-tests.md)          | Use Vitest for unit and integration tests                         | Accepted           |
| [0005](0005-json-rpc-over-stdio.md)           | Frame JSON-RPC as newline-delimited JSON on stdio                 | Proposed           |
| [0006](0006-zod-schemas.md)                   | Use Zod for schemas, and `yaml` for parsing                       | Proposed           |
| [0007](0007-page-snapshot-format.md)          | Record page snapshots with Playwright tracing, one chunk per step | Proposed           |
| [0008](0008-loading-user-actions.md)          | Compile user actions with esbuild                                 | Proposed           |
| [0009](0009-clients-locate-engine.md)         | Clients locate the engine by path and start it with Node          | Accepted           |
| [0010](0010-locator-candidates.md)            | Resolve targets by trying candidates in stored order              | Proposed           |
| [0011](0011-protocol-versioning.md)           | Version the protocol separately, with a handshake at connect      | Accepted           |
| [0012](0012-own-runner.md)                    | Run tests with our own runner on the Playwright library           | Proposed           |
| [0013](0013-data-rows.md)                     | Data rows repeat the whole test; no flow-level `forEach` yet      | Accepted           |
| [0014](0014-secret-masking.md)                | Mask secrets at every exit point, by value                        | Proposed           |
| [0015](0015-results-layout.md)                | Write each run to its own folder with a fixed layout              | Proposed           |
| [0016](0016-action-names.md)                  | Namespace user actions; reserve plain names for built-ins         | Accepted\*         |
| [0017](0017-product-identity.md)              | Define the product identity in one constant                       | Superseded by 0019 |
| [0018](0018-saved-logins.md)                  | Cache saved logins under a keyed hash with a maximum age          | Proposed           |
| [0019](0019-four-product-names.md)            | Keep four independent product names                               | Accepted           |

\* Except the reserved `expect` and `wait` namespaces (architecture open
question 13).
