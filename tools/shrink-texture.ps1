<#
.SYNOPSIS
    Brings a texture down to a most-pixels-a-side, in place, keeping its shape and its alpha.

.DESCRIPTION
    ART_DIRECTION section 9: "more is not better" - a prop is a few dozen pixels on screen, and the Quaternius
    megakits ship 2K and 4K base colours (the Stylized Nature kit's bark is 4 MB a picture). tools/pull_props.py
    calls this on every picture it pulls that is bigger than -Max (cc_task_l Part 3), so what lands in game/ is the
    size the table can use. The untouched original is still in the zip in assets/.

    A picture already within -Max is left alone, byte for byte. PNG stays PNG (alpha kept, for leaves); JPEG stays
    JPEG, at quality 90.

.EXAMPLE
    .\tools\shrink-texture.ps1 -Path game\models\props\stylized_nature_megakit_standard\Bark_NormalTree.png
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string[]] $Path,
    [int] $Max = 1024
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

foreach ($file in $Path) {
    $full = (Resolve-Path $file).Path
    $source = [System.Drawing.Image]::FromFile($full)

    try {
        $largest = [Math]::Max($source.Width, $source.Height)

        if ($largest -le $Max) {
            Write-Host ("  {0,-40} {1}x{2}, kept" -f (Split-Path $full -Leaf), $source.Width, $source.Height)
            continue
        }

        $scale = $Max / $largest
        $width = [int][Math]::Round($source.Width * $scale)
        $height = [int][Math]::Round($source.Height * $scale)

        $smaller = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $paint = [System.Drawing.Graphics]::FromImage($smaller)
        $paint.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $paint.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $paint.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $paint.DrawImage($source, 0, 0, $width, $height)
        $paint.Dispose()
    }
    finally {
        $source.Dispose()
    }

    if ($full -match '\.jpe?g$') {
        $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
        $quality = New-Object System.Drawing.Imaging.EncoderParameters 1
        $quality.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L
        $smaller.Save($full, $codec, $quality)
    }
    else {
        $smaller.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $smaller.Dispose()
    Write-Host ("  {0,-40} {1}x{2}" -f (Split-Path $full -Leaf), $width, $height)
}
