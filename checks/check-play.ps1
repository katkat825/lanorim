# The whole game, played headless on the real table: the launch screen makes a character, the table
# plays the sample test campaign (campaigns/sample_millbrook) from its first line to its last - the
# story, the checks, a shop, the fights on the board, the level-ups, the end - with the hero's dice
# thrown on the physical tray. Nobody touches it: -- --auto takes the first choice, lets the
# AutoPlayer fight, and throws as soon as the tray asks.
#
# What it proves is that the pieces are joined: launch -> table -> CampaignRun -> CombatSession ->
# tray -> back. What it cannot prove is how any of it looks. That is yours, in Godot.
#
#   .\checks\check-play.ps1              a fighter
#   .\checks\check-play.ps1 -Class mage  another class

param([string] $Godot, [string] $Class = 'fighter')

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

$log = Temp-Log 'play'

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

$errors = @($played -split "`n" | Where-Object { $_ -match '^(ERROR|SCRIPT ERROR)' -or $_ -match 'FAILED' })

# PRONE AND STANDING ON THE BOARD (cc_task_working-notes-10-01.md 1.2): the hero knocked Prone in a real fight
# lies inside its square, and Stand Up, taken the way the More Actions menu takes it, stands the mini up
Write-Host ''
Write-Host 'prone, then Stand Up, on the board:' -ForegroundColor DarkGray

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--',
                      '--begin', 'sample_millbrook', $Class, '--start', 'cellar', '--autodice', '--autostory',
                      '--prone-probe') $log

$prone = Get-Content $log -Raw

$prone -split "`n" | Where-Object { $_ -match '^prone ' } | ForEach-Object { Write-Host "  $_" }

$errors += @($prone -split "`n" | Where-Object { $_ -match '^prone\s+FAILED' })

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($played -match 'play\s+the end' -and $thrown -gt 0 -and $prone -match 'prone\s+check passed' -and $errors.Count -eq 0) {
    Pass 'launch to table to the end of the campaign, dice on the tray; Prone lies down and Stand Up stands up'
}

foreach ($line in $errors) { Write-Host "  $line" -ForegroundColor Red }
Fail
