# Does every screen fit every window? (cc_task_ui-issues-9-30.md part 2)
#
# The launch screens (title, book, tutorials, settings, creation's pages), then the table's HUD at the
# hero's first turn with the pause menu, the sheet and the pack, each laid out at 1280x720, 1366x768,
# 1920x1080, 2560x1440, 3440x1440 and 1024x768. It fails on a control past the edge of the screen, a
# wrapping label squeezed to a sliver, two HUD panels overlapping, or the log or the strip over the
# board (game/Screens/LayoutCheck.cs, LayoutProbe.cs). Headless: Godot lays out without drawing.

param([string] $Godot)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

$ok = $true

foreach ($run in @(
    @('--layout'),
    @('--begin', 'sample_millbrook', 'fighter', '--start', 'cellar', '--autodice', '--autostory', '--layout'),
    @('--begin', 'sample_millbrook', 'paladin', '--start', 'cellar', '--autodice', '--autostory', '--layout'))) {

    $log = Temp-Log 'layout'

    Invoke-Godot $Godot (@('--headless', '--path', 'game', 'res://launch.tscn', '--') + $run) $log

    $said = Get-Content $log -Raw
    $said -split "`n" | Where-Object { $_ -match '^layout' -and $_ -notmatch ': fits' } | ForEach-Object { Write-Host "  $_" }

    if ($said -notmatch 'layout ok') { $ok = $false }

    Remove-Item $log -ErrorAction SilentlyContinue
}

Write-Host ''

if ($ok) { Pass 'every screen fits every window: nothing off the screen, nothing squeezed, no HUD overlaps, the board kept clear' }

Fail
