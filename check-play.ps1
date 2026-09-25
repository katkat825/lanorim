# The whole game, played headless on the real table: the launch screen makes a character, the table
# plays the sample test campaign (campaigns/sample_millbrook) from its first line to its last - the
# story, the checks, a shop, the fights on the board, the level-ups, the end - with the hero's dice
# thrown on the physical tray. Nobody touches it: -- --auto takes the first choice, lets the
# AutoPlayer fight, and throws as soon as the tray asks.
#
# What it proves is that the pieces are joined: launch -> table -> CampaignRun -> CombatSession ->
# tray -> back. What it cannot prove is how any of it looks. That is yours, in Godot.
#
#   .\check-play.ps1              a fighter
#   .\check-play.ps1 -Class mage  another class

param([string] $Godot, [string] $Class = 'fighter')

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

. (Join-Path $PSScriptRoot 'Find-Godot.ps1')
$Godot = Find-Godot $Godot

# Godot writes warnings to stderr as it exits; with ErrorActionPreference Stop that would end the
# script before the verdict is read, so the run is shielded and the verdict is read from the log
function Invoke-Godot {
    param([string] $Exe, [string[]] $Arguments, [string] $Log)

    $was = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try   { & $Exe @Arguments *> $Log }
    catch { }
    finally { $ErrorActionPreference = $was }
}

Write-Host 'building...' -ForegroundColor DarkGray
dotnet build game/Lanorim.csproj -v q --nologo | Out-Null
if (-not $?) { Write-Host 'the build failed' -ForegroundColor Red; exit 1 }

$log = Join-Path ([System.IO.Path]::GetTempPath()) "lanorim-play-$PID.txt"

Write-Host 'importing...' -ForegroundColor DarkGray
Invoke-Godot $Godot @('--headless', '--path', 'game', '--import') $log

Write-Host ''
Write-Host "the sample campaign, played by a ${Class}:" -ForegroundColor DarkGray

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--',
                      '--begin', 'sample_millbrook', $Class, '--auto') $log

$played = Get-Content $log -Raw

$played -split "`n" |
    Where-Object { $_ -match '^(launch|play) ' } |
    ForEach-Object { Write-Host "  $_" }

$thrown = ([regex]::Matches($played, '(?m)^tray\s+Die')).Count
Write-Host "  $thrown throws on the tray"

$errors = $played -split "`n" | Where-Object { $_ -match '^(ERROR|SCRIPT ERROR)' -or $_ -match 'FAILED' }

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($played -match 'play\s+the end' -and $thrown -gt 0 -and $errors.Count -eq 0) {
    Write-Host 'check passed - launch to table to the end of the campaign, dice on the tray' -ForegroundColor Green
    exit 0
}

foreach ($line in $errors) { Write-Host "  $line" -ForegroundColor Red }
Write-Host 'check FAILED' -ForegroundColor Red
exit 1
