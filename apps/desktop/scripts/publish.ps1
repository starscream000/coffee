# Builds the engine of this checkout and publishes the desktop app into
# apps/desktop/dist/app, then prints the path of the app's executable. The app
# runs from inside the checkout, where it finds the engine it was built with
# (ADR D0004: the engine of a development checkout above the app). The app
# needs the .NET 10 runtime, and the engine Node 24.
# Usage: pwsh apps/desktop/scripts/publish.ps1   (from anywhere in the repository)
$ErrorActionPreference = 'Stop'

$desktop = Resolve-Path (Join-Path $PSScriptRoot '..')
$repo = Resolve-Path (Join-Path $desktop '../..')
$out = Join-Path $desktop 'dist/app'

function Invoke-Pnpm {
  if (Get-Command pnpm -ErrorAction SilentlyContinue) { & pnpm @args } else { & corepack pnpm @args }
  if ($LASTEXITCODE -ne 0) { throw "pnpm $args failed with exit code $LASTEXITCODE." }
}

Write-Host '== engine: install and build'
Push-Location $repo
try {
  Invoke-Pnpm install --frozen-lockfile
  Invoke-Pnpm build
}
finally {
  Pop-Location
}

Write-Host "== app: publish to $out"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
& dotnet publish (Join-Path $desktop 'src/Desktop.App/Desktop.App.csproj') -c Release -o $out -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

$exe = Join-Path $out 'Desktop.App'
if (Test-Path "$exe.exe") { $exe = "$exe.exe" }
if (-not (Test-Path $exe)) { throw "The app was published, but its executable is not where it was expected: $exe" }

Write-Host '== published; start the app with:'
Write-Host $exe
