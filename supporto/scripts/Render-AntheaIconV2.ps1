param(
    [string]$PngDestination = (Join-Path $PSScriptRoot '../../X.Desktop/Assets/anthea-icon-v2.png'),
    [string]$IcoDestination = (Join-Path $PSScriptRoot '../../X.Desktop/Assets/anthea-icon-v2.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Path([string]$data) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    switch ($data) {
        'center-petal' {
            $path.StartFigure()
            $path.AddBezier(512, 274, 454, 214, 468, 129, 512, 82)
            $path.AddBezier(512, 82, 556, 129, 570, 214, 512, 274)
            $path.CloseFigure()
        }
        'left-petal' {
            $path.StartFigure()
            $path.AddBezier(485, 283, 409, 269, 359, 209, 355, 144)
            $path.AddBezier(355, 144, 420, 153, 481, 198, 512, 261)
            $path.AddLine(512, 261, 485, 283)
            $path.CloseFigure()
        }
        'right-petal' {
            $path.StartFigure()
            $path.AddBezier(539, 283, 615, 269, 665, 209, 669, 144)
            $path.AddBezier(669, 144, 604, 153, 543, 198, 512, 261)
            $path.AddLine(512, 261, 539, 283)
            $path.CloseFigure()
        }
    }
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
        $r = 208
        $tile.AddArc(32, 32, 2 * $r, 2 * $r, 180, 90)
        $tile.AddArc(992 - 2 * $r, 32, 2 * $r, 2 * $r, 270, 90)
        $tile.AddArc(992 - 2 * $r, 992 - 2 * $r, 2 * $r, 2 * $r, 0, 90)
        $tile.AddArc(32, 992 - 2 * $r, 2 * $r, 2 * $r, 90, 90)
        $tile.CloseFigure()
        $graphics.FillPath($navy, $tile)
        $tile.Dispose()

        foreach ($name in @('left-petal', 'right-petal', 'center-petal')) {
            $petal = New-Path $name
            $graphics.FillPath($gold, $petal)
            $petal.Dispose()
        }

        $leftLeg = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(129,844), [System.Drawing.PointF]::new(416,310),
            [System.Drawing.PointF]::new(512,241), [System.Drawing.PointF]::new(512,430),
            [System.Drawing.PointF]::new(293,844)
        )
        $rightLeg = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(895,844), [System.Drawing.PointF]::new(608,310),
            [System.Drawing.PointF]::new(512,241), [System.Drawing.PointF]::new(512,430),
            [System.Drawing.PointF]::new(731,844)
        )
        $graphics.FillPolygon($ivory, $leftLeg)
        $graphics.FillPolygon($ivory, $rightLeg)

        $graphics.FillRectangle($gold, 372, 637, 280, 46)
        $graphics.FillEllipse($navy, 458, 606, 108, 108)
        $graphics.FillEllipse($gold, 467, 615, 90, 90)
    }
    finally {
        $navy.Dispose(); $ivory.Dispose(); $gold.Dispose()
    }

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
