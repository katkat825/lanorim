<#
.SYNOPSIS
    What every check script shares. Dot-source it; don't run it:

        . (Join-Path $PSScriptRoot '_common.ps1')

.DESCRIPTION
    THE ONE COPY of the lines the checks had written out five to eight times each
    (cc_task_godfiles-dupes-efficiency.md #20): stop on the first error, work from the repo root,
    build before checking, find Godot, run Godot without its exit-time warnings ending the
    script, and say the verdict the same way every time.

    Unlike the old build's check scripts, these BUILD FIRST. A check that runs stale code is
    worse than no check.
#>

$ErrorActionPreference = 'Stop'

# the checks live in checks/, and every path they use is from the repo root
Set-Location (Split-Path $PSScriptRoot -Parent)

# builds a project first, quietly, and stops the check if it doesn't build
function Build-First([string] $Project) {
    Write-Host 'building...' -ForegroundColor DarkGray
    dotnet build $Project -v q --nologo | Out-Null
    if (-not $?) { Write-Host 'the build failed' -ForegroundColor Red; exit 1 }
}

# the verdicts: the line a caller greps for is 'check passed' or 'check FAILED'
function Pass([string] $Why) {
    Write-Host "check passed - $Why" -ForegroundColor Green
    exit 0
}

function Fail([string] $Why = '') {
    Write-Host ('check FAILED' + $(if ($Why) { " - $Why" } else { '' })) -ForegroundColor Red
    exit 1
}

# --- Godot -------------------------------------------------------------------------------------

# THE ONE PLACE THE GODOT ROOT AND THE BINARY HEURISTIC ARE WRITTEN DOWN. Where it looks, in order:
#   1. the path passed in, if any            (-Godot on the calling script)
#   2. $env:GODOT                            a specific binary
#   3. $env:GODOT_ROOT, else C:\Godot        searched for the mono console binary
# Throws if it finds nothing, so every check fails the same clear way. Override the root with
# $env:GODOT_ROOT rather than editing this - a machine with Godot somewhere else shouldn't need a diff.
$script:GodotSearchRoot = if ($env:GODOT_ROOT) { $env:GODOT_ROOT } else { 'C:\Godot' }

function Find-Godot([string] $Godot) {
    if (-not $Godot) { $Godot = $env:GODOT }

    if (-not $Godot) {
        # The console binary, not the plain one: only that variant writes to stdout on Windows.
        # Newest first, by name - v4.7.1 sorts above v4.7.0.
        $Godot = Get-ChildItem $script:GodotSearchRoot -Recurse -Filter '*mono*console.exe' -ErrorAction SilentlyContinue |
                 Sort-Object FullName -Descending |
                 Select-Object -First 1 -ExpandProperty FullName
    }

    if (-not $Godot -or -not (Test-Path $Godot)) {
        throw "Godot not found$(if ($Godot) { " at '$Godot'" } else { " under '$($script:GodotSearchRoot)'" }). " +
              "Pass -Godot <path to the mono console exe>, or set `$env:GODOT to the binary " +
              "or `$env:GODOT_ROOT to the folder to search."
    }

    return $Godot
}

# GODOT WRITES WARNINGS TO STDERR AT EXIT ("N ObjectDB instances leaked"), AND POWERSHELL TURNS
# THAT INTO A TERMINATING ERROR under $ErrorActionPreference = 'Stop' - so a script would die
# before it reached its own verdict and report nothing at all. Every Godot call goes through this,
# and the verdict is read from the log, never from $LASTEXITCODE.
function Invoke-Godot([string] $Exe, [string[]] $Arguments, [string] $Log) {
    $was = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try   { & $Exe @Arguments *> $Log }
    catch { }
    finally { $ErrorActionPreference = $was }
}

# a log file of its own for one Godot run: lanorim-<what>-<pid>.txt in the temp folder
function Temp-Log([string] $What) {
    Join-Path ([System.IO.Path]::GetTempPath()) "lanorim-$What-$PID.txt"
}
