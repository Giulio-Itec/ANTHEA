param(
    [string]$PngDestination = (Join-Path $PSScriptRoot '../../X.Desktop/Assets/anthea-anthemion.png'),
    [string]$IcoDestination = (Join-Path $PSScriptRoot '../../X.Desktop/Assets/anthea-anthemion.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-LeafPath(
    [float]$startX, [float]$startY,
    [float]$control1X, [float]$control1Y,
    [float]$control2X, [float]$control2Y,
    [float]$tipX, [float]$tipY,
    [float]$return1X, [float]$return1Y,
    [float]$return2X, [float]$return2Y
) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.StartFigure()
    $path.AddBezier($startX, $startY, $control1X, $control1Y, $control2X, $control2Y, $tipX, $tipY)
    $path.AddBezier($tipX, $tipY, $return1X, $return1Y, $return2X, $return2Y, $startX, $startY)
    $path.CloseFigure()
    return $path
}

$scale = 2
$canvas = [System.Drawing.Bitmap]::new(1024 * $scale, 1024 * $scale, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.ScaleTransform($scale, $scale)
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $navy = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#0B2A4A'))
    $ivory = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F3F5F1'))
    $gold = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#C99532'))
    try {
        $tile = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $radius = 208
        $tile.AddArc(32, 32, 2 * $radius, 2 * $radius, 180, 90)
        $tile.AddArc(992 - 2 * $radius, 32, 2 * $radius, 2 * $radius, 270, 90)
        $tile.AddArc(992 - 2 * $radius, 992 - 2 * $radius, 2 * $radius, 2 * $radius, 0, 90)
        $tile.AddArc(32, 992 - 2 * $radius, 2 * $radius, 2 * $radius, 90, 90)
        $tile.CloseFigure()
        $graphics.FillPath($navy, $tile)
        $tile.Dispose()

        $leaves = @(
            @(456,704,302,678,190,529,184,366,326,409,428,525),
            @(568,704,722,678,834,529,840,366,698,409,596,525),
            @(486,704,364,594,326,370,370,222,454,338,495,514),
            @(538,704,660,594,698,370,654,222,570,338,529,514)
        )
        foreach ($leafData in $leaves) {
            $leaf = New-LeafPath @leafData
            $graphics.FillPath($ivory, $leaf)
            $leaf.Dispose()
        }

        $center = New-LeafPath 512 704 450 552 470 309 512 156 554 309 574 552
        $graphics.FillPath($gold, $center)
        $center.Dispose()

        $graphics.FillRectangle($gold, 210, 730, 604, 64)
        $graphics.FillEllipse($navy, 449, 667, 126, 126)
        $graphics.FillEllipse($gold, 458, 676, 108, 108)
        $graphics.FillRectangle($ivory, 284, 806, 456, 54)
    }
    finally { $navy.Dispose(); $ivory.Dispose(); $gold.Dispose() }

    $final = [System.Drawing.Bitmap]::new(1024, 1024, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $finalGraphics = [System.Drawing.Graphics]::FromImage($final)
    try {
        $finalGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $finalGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $finalGraphics.DrawImage($canvas, [System.Drawing.Rectangle]::new(0, 0, 1024, 1024))
        $final.Save([IO.Path]::GetFullPath($PngDestination), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $finalGraphics.Dispose(); $final.Dispose() }
}
finally { $graphics.Dispose(); $canvas.Dispose() }

& (Join-Path $PSScriptRoot 'Generate-AppIcon.ps1') -Source $PngDestination -Destination $IcoDestination
