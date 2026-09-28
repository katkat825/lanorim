# Is anything written twice?
#
# tools/find_duplicate_code.py --check, over the game (core, content, game, sim) and the tests as
# two pools. It fails on a method identical to another once local names are ignored, and on a
# one-line (=>) member whose body another file also has - unless the pair is on the script's
# ALLOWED list, which says in one line why the two stay two. Groups of methods 80% alike are
# listed and never fail. (cc_task_dedupe-methods.md, part D.)
#
# Needs Python 3 on the path (python, or the py launcher). Nothing to build: it reads the source.

. (Join-Path $PSScriptRoot '_common.ps1')

$python = Get-Command python -ErrorAction SilentlyContinue
if ($null -eq $python) { $python = Get-Command py -ErrorAction SilentlyContinue }
if ($null -eq $python) { Fail 'no python on the path - install Python 3' }

& $python.Source tools/find_duplicate_code.py --check
$found = $LASTEXITCODE

Write-Host ''

if ($found -eq 0) { Pass 'no duplicate methods outside the allow-list' }

Fail
