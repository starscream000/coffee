# 0018. Cache saved logins under a keyed hash with a maximum age

- Status: Proposed
- Date: 2026-10-09

## Context

Saved logins avoid signing in through the UI for every test. The owner asked
for a maximum age per login (default 12 hours), a per-test option to sign in
fresh, and cache keys that hash parameter values instead of storing them.
Login parameters usually include a password.

## Decision

- **Cache key**: HMAC-SHA-256 over a canonical JSON of the environment name,
  the login name, the content of the login flow file (and every flow it calls)
  and the resolved `with` values, secrets included. The HMAC key is 32 random
  bytes created on first use in `.coffee/logins/.key` (git-ignored with the rest
  of the folder).
- **What the HMAC key protects, and what it does not.** It helps when a cache
  file name leaks without the folder, for example in a log line, a CI artifact
  listing or a screenshot of a file list: without the key, nobody can test
  guessed passwords against the name. It does **not** help when the whole
  folder is copied, because the key file is in the same folder. Protecting the
  folder itself (it is git-ignored and holds live session cookies anyway) is
  the user's job.
- **Files**: `.coffee/logins/<key>.json` (Playwright storage state) and
  `.coffee/logins/<key>.meta.json` with `createdAt`, environment name, login
  name and engine version. Neither file contains any parameter value.
- **maxAge** per login in the config, default `12h`. A state older than that is
  not used; the flow runs again and replaces it. `--refresh-logins` ignores all
  saved states for one run (and replaces them).
- **freshLogin: true** on a test: every login in that test runs its flow, and
  the resulting state is used for that test only, neither read from nor written
  to the cache.
- **Clean-up at the start of every run**, before any test: the engine reads
  each `*.meta.json` in `.coffee/logins/` and deletes the state and its
  metadata when the state is older than its login's current `maxAge`, or when
  the metadata names an environment or a login that the config no longer has.
  A state file without readable metadata is deleted too. A file that cannot be
  deleted (for example because it is open) is a `warn` log
  (`LoginCleanupFailed`) and is tried again at the next run; it never fails
  the run. The `.key` file is never deleted by clean-up.
- A login flow failure fails every test instance that needs the login, with the
  login flow's own error and location.

## Alternatives rejected

- **File named after the login only** (`customer.json`, the first proposal): a
  changed password or environment would silently reuse an old session.
- **Plain SHA-256 of the parameters**: a short password could be found by
  hashing guesses.
- **Keeping states in memory only**: every run (and every CLI call) would sign
  in again, which is slow and can trip rate limits.
- **No expiry, relying on the app to reject old sessions**: tests would fail
  mid-run with confusing errors when a session expires.

## Consequences

Changing a password, the login flow or the environment automatically produces a
new session. The old state is no longer used and is deleted at the start of a
later run once it is older than `maxAge`, so live session cookies do not stay
on disk indefinitely. The storage state files are git-ignored and never sent
over the protocol.

## Revisit when

The server (v0.4.0) needs to share sessions between runs on different
machines, or applications under test require sessions to be re-created more
often than any reasonable `maxAge`.
