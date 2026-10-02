# Does the map builder work as an author would use it, and does what it saves load in a campaign?
#
# The builder is Godot (the table's board, the mouse, the panels), so it can only be checked inside the engine.
# The probe (game/Builder/MapBuilderProbe.cs) opens a new map in a throwaway campaign, presses the tool buttons,
# clicks and drags on the board, paints, lays a wall run, places a prop, sets the start and a spawn, undoes once
# and saves, then reads the file back and loads it through the campaign (Package.Maps).

param([string] $Godot)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

$log = Temp-Log 'mapbuilder'

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--', '--map-probe') $log

$said = Get-Content $log -Raw
$said -split "`n" | Where-Object { $_ -match '^mapprobe' } | ForEach-Object { Write-Host "  $_" }

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($said -match 'mapprobe passed') { Pass 'painted, walled, propped, started, spawned, undone and saved; the file reads back and its campaign loads it' }

Fail
