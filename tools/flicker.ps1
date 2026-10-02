<#
.SYNOPSIS
    How much a still picture flickers: the mean absolute change per pixel from one frame to the next.

        .\tools\flicker.ps1 -Frames C:\shots\before\f_%03d.png -Map C:\shots\before_map.png `
                            -Regions 'posts=600:200:80:40','floor=300:400:200:100'

.DESCRIPTION
    cc_task_f 1.7: the dungeon posts' tops z-fought with the walls' and flickered with the camera's handheld drift.
    Feed it a run of frames of one view (the table's `--shot <png> --frames 90`, or frames cut from a capture),
    and it prints, on a 0-255 grey scale, the mean change between neighbouring frames over the whole picture and
    over each named region (x:y:w:h in pixels), and with -Map writes a picture of where it changed (brighter is
    more, scaled up 8 times so a flicker shows). Needs ffmpeg on the PATH. A development tool; the game never runs it.
#>
param(
    [Parameter(Mandatory)] [string] $Frames,
    [string] $Map,
    [string[]] $Regions = @()
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw 'ffmpeg is not on the PATH' }

function Mean-Change([string] $crop) {
    $log = [System.IO.Path]::GetTempFileName()
    $filter = "$crop" + 'format=gray,tblend=all_mode=difference,signalstats,' +
              "metadata=print:key=lavfi.signalstats.YAVG:file='" + ($log -replace '\\', '/' -replace ':', '\:') + "'"

    & ffmpeg -hide_banner -loglevel error -framerate 30 -i $Frames -vf $filter -f null - | Out-Null

    $values = Get-Content $log | Where-Object { $_ -match 'YAVG=([0-9.]+)' } | ForEach-Object { [double]$Matches[1] }
    Remove-Item $log

    # tblend's first frame is the first picture against nothing; it isn't a change
    $values = @($values | Select-Object -Skip 1)
    if ($values.Count -eq 0) { return [double]::NaN }

    return ($values | Measure-Object -Average).Average
}

'{0,-12} {1,8:N3}' -f 'whole', (Mean-Change '')

foreach ($region in $Regions) {
    $name, $box = $region -split '=', 2
    $x, $y, $w, $h = $box -split ':'
    '{0,-12} {1,8:N3}' -f $name, (Mean-Change "crop=${w}:${h}:${x}:${y},")
}

if ($Map) {
    $count = @(Get-ChildItem (Split-Path $Frames) -Filter '*.png').Count
    & ffmpeg -hide_banner -loglevel error -y -framerate 30 -i $Frames `
             -vf "format=gray,tblend=all_mode=difference,tmix=frames=$([Math]::Min(1000, [Math]::Max(2, $count - 1))),lutyuv=y=val*8" `
             -update 1 $Map | Out-Null
    "map: $Map"
}
