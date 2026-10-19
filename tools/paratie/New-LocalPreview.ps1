<# Creates an isolated local preview from an already built Release folder and a coherent candidate snapshot.
   Does not install libraries, publish Git commits or change file associations. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GpcSnapshot,
    [Parameter(Mandatory)][string]$Examples,
    [Parameter(Mandatory)][string]$Output,
    [Parameter(Mandatory)][string]$GuideRevision,
    [Parameter(Mandatory)][string]$CiSummary
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$destination = [IO.Path]::GetFullPath($Output)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'supporto/artefatti/paratie'))
if (-not $destination.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'La candidata deve essere creata in una nuova sottocartella di supporto/artefatti/paratie.'
}
if (Test-Path -LiteralPath $destination) { throw 'La destinazione esiste gia: scegliere una nuova cartella.' }
if ($GuideRevision -notmatch '^\d+$') { throw 'Revisione della guida non valida.' }
$buildDirectory = Join-Path $repositoryRoot 'X.Desktop/bin/Release/net8.0-windows'
$snapshot = (Resolve-Path -LiteralPath $GpcSnapshot).Path
$examplesDirectory = (Resolve-Path -LiteralPath $Examples).Path
$ciPath = (Resolve-Path -LiteralPath $CiSummary).Path
$ci = Get-Content -LiteralPath $ciPath -Raw | ConvertFrom-Json
$commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE) { throw 'Revisione Git non disponibile.' }
if (& git -C $repositoryRoot status --porcelain) { throw 'Committare il lavoro prima di creare la candidata riproducibile.' }
if ($ci.commit -ne $commit -or $ci.dirtyBefore -or $ci.trackedModified.Count -gt 0) { throw 'La corsa CI deve riferirsi alla revisione corrente invariata.' }
if ($ci.profile -ne 'standard' -or $ci.only -or -not ($ci.results | Where-Object { $_.Suite -eq 'Presentation.Tests' -and $_.Status -eq 'PASS' })) {
    throw 'Serve il profilo standard completo, con Presentation.Tests superata.'
}
if (@($ci.results | Where-Object { $_.Status -notin @('PASS','KNOWN','FIXED') }).Count -gt 0) {
    throw 'La corsa CI contiene fallimenti: risolverli prima del confezionamento.'
}
if (-not (Test-Path -LiteralPath (Join-Path $buildDirectory 'ANTHEA.exe'))) { throw 'Build Release mancante.' }
$manifestPath = Join-Path $snapshot 'manifest.json'
if ((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne $ci.libManifestSha256) { throw 'Lo snapshot non coincide con quello verificato dal runner.' }
$gpc = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if (-not $gpc.candidate) { throw 'Questo strumento prepara solo una candidata locale.' }
foreach ($assembly in $gpc.assemblies) {
    foreach ($base in @($snapshot, $buildDirectory)) {
        $path = Join-Path $base $assembly.file
        if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $assembly.sha256) {
            throw "Libreria diversa dal set coerente: $path"
        }
    }
}
$forbidden = @(Get-ChildItem -LiteralPath $buildDirectory -Recurse -File | Where-Object { $_.Name -match '^(gmsh|GMsh\.Net|UnsafeEx|GMesh|ANTHEA\.Legacy|ANTHEA\.Testing)' })
if ($forbidden.Count) { throw 'Dipendenze di test presenti nella build Release.' }
$samples = @(Get-ChildItem -LiteralPath $examplesDirectory -File -Filter '*.anth')
if ($samples.Count -eq 0) { throw 'Nessun esempio .anth disponibile.' }
foreach ($sample in $samples) {
    $document = Get-Content -LiteralPath $sample.FullName -Raw | ConvertFrom-Json
    if ($document.formato -ne 'X' -or $document.tipo -ne 'calcolo' -or $document.modulo_id -ne 'geo_paratie') { throw "Esempio non paratie: $sample" }
}
New-Item -ItemType Directory -Path $destination | Out-Null
Copy-Item -LiteralPath $buildDirectory -Destination (Join-Path $destination 'app') -Recurse
Copy-Item -LiteralPath $examplesDirectory -Destination (Join-Path $destination 'esempi') -Recurse
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $destination 'gpc-manifest.json')
Copy-Item -LiteralPath $ciPath -Destination (Join-Path $destination 'ci-summary.json')
$guideFolder = New-Item -ItemType Directory -Path (Join-Path $destination 'guide')
foreach ($kind in @('pratica','teorica')) {
    foreach ($extension in @('docx','pdf')) {
        $guidePath = Join-Path $repositoryRoot "supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_${kind}_ITEC_Rev${GuideRevision}.${extension}"
        Copy-Item -LiteralPath $guidePath -Destination $guideFolder.FullName
    }
}
$launcher = '@echo off' + "`r`n" + 'start "" "%~dp0app\ANTHEA.exe"' + "`r`n"
[IO.File]::WriteAllText((Join-Path $destination 'Avvia-ANTHEA.cmd'), $launcher, [Text.Encoding]::ASCII)
foreach ($sample in $samples) {
    $launcher = '@echo off' + "`r`n" + 'start "" "%~dp0app\ANTHEA.exe" "%~dp0esempi\' + $sample.Name + '"' + "`r`n"
    [IO.File]::WriteAllText((Join-Path $destination ('Apri-' + $sample.BaseName + '.cmd')), $launcher, [Text.Encoding]::ASCII)
}
$files = @(Get-ChildItem -LiteralPath $destination -File -Recurse | ForEach-Object {
    [ordered]@{ Path=$_.FullName.Substring($destination.Length+1); Bytes=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
[ordered]@{
    CreatedUtc=[DateTime]::UtcNow.ToString('o'); Commit=$commit; Candidate=$true; OfficialRelease=$false
    Runtime='.NET Desktop Runtime 8 x64'; GuideRevision=$GuideRevision; GpcSnapshot=$snapshot
    Scope='Incrementi paratie 1-13: funzioni collaudate e limiti nelle due guide globali; matrice complessiva ancora aperta.'
    Examples=$samples.Name; Files=$files
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'manifest.json') -Encoding UTF8
Write-Output "Candidata locale: $destination"
