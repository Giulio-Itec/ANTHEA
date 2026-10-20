# Catalogo unico con l'applicazione; solo file espliciti, nessuna cancellazione ricorsiva.
function Get-InstallerExamples([string]$RepositoryRoot) {
    $root = Join-Path $RepositoryRoot 'supporto\esempi\distribuzione'
    $manifest = Join-Path $root 'catalogo.json'
    $catalog = Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($catalog.version -ne 1 -or @($catalog.entries).Count -lt 1) { throw 'Catalogo esempi non valido.' }
    $paths = @{}
    $files = New-Object System.Collections.Generic.List[object]
    $files.Add([pscustomobject]@{ Source = $manifest; Target = 'catalogo.json' })
    foreach ($entry in $catalog.entries) {
        if ($entry.module -notmatch '^[a-z0-9_]+$' -or
            $entry.path -notmatch '^[a-z0-9_]+/[a-zA-Z0-9_-][a-zA-Z0-9_.-]*\.anthea$' -or
            $entry.path.Split('/')[0] -ne $entry.module -or
            $entry.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or
            [string]::IsNullOrWhiteSpace($entry.title)) { throw "Esempio non valido: $($entry.path)" }
        if ($paths.ContainsKey($entry.path)) { throw "Esempio duplicato: $($entry.path)" }
        $paths[$entry.path] = $true
        $source = Join-Path $root $entry.path.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Esempio mancante: $source" }
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Esempio modificato: $source" }
        $files.Add([pscustomobject]@{ Source = $source; Target = $entry.path.Replace('/', '\') })
    }
    return $files.ToArray()
}

function Write-InstallerExamples([object[]]$Files, [string]$Stage) {
    $root = Join-Path $Stage 'esempi'
    $install = New-Object System.Collections.Generic.List[string]
    $uninstall = New-Object System.Collections.Generic.List[string]
    $folders = @{}
    foreach ($file in $Files) {
        $target = Join-Path $root $file.Target
        $folder = [IO.Path]::GetDirectoryName($file.Target)
        New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $file.Source -Destination $target
        if ($folder) { $folders[$folder] = $true }
        $destination = '$INSTDIR\Esempi' + $(if ($folder) { '\' + $folder })
        $install.Add('SetOutPath "' + $destination + '"')
        $install.Add('File "' + $target.Replace('$', '$$') + '"')
        $uninstall.Add('Delete "$INSTDIR\Esempi\' + $file.Target + '"')
    }
    foreach ($folder in ($folders.Keys | Sort-Object -Descending)) { $uninstall.Add('RMDir "$INSTDIR\Esempi\' + $folder + '"') }
    $uninstall.Add('RMDir "$INSTDIR\Esempi"')
    $encoding = New-Object System.Text.UTF8Encoding $true
    [IO.File]::WriteAllLines((Join-Path $Stage 'install-examples.nsh'), $install, $encoding)
    [IO.File]::WriteAllLines((Join-Path $Stage 'uninstall-examples.nsh'), $uninstall, $encoding)
    [IO.File]::WriteAllLines((Join-Path $Stage 'example-shortcuts.nsh'), @(
        'CreateDirectory "$SMPROGRAMS\${APP}"',
        'CreateShortcut "$SMPROGRAMS\${APP}\Esempi.lnk" "$INSTDIR\Esempi"'
    ), $encoding)
    [IO.File]::WriteAllLines((Join-Path $Stage 'uninstall-example-shortcuts.nsh'), @(
        'Delete "$SMPROGRAMS\${APP}\Esempi.lnk"'
    ), $encoding)
}
