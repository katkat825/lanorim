# The first playable slice, played headless on the real engine:
# make a character -> stand on a map built with the map builder -> fight and beat a goblin -> level up.
#
# That is the milestone of docs/v1_build_order.md, Phase 3. This script proves the rules half of
# it. The other half - does it look right on the table - is yours, in Godot.
#
# Then the balance tables, so you can see whether the numbers are anywhere near fair.

param([int]$Runs = 500)

. (Join-Path $PSScriptRoot '_common.ps1')

Build-First sim/sim.csproj

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

if ($slice -eq 0 -and $fight -eq 0) { Pass 'character to map to goblin to level-up' }

Fail
