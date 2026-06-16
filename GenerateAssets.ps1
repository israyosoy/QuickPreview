#Requires -Version 5.1
<#
.SYNOPSIS
    Genera todos los assets visuales de QuickPreview (iconos PNG + ICO).
    Ejecutar una sola vez antes del primer build o al cambiar el diseño del ícono.

.USAGE
    .\GenerateAssets.ps1

    Crea:
      QuickPreview\Assets\app.ico              (exe + taskbar)
      Package\Assets\Square44x44Logo.png       (MSIX / Store)
      Package\Assets\Square150x150Logo.png
      Package\Assets\Square310x310Logo.png
      Package\Assets\Wide310x150Logo.png
      Package\Assets\StoreLogo.png
      Package\Assets\SplashScreen.png
#>

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

# ── Colores de la marca ────────────────────────────────────────────────────────
$BgColor   = [System.Drawing.Color]::FromArgb(255, 30, 100, 210)   # azul vibrante
$TextColor = [System.Drawing.Color]::White
$DarkBg    = [System.Drawing.Color]::FromArgb(255, 30,  30,  30)   # fondo oscuro app
$AccentColor = [System.Drawing.Color]::FromArgb(255, 61, 127, 193) # azul claro

# ── Helpers ────────────────────────────────────────────────────────────────────

function Draw-Icon {
    param([System.Drawing.Graphics]$g, [int]$size, [bool]$roundedCorners = $true)

    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # Fondo: cuadrado redondeado
    $radius = [int]($size * 0.22)
    $rect   = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    if ($roundedCorners -and $size -ge 44) {
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc($rect.X, $rect.Y, $radius*2, $radius*2, 180, 90)
        $path.AddArc($rect.Right - $radius*2, $rect.Y, $radius*2, $radius*2, 270, 90)
        $path.AddArc($rect.Right - $radius*2, $rect.Bottom - $radius*2, $radius*2, $radius*2, 0, 90)
        $path.AddArc($rect.X, $rect.Bottom - $radius*2, $radius*2, $radius*2, 90, 90)
        $path.CloseAllFigures()
        $brush = New-Object System.Drawing.SolidBrush($BgColor)
        $g.FillPath($brush, $path)
        $brush.Dispose(); $path.Dispose()
    } else {
        $brush = New-Object System.Drawing.SolidBrush($BgColor)
        $g.FillRectangle($brush, $rect)
        $brush.Dispose()
    }

    # Letra "Q"
    $fontSize = $size * 0.55
    $font     = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf       = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $brush = New-Object System.Drawing.SolidBrush($TextColor)
    $g.DrawString("Q", $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $size, $size), $sf)
    $font.Dispose(); $brush.Dispose(); $sf.Dispose()
}

function Draw-WideTile {
    param([System.Drawing.Graphics]$g, [int]$w, [int]$h)

    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear($DarkBg)

    # Pequeño ícono Q a la izquierda
    $iconSize = [int]($h * 0.6)
    $iconX    = [int]($h * 0.2)
    $iconY    = [int](($h - $iconSize) / 2)
    $iconBmp  = New-Object System.Drawing.Bitmap($iconSize, $iconSize)
    $iconG    = [System.Drawing.Graphics]::FromImage($iconBmp)
    Draw-Icon $iconG $iconSize $true
    $iconG.Dispose()
    $g.DrawImage($iconBmp, $iconX, $iconY, $iconSize, $iconSize)
    $iconBmp.Dispose()

    # Texto "QuickPreview"
    $textX    = $iconX + $iconSize + [int]($h * 0.15)
    $fontSize = [int]($h * 0.26)
    $font     = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $brush    = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 220, 220, 220))
    $sf       = New-Object System.Drawing.StringFormat
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textRect = [System.Drawing.RectangleF]::new($textX, 0, $w - $textX - 20, $h)
    $g.DrawString("QuickPreview", $font, $brush, $textRect, $sf)
    $font.Dispose(); $brush.Dispose(); $sf.Dispose()
}

