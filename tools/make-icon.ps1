# Erzeugt Resources\SmartStart.ico (mehrere Größen, 32-bit mit Alphakanal).
# Motiv: abgerundetes Quadrat mit drei Menüzeilen und einem Pfeil (Kaskadenmenü).
param([string]$Out = (Join-Path $PSScriptRoot '..\Resources\SmartStart.ico'))

Add-Type -AssemblyName System.Drawing

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    $m = [Math]::Max(0.5, $s * 0.03)
    $bg = New-RoundRect $m $m ($s - 2 * $m) ($s - 2 * $m) ($s * 0.22)
    $rect = New-Object System.Drawing.RectangleF 0, 0, $s, $s
    $c1 = [System.Drawing.Color]::FromArgb(255, 20, 184, 166)
    $c2 = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 45
    $g.FillPath($brush, $bg)

    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(170, 255, 255, 255))
    $barH = [Math]::Max(1.5, $s * 0.11)
    $left = $s * 0.20
    $gap = $s * 0.075
    $total = 3 * $barH + 2 * $gap
    $top = ($s - $total) / 2
    $widths = @(0.42, 0.42, 0.42)
    for ($i = 0; $i -lt 3; $i++) {
        $y = $top + $i * ($barH + $gap)
        $b = if ($i -eq 1) { $white } else { $soft }
        $bar = New-RoundRect $left $y ($s * $widths[$i]) $barH ([Math]::Min($barH / 2, $s * 0.05))
        $g.FillPath($b, $bar)
    }

    # Pfeil rechts neben der mittleren Zeile
    $cy = $top + $barH + $gap + $barH / 2
    $ax = $s * 0.68
    $aw = $s * 0.13
    $ah = $s * 0.30
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.2, $s * 0.085))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $pts = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF $ax, ($cy - $ah / 2)),
        (New-Object System.Drawing.PointF ($ax + $aw), $cy),
        (New-Object System.Drawing.PointF $ax, ($cy + $ah / 2)))
    $g.DrawLines($pen, $pts)
    $g.Dispose()
    return $bmp
}

function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    # BITMAPINFOHEADER (Höhe doppelt: XOR + AND)
    $bw.Write([int]40); $bw.Write([int]$s); $bw.Write([int]($s * 2))
    $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]0)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
        }
    }
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    $bw.Write((New-Object byte[] ($maskRow * $s)))
    $bw.Flush()
    return , $ms.ToArray()
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 256)
$images = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $images += , $ms.ToArray()
    } else {
        $images += , (Get-DibBytes $bmp)
    }
    $bmp.Dispose()
}

New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]$images[$i].Length); $w.Write([int]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write([byte[]]$img) }
$w.Close()
Write-Host "Icon geschrieben: $Out"

# Vorschau-PNG (256 px) für die Dokumentation
$preview = New-IconBitmap 256
$preview.Save((Join-Path (Split-Path $Out) 'SmartStart.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

