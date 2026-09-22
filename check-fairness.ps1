# Are the dice uniform?
#
# Chi-squared over every die the game owns, on a fresh seed each run - so this is the one check
# that is deliberately not deterministic. A SUSPICIOUS verdict happens to a fair die one run in
# twenty; throw it again before believing it. BIASED twice running is a real fault.

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'building...' -ForegroundColor DarkGray
dotnet build sim/sim.csproj -v q --nologo | Out-Null
if (-not $?) { Write-Host 'the build failed' -ForegroundColor Red; exit 1 }

Write-Host ''
dotnet run --project sim --no-build -- fairness
$verdict = $LASTEXITCODE

Write-Host ''

if ($verdict -eq 0) {
    Write-Host 'check passed - no die is biased' -ForegroundColor Green
    exit 0
}

Write-Host 'check FAILED - a die came out biased. Run it once more; if it says the same thing twice, the generator is at fault' -ForegroundColor Red
exit 1
