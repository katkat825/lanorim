# Does the table actually come up, and does the whole stack meet on it?
#
# The engine tests prove the rules. This proves the thing the rules are wired to: Godot loads the
# assemblies, reads the SRD data, imports the locale, builds a hero, throws a real d20 in a real
# tray, and core reads the face off the felt.
#
# It also runs the LOCALE PROBE, which asks a question check-locale.ps1 cannot: Godot reads the
# compiled .translation beside the CSV, so a key added and not re-imported is missing at runtime
# while every test in the repo passes. That trap has caught this project before.

param([string] $Godot)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

. (Join-Path $PSScriptRoot 'Find-Godot.ps1')
$Godot = Find-Godot $Godot

# GODOT WRITES WARNINGS TO STDERR AT EXIT ("N ObjectDB instances leaked"), AND POWERSHELL TURNS
# THAT INTO A TERMINATING ERROR under $ErrorActionPreference = 'Stop' - so the script dies before
# it reaches its own verdict and reports nothing at all. Every Godot call here goes through this.
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

$log = Join-Path ([System.IO.Path]::GetTempPath()) "lanorim-table-$PID.txt"

Write-Host 'importing...' -ForegroundColor DarkGray
Invoke-Godot $Godot @('--headless', '--path', 'game', '--import') $log

# --- the words ---------------------------------------------------------------------------------

Write-Host ''
Write-Host 'the locale, as Godot sees it:' -ForegroundColor DarkGray

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://table.tscn', '--', '--locale') $log

$locale = Get-Content $log -Raw

$locale -split "`n" | Where-Object { $_ -match '^locale ' } | ForEach-Object { Write-Host "  $_" }

$wordsOk = $locale -match 'every key the game emits has words in it'

# --- the table ---------------------------------------------------------------------------------

Write-Host ''
Write-Host 'the table, booted and thrown once:' -ForegroundColor DarkGray

Invoke-Godot $Godot @('--headless', '--path', 'game', 'res://table.tscn', '--quit-after', '400') $log

$table = Get-Content $log -Raw

$table -split "`n" |
    Where-Object { $_ -match '^(tray|table) ' } |
    ForEach-Object { Write-Host "  $_" }

Remove-Item $log -ErrorAction SilentlyContinue

# a throw that resolved: the die settled AND core read it
$threw = $table -match 'tray\s+Die1 d20 \d+' -and $table -match 'table\s+check: d20'

# a raw key on screen means the .translation is stale
$stale = $table -match 'LOCALE STALE' -or $table -match 'ability\.str\.name'

Write-Host ''

if ($wordsOk -and $threw -and -not $stale) {
    Write-Host 'check passed - the table comes up, the dice roll, and core reads the felt' -ForegroundColor Green
    exit 0
}

if (-not $wordsOk) {
    Write-Host 'check FAILED - keys with no words in them. If the csv has them, re-import:' -ForegroundColor Red
    Write-Host '  godot --headless --path game --import' -ForegroundColor DarkGray
}

if (-not $threw) { Write-Host 'check FAILED - no d20 reached the felt' -ForegroundColor Red }

if ($stale) { Write-Host 'check FAILED - the .translation beside the csv is stale' -ForegroundColor Red }

exit 1
