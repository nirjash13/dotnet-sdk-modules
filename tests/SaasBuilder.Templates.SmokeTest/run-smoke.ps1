#Requires -Version 5.1
<#
.SYNOPSIS
    Smoke test for the SaasBuilder.Templates NuGet package.

.DESCRIPTION
    Validates that the saas-api template can be packed, installed, and used to scaffold
    a new project without errors. The generated project is NOT compiled or run — that
    requires the SDK packages to be available on a NuGet feed (Phase 1.5).

    TODO (Phase 1.5): once SaasBuilder.Host, SaasBuilder.Persistence, and
    SaasBuilder.SharedKernel are published to a feed, add:
        dotnet restore $smokeDir\Smoke.Test
        dotnet build   $smokeDir\Smoke.Test --no-restore -warnaserror

    Run manually:
        .\tests\SaasBuilder.Templates.SmokeTest\run-smoke.ps1

    This script is NOT wired into CI yet — that happens in Phase 1.5 when the SDK
    packages are published and the template can be compiled end-to-end.

.NOTES
    Exit code 0 = success. Non-zero = failure (PowerShell propagates $LASTEXITCODE).
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Resolve repo root (script lives at tests/SaasBuilder.Templates.SmokeTest/)
# ---------------------------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Resolve-Path (Join-Path $scriptDir '..\..')

Write-Host "==> Repo root: $repoRoot"

# ---------------------------------------------------------------------------
# Step 1: Pack the templates project
# ---------------------------------------------------------------------------
$artifactsDir = Join-Path $repoRoot 'artifacts\templates'
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

$templatesCsproj = Join-Path $repoRoot 'templates\SaasBuilder.Templates\SaasBuilder.Templates.csproj'
Write-Host "==> Packing $templatesCsproj ..."

dotnet pack $templatesCsproj -c Release -o $artifactsDir
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed (exit $LASTEXITCODE)" }

# ---------------------------------------------------------------------------
# Step 2: Find the produced .nupkg
# ---------------------------------------------------------------------------
$nupkg = Get-ChildItem -Path $artifactsDir -Filter 'SaasBuilder.Templates.*.nupkg' |
         Sort-Object LastWriteTime -Descending |
         Select-Object -First 1

if ($null -eq $nupkg) { throw "No SaasBuilder.Templates.*.nupkg found in $artifactsDir" }
Write-Host "==> Found package: $($nupkg.FullName)"

# ---------------------------------------------------------------------------
# Step 3: Create a temp directory for the scaffolded project
# ---------------------------------------------------------------------------
$tempBase = Join-Path ([System.IO.Path]::GetTempPath()) "saasbuilder-smoke-$([System.Guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Force -Path $tempBase | Out-Null
Write-Host "==> Temp dir: $tempBase"

# ---------------------------------------------------------------------------
# Step 4: Install the template package (uninstall first to ensure idempotency)
# ---------------------------------------------------------------------------

# Uninstall any existing version to avoid "file already exists" error on re-run.
# Ignore exit code — if it wasn't installed, uninstall is a no-op.
Write-Host "==> Ensuring SaasBuilder.Templates is not already installed ..."
dotnet new uninstall SaasBuilder.Templates 2>&1 | Out-Null

Write-Host "==> Installing template from $($nupkg.FullName) ..."
dotnet new install $nupkg.FullName
if ($LASTEXITCODE -ne 0) { throw "dotnet new install failed (exit $LASTEXITCODE)" }

# ---------------------------------------------------------------------------
# Step 5: Scaffold a new project
# ---------------------------------------------------------------------------
$smokeDir = Join-Path $tempBase 'Smoke.Test'
Write-Host "==> Scaffolding: dotnet new saas-api -n Smoke.Test -o $smokeDir ..."

dotnet new saas-api -n Smoke.Test -o $smokeDir
if ($LASTEXITCODE -ne 0) { throw "dotnet new saas-api failed (exit $LASTEXITCODE)" }

Write-Host "==> Scaffolded files:"
Get-ChildItem -Recurse $smokeDir | Select-Object -ExpandProperty FullName | ForEach-Object { Write-Host "    $_" }

# ---------------------------------------------------------------------------
# Step 6: Assert expected files exist
# ---------------------------------------------------------------------------
$expectedFiles = @(
    'Program.cs',
    'Smoke.Test.csproj',
    'appsettings.json',
    'appsettings.Development.json',
    'docker-compose.yml',
    'otel-collector-config.yaml',
    '.gitignore',
    '.dockerignore',
    'README.md'
)

foreach ($file in $expectedFiles) {
    $path = Join-Path $smokeDir $file
    if (-not (Test-Path $path)) {
        throw "Expected file missing from scaffolded output: $file"
    }
    Write-Host "    [OK] $file"
}

# TODO (Phase 1.5): restore + build once SDK packages are on a feed
# dotnet restore $smokeDir
# dotnet build   $smokeDir --no-restore -warnaserror

# ---------------------------------------------------------------------------
# Step 7: Uninstall the template (clean up global state)
# ---------------------------------------------------------------------------
Write-Host "==> Uninstalling SaasBuilder.Templates ..."
dotnet new uninstall SaasBuilder.Templates
if ($LASTEXITCODE -ne 0) { throw "dotnet new uninstall failed (exit $LASTEXITCODE)" }

# ---------------------------------------------------------------------------
# Step 8: Remove temp directory
# ---------------------------------------------------------------------------
Remove-Item -Recurse -Force $tempBase
Write-Host "==> Cleaned up $tempBase"

Write-Host ""
Write-Host "==> Smoke test PASSED."
exit 0
