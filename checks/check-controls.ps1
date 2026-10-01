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

# THE FIVE KEYS THAT DID NOTHING (cc_task_open-questions-answers.md 4.2): F2, F1, Tab, Shift+Tab and F3 pressed in a
# real fight, through Godot's own input, and what each said or showed (Game.Access.AccessDesk.Probe)
Write-Host ''
Write-Host 'Tab, Shift+Tab, F1, F2 and F3 at the table:' -ForegroundColor DarkGray

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://launch.tscn', '--',
                      '--begin', 'sample_millbrook', 'fighter', '--start', 'cellar', '--autodice', '--autostory',
                      '--access-probe') $log

$keys = Get-Content $log -Raw
$keys -split "`n" | Where-Object { $_ -match '^access' } | ForEach-Object { Write-Host "  $_" }

Remove-Item $log -ErrorAction SilentlyContinue

Write-Host ''

if ($said -match 'controls ok' -and $keys -match 'access\s+check passed') {
    Pass 'every act has a row, every row names a key, Change rebinds, and Tab, F1, F2 and F3 answer'
}

Fail
