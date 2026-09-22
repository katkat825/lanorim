# Does every key the engine emits have English?
#
# The audit itself lives in content.tests (LocaleTests), so it cannot be forgotten - this script
# is the one-line verdict, plus the scaffolder that fills in anything new.
#
# Unlike the old build's check scripts, this one BUILDS FIRST. A check that runs stale code is
# worse than no check.

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'building...' -ForegroundColor DarkGray
dotnet build sim/sim.csproj -v q --nologo | Out-Null
if (-not $?) { Write-Host 'the build failed' -ForegroundColor Red; exit 1 }

Write-Host ''
dotnet run --project sim --no-build -- locale
$scaffolded = $LASTEXITCODE

Write-Host ''
Write-Host 'the locale audit, as a test:' -ForegroundColor DarkGray
dotnet test content.tests/content.tests.csproj --nologo -v q --filter 'FullyQualifiedName~LocaleTests'
$tested = $LASTEXITCODE

Write-Host ''

if ($scaffolded -eq 0 -and $tested -eq 0) {
    Write-Host 'check passed - every key has English' -ForegroundColor Green
    exit 0
}

Write-Host 'check FAILED' -ForegroundColor Red
exit 1
