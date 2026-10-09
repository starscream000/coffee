#!/usr/bin/env bash
# Runs every check the desktop app must pass before a pull request: code style,
# a warnings-as-errors build, all tests, and Prettier on the desktop's Markdown
# and JSON files (the repository's root `pnpm verify` checks those too).
# Usage: apps/desktop/scripts/verify.sh   (from anywhere in the repository)
set -euo pipefail

desktop="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo="$(cd "$desktop/../.." && pwd)"

echo "== restore (locked)"
dotnet restore "$desktop/Desktop.slnx" --locked-mode
echo "== format"
dotnet format "$desktop/Desktop.slnx" --verify-no-changes --no-restore
echo "== build"
dotnet build "$desktop/Desktop.slnx" --no-restore -c Release
echo "== test"
dotnet test "$desktop/Desktop.slnx" --no-build -c Release
echo "== prettier"
(cd "$repo" && corepack pnpm exec prettier --check apps/desktop)
echo "== desktop verify passed"
