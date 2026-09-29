<#
.SYNOPSIS
Crea l'installer NSIS di ANTHEA.

.DESCRIPTION
Pubblica X.Desktop self-contained per win-x64 (runtime .NET 8 incluso) nella cartella
di lavoro supporto/artefatti/installer, senza toccare bin/obj del repository; copia l'ultima
revisione delle guide PDF, genera le immagini dell'installer dal logo e gli elenchi dei file
per installazione e disinstallazione, poi compila ANTHEA.nsi con makensis.
La versione e' quella di <Version> in X.Desktop/X.Desktop.csproj: il setup si chiama
ANTHEA-<versione>-Setup-x64.exe e con la stessa versione viene sovrascritto.

.PARAMETER MakeNsis
Percorso di makensis.exe. Se omesso: PATH, NSIS_HOME, registro, Programmi.

.PARAMETER OutputDirectory
Cartella del setup. Predefinita: supporto/installer (questa cartella).

.PARAMETER SkipPublish
Riusa la pubblicazione gia' presente nella cartella di lavoro.
#>
[CmdletBinding()]
param(
    [string]$MakeNsis,
    [string]$OutputDirectory,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $root 'X.Desktop\X.Desktop.csproj'
$icon = Join-Path $root 'X.Desktop\Assets\anthea.ico'
$logo = Join-Path $root 'X.Desktop\Assets\logo.png'
$guides = Join-Path $root 'supporto\documentazione\Guide_ANTHEA'
if (-not $OutputDirectory) { $OutputDirectory = $PSScriptRoot }
$OutputDirectory = [string]$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$work = Join-Path $root 'supporto\artefatti\installer'
$stage = Join-Path $work 'stage'
$app = Join-Path $stage 'app'

function Find-MakeNsis {
    if ($MakeNsis) {
        if (-not (Test-Path $MakeNsis)) { throw "makensis non trovato: $MakeNsis" }
        return [string]$MakeNsis
    }
    $command = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($command) { return [string]$command.Source }
    $candidates = @()
    if ($env:NSIS_HOME) { $candidates += Join-Path $env:NSIS_HOME 'makensis.exe' }
    foreach ($key in 'HKLM:\SOFTWARE\WOW6432Node\NSIS', 'HKLM:\SOFTWARE\NSIS') {
        $item = Get-ItemProperty $key -ErrorAction SilentlyContinue
        if ($item -and $item.'(default)') { $candidates += Join-Path $item.'(default)' 'makensis.exe' }
    }
    $candidates += (Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'), (Join-Path $env:ProgramFiles 'NSIS\makensis.exe')
    foreach ($candidate in $candidates) { if (Test-Path $candidate) { return [string]$candidate } }
    throw 'makensis.exe non trovato: installare NSIS 3 (winget install NSIS.NSIS) oppure usare -MakeNsis.'
}

function Get-ProjectVersion {
    [xml]$xml = Get-Content $project -Raw
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')
    if (-not $node -or $node.InnerText -notmatch '^\d+\.\d+\.\d+$') {
        throw "X.Desktop.csproj deve contenere <Version>maggiore.minore.patch</Version>."
    }
    return $node.InnerText
}

# NSIS strings are double quoted: a literal $ is written $$.
function ConvertTo-NsisString([string]$text) { return $text.Replace('$', '$$') }

function Join-InstallPath([string]$relative) {
    if ($relative) { return '$INSTDIR\' + (ConvertTo-NsisString $relative) }
    return '$INSTDIR'
}

function Write-Utf8Lines([string]$path, [string[]]$lines) {
    [System.IO.File]::WriteAllLines($path, $lines, (New-Object System.Text.UTF8Encoding $true))
}

function Write-FileLists {
    $files = @(Get-ChildItem $app -Recurse -File | Sort-Object DirectoryName, Name)
    if (-not (Test-Path (Join-Path $app 'ANTHEA.exe'))) { throw "ANTHEA.exe mancante in $app" }
    $install = New-Object System.Collections.Generic.List[string]
    $uninstall = New-Object System.Collections.Generic.List[string]
    $directory = $null
    foreach ($file in $files) {
        $relative = $file.DirectoryName.Substring($app.Length).TrimStart('\')
        if ($relative -ne $directory) {
            $install.Add('SetOutPath "' + (Join-InstallPath $relative) + '"')
            $directory = $relative
        }
        $install.Add('File "' + (ConvertTo-NsisString $file.FullName) + '"')
        $target = if ($relative) { "$relative\$($file.Name)" } else { $file.Name }
        $uninstall.Add('Delete "' + (Join-InstallPath $target) + '"')
    }
    # Only the folders created by the installer, deepest first and never recursively.
    $folders = @(Get-ChildItem $app -Recurse -Directory | ForEach-Object { $_.FullName.Substring($app.Length).TrimStart('\') } |
        Sort-Object { ($_ -split '\\').Count } -Descending)
    foreach ($folder in $folders) { $uninstall.Add('RMDir "' + (Join-InstallPath $folder) + '"') }
    Write-Utf8Lines (Join-Path $stage 'install-files.nsh') $install.ToArray()
    Write-Utf8Lines (Join-Path $stage 'uninstall-files.nsh') $uninstall.ToArray()
    return $files
}

function Copy-LatestGuides {
    $target = Join-Path $stage 'guide'
    New-Item -ItemType Directory -Force $target | Out-Null
    $revisions = @()
    foreach ($kind in 'pratica', 'teorica') {
        $pdf = Get-ChildItem $guides -Filter "ANTHEA_Guida_${kind}_*Rev*.pdf" |
            Sort-Object { [int]([regex]::Match($_.BaseName, 'Rev(\d+)').Groups[1].Value) } | Select-Object -Last 1
        if (-not $pdf) { throw "Guida $kind in PDF non trovata in $guides" }
        Copy-Item $pdf.FullName (Join-Path $target "Guida $kind ANTHEA.pdf") -Force
        $revisions += [regex]::Match($pdf.BaseName, 'Rev(\d+)').Groups[1].Value
        Write-Host "  guida $kind`: $($pdf.Name)"
    }
    return ($revisions | Sort-Object -Unique) -join '/'
}

# MUI bitmaps at their native size (welcome 164x314, header 150x57), drawn from the app logo.
function New-InstallerImages {
    Add-Type -AssemblyName System.Drawing
    $source = [System.Drawing.Image]::FromFile($logo)
    $navy = [System.Drawing.Color]::FromArgb(11, 37, 94)
    $gold = [System.Drawing.Color]::FromArgb(207, 157, 72)
    # The logo background is not pure white: key it out so it blends with the page.
    $key = New-Object System.Drawing.Imaging.ImageAttributes
    $key.SetColorKey([System.Drawing.Color]::FromArgb(238, 238, 238), [System.Drawing.Color]::White)
    $pixel = [System.Drawing.GraphicsUnit]::Pixel
    try {
        $welcome = New-Object System.Drawing.Bitmap 164, 314, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $g = [System.Drawing.Graphics]::FromImage($welcome)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $g.Clear([System.Drawing.Color]::White)
        $g.DrawImage($source, (New-Object System.Drawing.Rectangle 10, 44, 144, 144), 0, 0, $source.Width, $source.Height, $pixel, $key)
        $pen = New-Object System.Drawing.Pen $gold, 1
        $g.DrawLine($pen, 32, 208, 132, 208)
        $format = New-Object System.Drawing.StringFormat
        $format.Alignment = [System.Drawing.StringAlignment]::Center
        $font = New-Object System.Drawing.Font 'Segoe UI', 8.5
        $brush = New-Object System.Drawing.SolidBrush $navy
        $g.DrawString("Calcolo strutturale`ne geotecnico", $font, $brush, (New-Object System.Drawing.RectangleF 0, 216, 164, 40), $format)
        $small = New-Object System.Drawing.Font 'Segoe UI', 7.5
        $grey = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(110, 118, 135))
        $g.DrawString('ITEC Engineering', $small, $grey, (New-Object System.Drawing.RectangleF 0, 288, 164, 16), $format)
        $g.Dispose()
        $welcome.Save((Join-Path $stage 'welcome.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
        $welcome.Dispose()

        $header = New-Object System.Drawing.Bitmap 150, 57, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $g = [System.Drawing.Graphics]::FromImage($header)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.Clear([System.Drawing.Color]::White)
        # Symbol only (the header title already names ANTHEA): logo pixels 160..1100 x 70..960.
        $g.DrawImage($source, (New-Object System.Drawing.Rectangle 89, 1, 58, 55), 160, 70, 940, 890, $pixel, $key)
        $g.Dispose()
        $header.Save((Join-Path $stage 'header.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
        $header.Dispose()
    }
    finally { $key.Dispose(); $source.Dispose() }
}

$makensisPath = Find-MakeNsis
$version = Get-ProjectVersion
Write-Host "ANTHEA $version - makensis: $makensisPath"

if ($SkipPublish) {
    if (-not (Test-Path (Join-Path $app 'ANTHEA.exe'))) { throw "Nessuna pubblicazione in $app" }
    Get-ChildItem $stage -File | Remove-Item -Force
    if (Test-Path (Join-Path $stage 'guide')) { Remove-Item (Join-Path $stage 'guide') -Recurse -Force }
}
else {
    if ((Test-Path $stage) -and (Split-Path $stage -Leaf) -eq 'stage') { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Force $app | Out-Null
    # Separate artifacts path: the RID-specific restore must not overwrite obj of the normal build.
    $build = Join-Path $work 'build'
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $app --artifacts-path $build `
        -p:DebugType=None -p:DebugSymbols=false -p:SatelliteResourceLanguages=it
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish non riuscito ($LASTEXITCODE)" }
}

$guideRevision = Copy-LatestGuides
New-InstallerImages
$files = Write-FileLists
$bytes = ($files | Measure-Object Length -Sum).Sum
Write-Host ("  applicazione: {0} file, {1:N1} MB" -f $files.Count, ($bytes / 1MB))

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$setup = Join-Path $OutputDirectory "ANTHEA-$version-Setup-x64.exe"
if (Test-Path $setup) { Write-Host "  il setup della versione $version esiste gia': viene sovrascritto" }
& $makensisPath /V3 /INPUTCHARSET UTF8 "/DVERSION=$version" "/DVERSION4=$version.0" "/DSTAGE=$stage" `
    "/DOUTFILE=$setup" "/DICON=$icon" "/DGUIDE_REV=$guideRevision" (Join-Path $PSScriptRoot 'ANTHEA.nsi')
if ($LASTEXITCODE -ne 0) { throw "makensis non riuscito ($LASTEXITCODE)" }

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
Write-Host ("Setup: {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
Write-Host "SHA256: $hash"
