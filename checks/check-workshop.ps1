# Does the Workshop make a campaign an author can play? (cc_task_m, "Tests and checks")
#
# The probe (game/Dev/WorkshopProbe.cs) works on a throwaway user:// (user://workshop_probe: a My campaigns and a My maps
# of its own, never the player's) and drives the screens through their own fields and buttons:
#   - the hub: New campaign first, Subscribed empty, the game's own campaigns (the editor build), My maps;
#   - the wizard's three pages: a name, a blurb, a tag, mature; the rules and a GM screen; a first chapter on a new map
#     made from a template, which Finish opens in the builder;
#   - back from the builder to the campaign's page: a second chapter, one of My maps copied onto it, Ctrl+Z and Ctrl+Y,
#     a character; Save, and it loads with no errors;
#   - the table editors (cc_task_n): a fight whose meter goes amber with a second monster, a random table with a fight and
#     a moment (Roll it), a loot table with all three coins and an item (Roll it), a shop and an armory (its picker greys a
#     potion), a lucky roll;
#   - the story editor (cc_task_o, cc_task_p): a note with two options to two new cards, one ending; a check whose
#     two-keys band "Add another way" clears, its pass remembering "picked the lock", a fight with They won and They ran,
#     a loot search with "if they found coins"; an ending with "Only if they remember that..."; saved, compiled, and played
#     headless to both endings (Try every choice);
#   - play-testing (cc_task_q): Play from here on a card with a preset and rolls Always fail, back to the editor on the
#     card and line that played; again with Always pass, the fight skipped as a win ($fight won, the loot dropped); Run it
#     for me from the page and on the sample (every card reached); Try this fight, played, and back on the fight's card;
#   - its card on the hub and its line in the book's contents;
#   - played from the book to the end (--auto).

param([string] $Godot)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

Build-First game/Lanorim.csproj

$log = Temp-Log 'workshop'

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--', '--workshop-probe', '--auto') $log

$said = Get-Content $log -Raw
$said -split "`n" | Where-Object { $_ -match '^(workshop|play  |test  )' } | ForEach-Object { Write-Host "  $_" }

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($said -match 'workshop passed' -and $said -match 'play    the end') {
    Pass 'a campaign made through the wizard, its tables, story and tests made and run through the Workshop, saved with no errors, in the book, and played to the end'
}

Fail
