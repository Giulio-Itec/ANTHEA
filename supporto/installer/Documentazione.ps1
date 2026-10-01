# Catalogo condiviso dalla build e dai controlli dell'installer. Nessuna operazione al caricamento.
function Get-InstallerGuides([string]$RepositoryRoot) {
    $directory = Join-Path $RepositoryRoot 'supporto\documentazione\Guide_ANTHEA'
    $entries = New-Object System.Collections.Generic.List[object]
    $revisions = @()
    foreach ($kind in 'pratica', 'teorica') {
        $candidates = @(Get-ChildItem -LiteralPath $directory -File -ErrorAction Stop |
            Where-Object { $_.Name -match "^ANTHEA_Guida_${kind}_.*Rev\d+\.pdf$" } |
            Sort-Object { [int]([regex]::Match($_.BaseName, 'Rev(\d+)$').Groups[1].Value) })
        if ($candidates.Count -eq 0) { throw "Guida $kind in PDF non trovata in $directory" }
        $pdf = $candidates[-1]
        $revision = [int]([regex]::Match($pdf.BaseName, 'Rev(\d+)$').Groups[1].Value)
        $revisions += $revision
        $entries.Add([pscustomobject]@{ Source = $pdf.FullName; Target = "Guida $kind ANTHEA.pdf"; Shortcut = "Guida $kind" })
    }
    if ($revisions[0] -ne $revisions[1]) {
        throw 'Le ultime revisioni delle guide pratica e teorica non coincidono: allineare i PDF prima di creare il setup.'
    }
    $manifest = Join-Path $RepositoryRoot 'supporto\installer\Guide.json'
    foreach ($entry in (Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json)) {
        $entries.Add([pscustomobject]@{
            Source = Join-Path $RepositoryRoot $entry.source
            Target = $entry.target
            Shortcut = $entry.shortcut.Replace('/', '\')
        })
    }
    $targets = @{}
    $shortcuts = @{}
    foreach ($entry in $entries) {
        if ([IO.Path]::GetFileName($entry.Target) -ne $entry.Target -or $entry.Target -notlike '*.pdf') {
            throw "Nome PDF di destinazione non valido: $($entry.Target)"
        }
        if ($targets.ContainsKey($entry.Target) -or $shortcuts.ContainsKey($entry.Shortcut)) {
            throw "PDF o collegamento duplicato nel catalogo: $($entry.Target)"
        }
        $targets[$entry.Target] = $true
        $shortcuts[$entry.Shortcut] = $true
        if (-not (Test-Path -LiteralPath $entry.Source -PathType Leaf)) {
            throw "PDF obbligatorio mancante: $($entry.Source)"
        }
        $stream = [IO.File]::OpenRead($entry.Source)
        try {
            $header = New-Object byte[] 5
            if ($stream.Read($header, 0, 5) -ne 5 -or [Text.Encoding]::ASCII.GetString($header) -ne '%PDF-') {
                throw "Il file non contiene un PDF: $($entry.Source)"
            }
        }
        finally { $stream.Dispose() }
    }
    return [pscustomobject]@{ Revision = ('{0:D2}' -f $revisions[0]); Entries = $entries.ToArray() }
}

function Write-InstallerGuides([object]$Catalog, [string]$Stage) {
    $target = Join-Path $Stage 'guide'
    New-Item -ItemType Directory -Force $target | Out-Null
    $install = New-Object System.Collections.Generic.List[string]
    $uninstall = New-Object System.Collections.Generic.List[string]
    $links = New-Object System.Collections.Generic.List[string]
    $unlink = New-Object System.Collections.Generic.List[string]
    $folders = @{}
    $install.Add('SetOutPath "$INSTDIR\Guide"')
    $links.Add('CreateDirectory "$SMPROGRAMS\${APP}"')
    $links.Add('CreateShortcut "$SMPROGRAMS\${APP}\Documentazione.lnk" "$INSTDIR\Guide"')
    $unlink.Add('Delete "$SMPROGRAMS\${APP}\Documentazione.lnk"')
    foreach ($entry in $Catalog.Entries) {
        $file = Join-Path $target $entry.Target
        Copy-Item -LiteralPath $entry.Source -Destination $file -Force
        # NSIS expands dollars in quoted strings; filenames must preserve literal dollars.
        $name = $entry.Target.Replace('$', '$$')
        $shortcut = $entry.Shortcut.Replace('$', '$$')
        $install.Add('File "' + $file.Replace('$', '$$') + '"')
        $uninstall.Add('Delete "$INSTDIR\Guide\' + $name + '"')
        $folder = Split-Path $shortcut -Parent
        if ($folder -and -not $folders.ContainsKey($folder)) {
            $folders[$folder] = $true
            $links.Add('CreateDirectory "$SMPROGRAMS\${APP}\' + $folder + '"')
        }
        $links.Add('CreateShortcut "$SMPROGRAMS\${APP}\' + $shortcut + '.lnk" "$INSTDIR\Guide\' + $name + '"')
        $unlink.Add('Delete "$SMPROGRAMS\${APP}\' + $shortcut + '.lnk"')
        Write-Host "  documento: $($entry.Target) <- $([IO.Path]::GetFileName($entry.Source))"
    }
    # Remove only installed files and empty folders: preserve the user's own documents.
    $uninstall.Add('RMDir "$INSTDIR\Guide"')
    foreach ($folder in ($folders.Keys | Sort-Object Length -Descending)) {
        $unlink.Add('RMDir "$SMPROGRAMS\${APP}\' + $folder + '"')
    }
    $encoding = New-Object Text.UTF8Encoding $true
    [IO.File]::WriteAllLines((Join-Path $Stage 'install-guides.nsh'), $install.ToArray(), $encoding)
    [IO.File]::WriteAllLines((Join-Path $Stage 'uninstall-guides.nsh'), $uninstall.ToArray(), $encoding)
    [IO.File]::WriteAllLines((Join-Path $Stage 'guide-shortcuts.nsh'), $links.ToArray(), $encoding)
    [IO.File]::WriteAllLines((Join-Path $Stage 'uninstall-guide-shortcuts.nsh'), $unlink.ToArray(), $encoding)
}
