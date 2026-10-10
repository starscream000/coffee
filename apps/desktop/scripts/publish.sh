#!/usr/bin/env bash
# Builds the engine of this checkout and publishes the desktop app into
# apps/desktop/dist/app, then prints the path of the app's executable. The app
# runs from inside the checkout, where it finds the engine it was built with
# (ADR D0004: the engine of a development checkout above the app). The app
# needs the .NET 10 runtime, and the engine Node 24.
# Usage: apps/desktop/scripts/publish.sh   (from anywhere in the repository)
set -euo pipefail

desktop="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo="$(cd "$desktop/../.." && pwd)"
out="$desktop/dist/app"

run_pnpm() {
  if command -v pnpm >/dev/null 2>&1; then pnpm "$@"; else corepack pnpm "$@"; fi
}

echo "== engine: install and build"
(cd "$repo" && run_pnpm install --frozen-lockfile && run_pnpm build)

echo "== app: publish to $out"
rm -rf "$out"
dotnet publish "$desktop/src/Desktop.App/Desktop.App.csproj" -c Release -o "$out" -p:RestoreLockedMode=true

exe="$out/Desktop.App"
if [ -f "$exe.exe" ]; then exe="$exe.exe"; fi
if [ ! -f "$exe" ]; then
  echo "The app was published, but its executable is not where it was expected: $exe" >&2
  exit 1
fi

echo "== published; start the app with:"
echo "$exe"
