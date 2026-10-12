# Does the game have every bark Kathleen has written? (cc_task_i Part 2)
#
# tools/import_barks.py reads her vault (../campaign_authoring/Bark Pools, never written to) and writes the game's
# bark banks (content/srd/barks) and their English (game/locale/game.csv). This runs it with --check: it writes
# nothing, prints the pool sizes against DIALOG_POOLS.md's minimums and anything it skipped, and fails when the
# game is behind the vault. A machine without the vault passes with a note. To bring the game up to date:
#
#   python tools/import_barks.py

. (Join-Path $PSScriptRoot '_common.ps1')

Write-Host ''
python (Join-Path $PSScriptRoot '..\tools\import_barks.py') --check
$checked = $LASTEXITCODE

Write-Host ''
Write-Host 'the banks, as the game reads them:' -ForegroundColor DarkGray
dotnet test content.tests/content.tests.csproj --nologo -v q --filter 'FullyQualifiedName~BarkTests'
$tested = $LASTEXITCODE

Write-Host ''

if ($checked -eq 0 -and $tested -eq 0) { Pass 'the game has every bark the vault has, and reads them' }

Fail 'run python tools/import_barks.py, then godot --headless --path game --import'
