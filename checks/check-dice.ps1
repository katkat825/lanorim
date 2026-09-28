# Are the PHYSICS dice fair?
#
# This is the one check that cannot run in dotnet test, because the thing being measured is a
# solid tumbling in a wooden box. THE TRAY IS THE RANDOM NUMBER GENERATOR (game/Table/Table.cs):
# a d20 that favours its low faces would be a game that favours them, and every test in the repo
# would still be green.
#
# Chi-squared over the faces, plus a second test on the mean, because chi-squared ignores face
# order and would not notice a tray that rolls high.
#
# A SUSPICIOUS verdict happens to a fair die one run in twenty. Throw it again before believing
# it. BIASED twice running is a real fault in the solid, the tray or the felt.
#
# It is SLOW - every throw is a second and a half of real physics - so it is not in the default
# suite. Run it after touching DieSolid, the tray scene, or a skin's friction or bounce.

param(
    [string] $Godot,
    [string] $Dice = 'd20',
    [int] $Throws = 150,
    [int] $Handful = 8,
    [switch] $Quiet
)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

Write-Host "throwing $Throws x $Handful $Dice - this takes a few minutes" -ForegroundColor DarkGray
Write-Host ''

$log = Temp-Log 'dice'

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://table.tscn', '--',
                       '--dice', $Dice, '--throws', "$Throws", '--handful', "$Handful") $log

$text = Get-Content $log -Raw

if (-not $Quiet) {
    $text -split "`n" |
        Where-Object { $_ -match '^\s*(probe|\s+\d+\s+\d+|chi-squared|worst face|verdict|\d+ throws)' } |
        ForEach-Object { Write-Host $_ }
}

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($text -match 'is BIASED') {
    Fail "the $Dice is biased. Run it once more; if it says the same thing twice, the solid or the tray is at fault"
}

if ($text -match 'probe\s+\S+ in the .* tray: Suspicious') {
    Write-Host "check passed, BUT the $Dice came out SUSPICIOUS - past the 5% line." -ForegroundColor Yellow
    Write-Host "A fair die does that one run in twenty, and a short sweep does it oftener." -ForegroundColor Yellow
    Write-Host "Run it again with more throws before believing it:" -ForegroundColor Yellow
    Write-Host "  .\checks\check-dice.ps1 -Dice $Dice -Throws $($Throws * 4)" -ForegroundColor DarkGray
    exit 0
}

if ($text -match 'probe\s+\S+ in the .* tray:') { Pass "the physics $Dice is not biased" }

Write-Host 'the probe never reached a verdict. The tray or the scene is broken:' -ForegroundColor Red
$text -split "`n" | Select-Object -Last 20 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
Fail
