# Is the README's list of flags the dev registry's, and does the registry list every flag the game reads?
#
# tools/dev_flags.py --check: the flags are written down once, in game/Dev/DevHooks.cs (DevHooks.Flags), and the
# README's list is made from it (python tools/dev_flags.py). It fails when the README is behind the registry, when
# game/DevArgs.cs reads a flag the registry doesn't list, or when the registry lists one nothing reads
# (cc_task_j-split-classes-dev-hooks.md Part 2).
#
# Needs Python 3 on the path (python, or the py launcher). Nothing to build: it reads the source.

. (Join-Path $PSScriptRoot '_common.ps1')

$python = Get-Command python -ErrorAction SilentlyContinue
if ($null -eq $python) { $python = Get-Command py -ErrorAction SilentlyContinue }
if ($null -eq $python) { Fail 'no python on the path - install Python 3' }

& $python.Source tools/dev_flags.py --check
$found = $LASTEXITCODE

Write-Host ''

if ($found -eq 0) { Pass 'the README lists every flag the registry does, and the registry every flag the game reads' }

Fail 'run python tools/dev_flags.py'
