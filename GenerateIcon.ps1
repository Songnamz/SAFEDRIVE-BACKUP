Add-Type -AssemblyName System.Drawing

function Draw-Shield {
    param([int]$size)
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $sf = $size / 32.0

    $pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(16*$sf,  2*$sf),
        [System.Drawing.PointF]::new(29*$sf,  7*$sf),
        [System.Drawing.PointF]::new(29*$sf, 17*$sf),
        [System.Drawing.PointF]::new(16*$sf, 30*$sf),
        [System.Drawing.PointF]::new( 3*$sf, 17*$sf),
        [System.Drawing.PointF]::new( 3*$sf,  7*$sf)
    )
    $shield = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shield.AddPolygon($pts)

    $fill    = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 120, 212))
    $outline = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 255, 255, 255), [float]($sf))
    $g.FillPath($fill, $shield)
    $g.DrawPath($outline, $shield)

    $pw = [float]([Math]::Max(1.0, 2.5 * $sf))
    $checkPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $pw)
    $checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $checkPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $ck = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(10*$sf, 16*$sf),
        [System.Drawing.PointF]::new(14*$sf, 21*$sf),
        [System.Drawing.PointF]::new(22*$sf, 11*$sf)
    )
    $g.DrawLines($checkPen, $ck)

    $g.Dispose(); $fill.Dispose(); $outline.Dispose(); $checkPen.Dispose()
    return $bmp
}

function Save-Ico {
    param([string]$outPath, [int[]]$sizes)

    $pngs = foreach ($sz in $sizes) {
        $bmp = Draw-Shield $sz
        $ms  = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $ms
    }

    $fs     = [System.IO.File]::Open($outPath, [System.IO.FileMode]::Create)
    $writer = New-Object System.IO.BinaryWriter($fs)

    # ICO header
    $writer.Write([uint16]0)             # reserved
    $writer.Write([uint16]1)             # type = ICO
    $writer.Write([uint16]$sizes.Count)  # image count

    # Directory offset starts after header (6) + all dir entries (16 each)
    $offset = 6 + 16 * $sizes.Count

    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz  = $sizes[$i]
        $len = [int]$pngs[$i].Length
        $w   = if ($sz -ge 256) { 0 } else { $sz }
        $h   = if ($sz -ge 256) { 0 } else { $sz }
        $writer.Write([byte]$w)
        $writer.Write([byte]$h)
        $writer.Write([byte]0)      # color count
        $writer.Write([byte]0)      # reserved
        $writer.Write([uint16]1)    # planes
        $writer.Write([uint16]32)   # bit depth
        $writer.Write([uint32]$len)
        $writer.Write([uint32]$offset)
        $offset += $len
    }

    foreach ($ms in $pngs) {
        $writer.Write($ms.ToArray())
        $ms.Dispose()
    }

    $writer.Close(); $fs.Close()
}

$out = Join-Path $PSScriptRoot "SafeDriveBackup.App\SafeDrive.ico"
Save-Ico $out @(16, 24, 32, 48, 64, 128, 256)
Write-Host "Icon saved to: $out"
