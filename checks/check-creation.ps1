# Does creation keep its place, say what a choice is without hover text, and split cantrips from spells?
#
# Creation is Godot controls, so it can only be checked inside the engine. The probe
# (game/Screens/CreationProbe.cs) presses its buttons and boxes the way a player does.

param([string] $Godot)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

$log = Temp-Log 'creation'

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--', '--creation') $log

$said = Get-Content $log -Raw
$said -split "`n" | Where-Object { $_ -match '^creation' } | ForEach-Object { Write-Host "  $_" }

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($said -match 'creation ok') { Pass 'no hover text, a description under every list, cantrips and spells apart, and the scroll kept' }

Fail