function Draw-Splash {
    param([System.Drawing.Graphics]$g, [int]$w, [int]$h)

    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear($DarkBg)

    # Ícono Q centrado
    $iconSize = [int]($h * 0.45)
    $iconX    = [int](($w - $iconSize) / 2)
    $iconY    = [int](($h - $iconSize) / 2) - [int]($h * 0.06)
    $iconBmp  = New-Object System.Drawing.Bitmap($iconSize, $iconSize)
    $iconG    = [System.Drawing.Graphics]::FromImage($iconBmp)
    Draw-Icon $iconG $iconSize $true
    $iconG.Dispose()
    $g.DrawImage($iconBmp, $iconX, $iconY, $iconSize, $iconSize)
    $iconBmp.Dispose()

    # Tagline debajo
    $fontSize = [int]($h * 0.09)
    $font  = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 120, 120, 120))
    $sf    = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $textY = $iconY + $iconSize + [int]($h * 0.06)
    $textRect = [System.Drawing.RectangleF]::new(0, $textY, $w, $h - $textY)
    $g.DrawString("QuickPreview", $font, $brush, $textRect, $sf)
    $font.Dispose(); $brush.Dispose(); $sf.Dispose()
}

function Save-Png {
    param([System.Drawing.Bitmap]$bmp, [string]$relativePath)
    $fullPath = Join-Path $ProjectRoot $relativePath
    $dir = Split-Path $fullPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $bmp.Save($fullPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "  OK  $relativePath"
}

function Save-Ico {
    param([System.Drawing.Bitmap]$bmp256, [string]$relativePath)
    $path = Join-Path $ProjectRoot $relativePath
    $dir = Split-Path $path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

    # Build a proper multi-size ICO (16, 32, 48, 256) by writing the ICO binary format
    $sizes = @(16, 32, 48, 256)
    $pngData = @{}
    foreach ($s in $sizes) {
        $scaled = New-Object System.Drawing.Bitmap($s, $s)
        $sg = [System.Drawing.Graphics]::FromImage($scaled)
        $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $sg.DrawImage($bmp256, 0, 0, $s, $s)
        $sg.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $scaled.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData[$s] = $ms.ToArray()
        $scaled.Dispose(); $ms.Dispose()
    }

    # ICO header: ICONDIR
    $stream = [System.IO.File]::OpenWrite($path)
    $w = New-Object System.IO.BinaryWriter($stream)
    $w.Write([uint16]0)          # reserved
    $w.Write([uint16]1)          # type: ICO
    $w.Write([uint16]$sizes.Count)

    # Calculate offsets (6 header + 16 * count entries, then image data)
    $dataOffset = 6 + 16 * $sizes.Count
    foreach ($s in $sizes) {
        $d = $pngData[$s]
        $dim = if ($s -eq 256) { 0 } else { $s }  # 0 means 256 in ICO format
        $w.Write([byte]$dim)      # width
        $w.Write([byte]$dim)      # height
        $w.Write([byte]0)         # color count
        $w.Write([byte]0)         # reserved
        $w.Write([uint16]1)       # planes
        $w.Write([uint16]32)      # bit count
        $w.Write([uint32]$d.Length)
        $w.Write([uint32]$dataOffset)
        $dataOffset += $d.Length
    }
    foreach ($s in $sizes) { $w.Write($pngData[$s]) }
    $w.Dispose(); $stream.Dispose()
    Write-Host "  OK  $relativePath"
}

# ── Generar assets ─────────────────────────────────────────────────────────────

Write-Host "`nGenerando assets de QuickPreview...`n"

# Square icons
foreach ($size in @(44, 50, 150, 256, 310)) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    Draw-Icon $g $size $true
    $g.Dispose()

    switch ($size) {
        44  { Save-Png $bmp "Package\Assets\Square44x44Logo.png" }
        50  { Save-Png $bmp "Package\Assets\StoreLogo.png" }
        150 { Save-Png $bmp "Package\Assets\Square150x150Logo.png" }
        256 {
            Save-Ico $bmp "QuickPreview\Assets\app.ico"
            Save-Png $bmp "Package\Assets\Square256x256Logo.png"
        }
        310 { Save-Png $bmp "Package\Assets\Square310x310Logo.png" }
    }
    $bmp.Dispose()
}

# Wide tile 310×150
$wideBmp = New-Object System.Drawing.Bitmap(310, 150)
$wideG   = [System.Drawing.Graphics]::FromImage($wideBmp)
Draw-WideTile $wideG 310 150
$wideG.Dispose()
Save-Png $wideBmp "Package\Assets\Wide310x150Logo.png"
$wideBmp.Dispose()

# Splash 620×300
$splashBmp = New-Object System.Drawing.Bitmap(620, 300)
$splashG   = [System.Drawing.Graphics]::FromImage($splashBmp)
Draw-Splash $splashG 620 300
$splashG.Dispose()
Save-Png $splashBmp "Package\Assets\SplashScreen.png"
$splashBmp.Dispose()

Write-Host "`n✓ Assets generados correctamente."
Write-Host "  Ahora puedes hacer 'dotnet build' — el ícono estará embebido en el .exe`n"
