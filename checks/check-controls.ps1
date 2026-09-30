# Does the Controls section list every act, name a key on every row, and rebind the player's way?
#
# The section reads the InputMap, so it can only be checked inside the engine. The probe
# (game/Screens/ControlsProbe.cs) plays apart from the player's own settings file.

param([string] $Godot)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

$log = Temp-Log 'controls'

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--', '--controls') $log

$said = Get-Content $log -Raw
$said -split "`n" | Where-Object { $_ -match '^controls' } | ForEach-Object { Write-Host "  $_" }

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($said -match 'controls ok') { Pass 'every act has a row, every row names a key, and Change rebinds' }

Fail
