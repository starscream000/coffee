# Runs every check the desktop app must pass before a pull request: code style,
# a warnings-as-errors build, all tests, and Prettier on the desktop's Markdown
# and JSON files. Windows counterpart of verify.sh.
# Usage: pwsh apps/desktop/scripts/verify.ps1
$ErrorActionPreference = 'Stop'
$desktop = Split-Path -Parent $PSScriptRoot
$repo = Resolve-Path (Join-Path $desktop '../..')
$solution = Join-Path $desktop 'Desktop.slnx'

function Invoke-Step([string] $name, [scriptblock] $step) {
    Write-Host "== $name"
    & $step
    if ($LASTEXITCODE -ne 0) { throw "$name failed (exit code $LASTEXITCODE)" }
}

Invoke-Step 'restore (locked)' { dotnet restore $solution --locked-mode }
Invoke-Step 'format' { dotnet format $solution --verify-no-changes --no-restore }
Invoke-Step 'build' { dotnet build $solution --no-restore -c Release }
Invoke-Step 'test' { dotnet test $solution --no-build -c Release }
Invoke-Step 'prettier' { Push-Location $repo; try { corepack pnpm exec prettier --check apps/desktop } finally { Pop-Location } }
Write-Host '== desktop verify passed'
