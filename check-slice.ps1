# The first playable slice, played headless on the real engine:
# make a character -> stand on a map built with the map builder -> fight and beat a goblin -> level up.
#
# That is the milestone of docs/v1_build_order.md, Phase 3. This script proves the rules half of
# it. The other half - does it look right on the table - is yours, in Godot.
#
# Then the balance tables, so you can see whether the numbers are anywhere near fair.

param([int]$Runs = 500)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'building...' -ForegroundColor DarkGray
dotnet build sim/sim.csproj -v q --nologo | Out-Null
if (-not $?) { Write-Host 'the build failed' -ForegroundColor Red; exit 1 }

Write-Host ''
Write-Host 'the slice, end to end:' -ForegroundColor DarkGray

dotnet test content.tests/content.tests.csproj --nologo -v q --filter 'FullyQualifiedName~FirstSliceTests'
$slice = $LASTEXITCODE

Write-Host ''
Write-Host 'the fight rules:' -ForegroundColor DarkGray

dotnet test core.tests/core.tests.csproj --nologo -v q `
    --filter 'FullyQualifiedName~EncounterTests|FullyQualifiedName~TacticsTests|FullyQualifiedName~BattlefieldTests|FullyQualifiedName~TurnTests'
$fight = $LASTEXITCODE

Write-Host ''
dotnet run --project sim --no-build -- balance $Runs

Write-Host ''

if ($slice -eq 0 -and $fight -eq 0) {
    Write-Host 'check passed - character to map to goblin to level-up' -ForegroundColor Green
    exit 0
}

Write-Host 'check FAILED' -ForegroundColor Red
exit 1
