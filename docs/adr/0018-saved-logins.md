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
  of the folder). Using an HMAC rather than a plain hash means a copied cache
  folder without the key file does not allow guessing a weak password from the
  hash.
- **Files**: `.coffee/logins/<key>.json` (Playwright storage state) and
  `.coffee/logins/<key>.meta.json` with `createdAt`, environment name, login
  name and engine version. Neither file contains any parameter value.
- **maxAge** per login in the config, default `12h`. A state older than that is
  not used; the flow runs again and replaces it. `--refresh-logins` ignores all
  saved states for one run (and replaces them).
- **freshLogin: true** on a test: every login in that test runs its flow, and
  the resulting state is used for that test only, neither read from nor written
  to the cache.
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
new session. Old cache files remain until the folder is cleared (retention is
open question 16). The storage state files hold live session cookies; they are
git-ignored and never sent over the protocol.

## Revisit when

The server (v0.4.0) needs to share sessions between runs on different
machines, or applications under test require sessions to be re-created more
often than any reasonable `maxAge`.
