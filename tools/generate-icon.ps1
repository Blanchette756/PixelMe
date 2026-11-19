param(
    [string]$OutPath = (Join-Path $PSScriptRoot "..\src\PixelMe\Assets\PixelMe.ico")
)

Add-Type -AssemblyName System.Drawing

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [single]($r * 2)
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [single]$size
    $tile = New-RoundedPath 0 0 $s $s ([single]($s * 0.23))
    $rect = New-Object System.Drawing.RectangleF 0, 0, $s, $s
    $top = [System.Drawing.Color]::FromArgb(255, 124, 58, 237)
    $bottom = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $top, $bottom, 50
    $g.FillPath($fill, $tile)

    $pillW = [single]($s * 0.68)
    $pillH = [single]([Math]::Max(3, $s * 0.24))
    $pillX = [single](($s - $pillW) / 2)
    $pillY = [single]($s * 0.36 - $pillH / 2)
    $pill = New-RoundedPath $pillX $pillY $pillW $pillH ([single]($pillH / 2))
    $pillBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 10, 10, 14))
    $g.FillPath($pillBrush, $pill)

    $dot = [single]([Math]::Max(2, $pillH * 0.42))
    $dotX = [single]($pillX + $pillW - $pillH / 2 - $dot / 2)
    $dotY = [single]($pillY + ($pillH - $dot) / 2)
    $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 52, 211, 153))
    $g.FillEllipse($dotBrush, $dotX, $dotY, $dot, $dot)

    if ($size -ge 32) {
        $lineBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 255, 255, 255))
        $lineH = [single]($s * 0.07)
        $g.FillPath($lineBrush, (New-RoundedPath ([single]($s * 0.22)) ([single]($s * 0.62)) ([single]($s * 0.56)) $lineH ([single]($lineH / 2))))
        $faint = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(90, 255, 255, 255))
        $g.FillPath($faint, (New-RoundedPath ([single]($s * 0.30)) ([single]($s * 0.75)) ([single]($s * 0.40)) $lineH ([single]($lineH / 2))))
    }

    $g.Dispose()
    return $bmp
}

function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $size = $bmp.Width
    $data = $bmp.LockBits((New-Object System.Drawing.Rectangle 0, 0, $size, $size), [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $pixels = New-Object byte[] ($stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $w.Write([uint32]40)
    $w.Write([int32]$size)
    $w.Write([int32]($size * 2))
    $w.Write([uint16]1)
    $w.Write([uint16]32)
    $w.Write([uint32]0)
    $w.Write([uint32]($size * $size * 4 + $maskStride * $size))
    $w.Write([int32]0)
    $w.Write([int32]0)
    $w.Write([uint32]0)
    $w.Write([uint32]0)
    for ($row = $size - 1; $row -ge 0; $row--) {
        $w.Write($pixels, $row * $stride, $size * 4)
    }
    $w.Write((New-Object byte[] ($maskStride * $size)))
    $w.Flush()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = @()
foreach ($size in $sizes) {
    $frame = New-Frame $size
    $images += , (Get-DibBytes $frame)
    $frame.Dispose()
}

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$images[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath $dir).Path + "\" + (Split-Path -Leaf $OutPath), $out.ToArray())
Write-Output "Icon written to $OutPath"
