# Are the dice uniform?
#
# Chi-squared over every die the game owns, on a fresh seed each run - so this is the one check
# that is deliberately not deterministic. A SUSPICIOUS verdict happens to a fair die one run in
# twenty; throw it again before believing it. BIASED twice running is a real fault.

. (Join-Path $PSScriptRoot '_common.ps1')

Build-First sim/sim.csproj

Write-Host ''
dotnet run --project sim --no-build -- fairness
$verdict = $LASTEXITCODE

Write-Host ''

if ($verdict -eq 0) { Pass 'no die is biased' }

Fail 'a die came out biased. Run it once more; if it says the same thing twice, the generator is at fault'
