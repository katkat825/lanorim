# Does the Workshop make a campaign an author can play? (cc_task_m, "Tests and checks")
#
# The probe (game/Dev/WorkshopProbe.cs) works on a throwaway user:// (user://workshop_probe: a My campaigns and a My maps
# of its own, never the player's) and drives the screens through their own fields and buttons:
#   - the hub: New campaign first, Subscribed empty, the game's own campaigns (the editor build), My maps;
#   - the wizard's three pages: a name, a blurb, a tag, mature; the rules and a GM screen; a first chapter on a new map
#     made from a template, which Finish opens in the builder;
#   - back from the builder to the campaign's page: a second chapter, one of My maps copied onto it, Ctrl+Z and Ctrl+Y,
#     a character; Save, and it loads with no errors;
#   - its card on the hub and its line in the book's contents;
#   - played from the book: its one line, then the end (--auto).

param([string] $Godot)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

$log = Temp-Log 'workshop'

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--', '--workshop-probe', '--auto') $log

$said = Get-Content $log -Raw
$said -split "`n" | Where-Object { $_ -match '^(workshop|play  )' } | ForEach-Object { Write-Host "  $_" }

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($said -match 'workshop passed' -and $said -match 'play    the end') {
    Pass 'a campaign made through the wizard, a chapter and a map added, saved with no errors, in the book, and played to its one line'
}

Fail
