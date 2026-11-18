param([Parameter(Mandatory)][string]$InputDocx, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WordPageGdi {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("gdi32.dll")] public static extern bool PlayEnhMetaFile(IntPtr dc, IntPtr metafile, ref Rect rectangle);
    [DllImport("gdi32.dll")] public static extern bool DeleteEnhMetaFile(IntPtr metafile);
}
'@
$source = (Resolve-Path -LiteralPath $InputDocx).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Stage([string]$message) { $message | Tee-Object -FilePath (Join-Path $output 'word.log') -Append }
$taskWord = $null
$taskDoc = $null
try {
    Stage 'Avvio istanza Word dedicata'
    $before = @(Get-Process WINWORD -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    $taskWord = New-Object -ComObject Word.Application
    $owned = @(Get-Process WINWORD | Where-Object { $_.Id -notin $before } | Select-Object -ExpandProperty Id)
    $owned | ConvertTo-Json | Set-Content (Join-Path $output 'word-process.json')
    $taskWord.Visible = $false
    $taskWord.DisplayAlerts = 0
    $taskWord.AutomationSecurity = 3
    Stage 'Apertura ordinaria in sola lettura, senza riparazione'
    $taskDoc = $taskWord.Documents.Open($source, $false, $true, $false)
    Stage 'Documento aperto'
    $taskDoc.Repaginate()
    $taskDoc.ActiveWindow.View.Type = 3
    $taskDoc.ActiveWindow.View.Zoom.Percentage = 100
    $taskDoc.ActiveWindow.ScrollIntoView($taskDoc.Range(0, 0), $true)
    $pages = $taskDoc.ActiveWindow.Panes.Item(1).Pages
    Stage ('Pagine: ' + $pages.Count)
    for ($i = 1; $i -le $pages.Count; $i++) {
        $bits = [byte[]]$pages.Item($i).EnhMetaFileBits
        $stream = [IO.MemoryStream]::new($bits, $false)
        $metafile = [Drawing.Imaging.Metafile]::new($stream)
        $width = 1240
        $height = [int][Math]::Round($width * $metafile.Height / $metafile.Width)
        $bitmap = [Drawing.Bitmap]::new($width, $height)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $graphics.Clear([Drawing.Color]::White)
        # Native GDI preserves the glyph advances emitted by Word; GDI+ DrawImage can
        # replay justified Manrope text with wrong spacing and clip the right edge.
        $emfHandle = $metafile.GetHenhmetafile()
        $dc = $graphics.GetHdc()
        $rectangle = [WordPageGdi+Rect]::new()
        $rectangle.Right = $width; $rectangle.Bottom = $height
        try { if (-not [WordPageGdi]::PlayEnhMetaFile($dc, $emfHandle, [ref]$rectangle)) { throw 'Resa GDI della pagina non riuscita' } }
        finally { $graphics.ReleaseHdc($dc); [void][WordPageGdi]::DeleteEnhMetaFile($emfHandle) }
        $bitmap.Save((Join-Path $output ('page-{0:D2}.png' -f $i)), [Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose(); $bitmap.Dispose(); $metafile.Dispose(); $stream.Dispose()
        Stage ('Resa pagina ' + $i)
    }
    Stage 'PASS apertura Word e resa PNG; nessun PDF'
}
finally {
    $discard = 0
    try { if ($null -ne $taskDoc) { $taskDoc.Close([ref]$discard); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskDoc) } }
    finally { if ($null -ne $taskWord) { $taskWord.Quit([ref]$discard); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskWord) } }
}
