# Does the game export, and does the exported build come up?
#
# Exports a preset from game/export_presets.cfg headless (Windows Release by default; -Dev for Windows Dev, which also
# puts the sample campaign beside the exe), launches the exe headless and checks it reaches the campaign book, and
# prints the build's size. The exported game finds campaigns in a `campaigns` folder beside its exe
# (game/Campaigns/CampaignFolders.cs), never inside the PCK, so a release build ships with none until a real one
# is put there.
#
# Needs Godot's export templates for the exact version, .NET flavour (4.7.1.stable.mono): in the Godot editor,
# Editor > Manage Export Templates > Download and Install. This script never fetches them.

param([string] $Godot, [switch] $Dev)

. (Join-Path $PSScriptRoot '_common.ps1')

$Godot = Find-Godot $Godot

$version = '4.7.1.stable.mono'
$templates = Join-Path $env:APPDATA "Godot\export_templates\$version"

if (-not (Test-Path (Join-Path $templates 'windows_release_x86_64.exe'))) {
    Write-Host "Godot's export templates for $version aren't installed (looked in $templates)." -ForegroundColor Yellow
    Write-Host "Install them from the editor: Editor > Manage Export Templates > Download and Install, for Godot 4.7.1 .NET." -ForegroundColor Yellow
    Fail 'no export templates'
}

Build-First game/Lanorim.csproj

$preset = if ($Dev) { 'Windows Dev' } else { 'Windows Release' }
$folder = if ($Dev) { 'build/dev' } else { 'build/release' }
$exe = Join-Path $folder 'Lanorim.exe'

if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
New-Item -ItemType Directory -Force $folder | Out-Null

# the import first, so the locale's .translation exists to be exported (it's made on import, and not committed)
Invoke-Godot $Godot @('--headless', '--path', 'game', '--import') (Temp-Log 'export-import')

$log = Temp-Log 'export'
$mode = if ($Dev) { '--export-debug' } else { '--export-release' }
Invoke-Godot $Godot @('--headless', '--path', 'game', $mode, $preset, (Resolve-Path $folder).Path + '\Lanorim.exe') $log

if (-not (Test-Path $exe)) {
    Get-Content $log | Select-Object -Last 20 | ForEach-Object { Write-Host "  $_" }
    Fail "the export made no $exe"
}

if ($Dev) {
    New-Item -ItemType Directory -Force (Join-Path $folder 'campaigns') | Out-Null
    Copy-Item campaigns/sample_millbrook (Join-Path $folder 'campaigns') -Recurse
}

# the build, run: headless, to the book, and out
$ran = Temp-Log 'export-run'
Invoke-Godot (Resolve-Path $exe).Path @('--headless', '--quit-after', '600', '--', '--show', 'book') $ran
$said = Get-Content $ran -Raw

$book = ($said -split "`n") | Where-Object { $_ -match '^book    open' } | Select-Object -First 1

$files = Get-ChildItem $folder -Recurse -File
$total = ($files | Measure-Object Length -Sum).Sum / 1MB
$pck = (Get-Item (Join-Path $folder 'Lanorim.pck') -ErrorAction SilentlyContinue).Length / 1MB

Write-Host ''
Write-Host ('  {0}: {1:N1} MB in {2} files (the PCK {3:N1} MB)' -f $preset, $total, $files.Count, $pck)
if ($book) { Write-Host "  $($book.Trim())" }

Remove-Item $log, $ran -ErrorAction SilentlyContinue

if ($book) { Pass "$preset exported and reached the book" }

Fail 'the exported build never reached the book'
