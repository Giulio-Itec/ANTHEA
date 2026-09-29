param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$taskSupport = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskArchive = Join-Path $taskSupport 'SUPERATI'
$taskEntries = [Collections.Generic.List[object]]::new()
function Add-ArchiveItem([string]$Relative, [string]$Reason) {
    $taskSource = [IO.Path]::GetFullPath((Join-Path $taskSupport $Relative))
    $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskArchive $Relative))
    if (-not $taskSource.StartsWith($taskSupport + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Origine fuori da supporto' }
    if (-not $taskDestination.StartsWith($taskArchive + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Destinazione fuori da SUPERATI' }
    if (-not (Test-Path -LiteralPath $taskSource)) { throw "Origine assente: $taskSource" }
    if (Test-Path -LiteralPath $taskDestination) { throw "Destinazione esistente: $taskDestination" }
    $taskEntries.Add([pscustomobject]@{source=$taskSource;destination=$taskDestination;relative=$Relative;reason=$Reason})
}
Get-ChildItem -LiteralPath (Join-Path $taskSupport 'documentazione/Guide_ANTHEA') -File |
    Where-Object { $_.Name -match 'Rev0[1-3]\.docx$' } |
    ForEach-Object { Add-ArchiveItem ('documentazione/Guide_ANTHEA/' + $_.Name) 'Sostituita dalla corrispondente guida ITEC Rev04.' }
Get-ChildItem -LiteralPath (Join-Path $taskSupport 'documentazione/Validazione_CA_ANTHEA') -File |
    Where-Object { $_.Name -ne 'ANTHEA_Validazione_Software_CA_e_Ponti_Rev03.docx' -and $_.Extension -eq '.docx' } |
    ForEach-Object { Add-ArchiveItem ('documentazione/Validazione_CA_ANTHEA/' + $_.Name) 'Revisione precedente alla validazione software CA e ponti Rev03.' }
foreach ($taskName in @('esempi','validazione-20260929','validazione-finale','qa-20260929','qa-finali','10-esempi-anthea-controllo-interno.zip')) {
    Add-ArchiveItem ('artefatti/stabilita-globale/' + $taskName) 'Raccolta o PDF anteriori alla correzione dei cerchi tangenti. Riferimenti correnti: regressione-tangenti-max e confronti-max/SOMMARIO-PARZIALE.'
}
Get-ChildItem -Path (Join-Path $taskSupport 'artefatti/stabilita-globale/confronti-max/*/relazione-anthea.docx') -File |
    ForEach-Object { Add-ArchiveItem ('artefatti/stabilita-globale/confronti-max/' + $_.Directory.Name + '/' + $_.Name) 'Relazione generata prima della correzione dei cerchi tangenti. Nuova relazione della serie MAX ancora da rigenerare alla ripresa dei test.' }
if (-not $Apply) {
    $taskEntries | Select-Object relative,reason | ConvertTo-Json
    return
}
[IO.Directory]::CreateDirectory($taskArchive) | Out-Null
$taskLog = [Collections.Generic.List[object]]::new()
foreach ($taskEntry in $taskEntries) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskEntry.destination)) | Out-Null
    $taskItem = Get-Item -LiteralPath $taskEntry.source
    $taskFiles = if ($taskItem.PSIsContainer) { @(Get-ChildItem -LiteralPath $taskEntry.source -Recurse -File) } else { @($taskItem) }
    foreach ($taskFile in $taskFiles) {
        $taskRelative = $taskFile.FullName.Substring($taskSupport.Length + 1)
        $taskLog.Add([pscustomobject]@{original=$taskRelative;archived=('SUPERATI/' + $taskRelative);sha256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash;reason=$taskEntry.reason})
    }
    Move-Item -LiteralPath $taskEntry.source -Destination $taskEntry.destination
}
$taskLog | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskArchive 'registro-20260929.json') -Encoding utf8
Write-Output ("Archiviati {0} elementi, {1} file. Nessuna eliminazione." -f $taskEntries.Count,$taskLog.Count)
