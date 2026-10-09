# Request R0001: a CI job for the desktop app

- Date: 2026-10-09
- Written by: desktop implementer
- To: reviewer (change to `.github/workflows/ci.yml`)
- State: done
- Raised in: [report D0001](../reports/D0001-desktop-foundation.md)

## What is needed

A `desktop` job in `.github/workflows/ci.yml` that builds and tests
`apps/desktop` on Linux, Windows and macOS, so that `main` stays green for the
desktop app too.

## Why

The root rule is that CI checks every change on all three systems. Today CI
runs only the pnpm checks: they cover the desktop's Markdown and JSON
(Prettier) but no C# at all. A broken desktop build could reach `main`
unnoticed. The desktop implementer may not edit `.github/`.

## Proposal

Add this job next to `verify` (the action versions should match whatever the
file uses when it is applied; `actions/setup-dotnet@v5` was the current major
when this was written):

```yaml
desktop:
  name: desktop (${{ matrix.os }})
  runs-on: ${{ matrix.os }}
  strategy:
    fail-fast: false
    matrix:
      os: [ubuntu-latest, windows-latest, macos-latest]
  env:
    ContinuousIntegrationBuild: true
    DOTNET_NOLOGO: true
    # Real-engine tests fail instead of skipping when the engine is missing.
    DESKTOP_TESTS_REQUIRE_ENGINE: 1
  steps:
    - uses: actions/checkout@v7
    - uses: pnpm/action-setup@v6
    - uses: actions/setup-node@v7
      with:
        node-version-file: .nvmrc
        cache: pnpm
    - uses: actions/setup-dotnet@v5
      with:
        global-json-file: apps/desktop/global.json
        cache: true
        cache-dependency-path: apps/desktop/**/packages.lock.json
    # The engine build, for the desktop tests that talk to the real engine.
    - run: pnpm install --frozen-lockfile
    - run: pnpm build
    - run: dotnet restore apps/desktop/Desktop.slnx --locked-mode
    - run: dotnet format apps/desktop/Desktop.slnx --verify-no-changes --no-restore
    - run: dotnet build apps/desktop/Desktop.slnx --no-restore -c Release
    - run: dotnet test apps/desktop/Desktop.slnx --no-build -c Release
```

Optionally, a `paths` filter can skip the job when nothing under
`apps/desktop/`, `packages/protocol/` or `packages/engine/` changed; the
desktop tests read the protocol's schemas and start the engine, so those
folders must stay in the filter.

## Until then

The desktop implementer runs `apps/desktop/scripts/verify.sh` on Linux before
every push and says in each report that Windows and macOS are not checked.

## Answer

Accepted and done (reviewer, 2026-10-10). The `desktop` job is on `main` since
pull request #18, as proposed, with one addition: when tests fail, the step
"Report failed tests" shows each failed test and its message on the pull
request (`.github/scripts/report-failed-tests.mjs`). There is no `paths`
filter: the job takes a few minutes, and a filter is one more thing to get
wrong. From now on every desktop pull request is built and tested on Linux,
Windows and macOS, so reports no longer need to say that only Linux was
checked once CI has run.
