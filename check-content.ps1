# Does every SRD data file load, and does it say what the design docs say it should?
#
# The seven classes, the seven species, the 62 MUST spells, the items, the backgrounds and the
# bestiary - read out of the Content assembly and held against docs/v1_class_roster.md,
# docs/v1_species_roster.md and docs/v1_spell_list.md.

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'building...' -ForegroundColor DarkGray
dotnet build sim/sim.csproj -v q --nologo | Out-Null
if (-not $?) { Write-Host 'the build failed' -ForegroundColor Red; exit 1 }

Write-Host ''
dotnet run --project sim --no-build -- spells
$spells = $LASTEXITCODE

Write-Host ''
Write-Host 'the data held against the design docs:' -ForegroundColor DarkGray

dotnet test content.tests/content.tests.csproj --nologo -v q `
    --filter 'FullyQualifiedName~LibraryTests|FullyQualifiedName~SpellFileTests|FullyQualifiedName~BestiaryTests'
$tested = $LASTEXITCODE

Write-Host ''

if ($spells -eq 0 -and $tested -eq 0) {
    Write-Host 'check passed - every data file loads and matches its design doc' -ForegroundColor Green
    exit 0
}

Write-Host 'check FAILED' -ForegroundColor Red
exit 1
