# Does every key the engine emits have English?
#
# The audit itself lives in content.tests (LocaleTests), so it cannot be forgotten - this script
# is the one-line verdict. game/locale/game.csv is written by hand; `sim locale` only reads it.

. (Join-Path $PSScriptRoot '_common.ps1')

Build-First sim/sim.csproj

Write-Host ''
dotnet run --project sim --no-build -- locale
$audited = $LASTEXITCODE

Write-Host ''
Write-Host 'the locale audit, as a test:' -ForegroundColor DarkGray
dotnet test content.tests/content.tests.csproj --nologo -v q --filter 'FullyQualifiedName~LocaleTests'
$tested = $LASTEXITCODE

Write-Host ''

if ($audited -eq 0 -and $tested -eq 0) { Pass 'every key has English' }

Fail
