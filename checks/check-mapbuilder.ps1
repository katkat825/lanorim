# Does the map builder work as an author would use it, does what it saves load in a campaign, and does a map play?
#
# The builder is Godot (the table's board, the mouse, the panels), so it can only be checked inside the engine.
# The probe (game/Dev/MapBuilderProbe.cs) starts at the book's own Map builder page, in a library and a campaign of its
# own (user://map_probe_<pid>, never the player's), and drives the builder through its buttons, mouse and keys:
#   - a map in My maps from each of the five templates, saved;
#   - a dungeon room: rough ground dragged, grass filled up to the walls, a run of fence, a barrel, a two-by-two
#     cottage, a Shift-drag scatter of pines, the start, two spawns and one undo, saved;
#   - reopened from the page's My maps list and found identical; copied to the campaign (twice: the second asks, and
#     Rename copies it as _2); the campaign loads it with its cottage and its grass;
#   - Play this map: a level 3 fighter against a goblin, played to the end (--auto), and back in the builder with the
#     map as it was.
# A fight with the tray's real throws takes a minute or two.

param([string] $Godot)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

$log = Temp-Log 'mapbuilder'

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--', '--map-probe', '--auto') $log

$said = Get-Content $log -Raw
$said -split "`n" | Where-Object { $_ -match '^(mapprobe|trial)' } | ForEach-Object { Write-Host "  $_" }

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($said -match 'mapprobe passed') {
    Pass 'five templates made in My maps; ground, fill, fence, a building and a scatter of trees; undo, save, reopen, copy to a campaign (asked when taken), and played to the end and back in the builder'
}

Fail
