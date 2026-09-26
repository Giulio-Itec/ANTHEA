param([string] $OutputDirectory)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository ('supporto/artefatti/calculation-library/portable-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $destination) -and (Get-ChildItem -LiteralPath $destination -Force | Select-Object -First 1)) { throw 'Scegliere una cartella di destinazione vuota.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$sources = @('X.Calculations', 'supporto/test/CalculationLibrary.Checks', 'lib/Checker')
foreach ($source in $sources) {
    $base = Join-Path $repository $source
    foreach ($file in Get-ChildItem -LiteralPath $base -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
        $relative = [IO.Path]::GetRelativePath($repository, $file.FullName)
        $target = Join-Path $destination $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
Copy-Item -LiteralPath (Join-Path $repository 'supporto/docs/libreria-calcolo.md') -Destination (Join-Path $destination 'README.md')
New-Item -ItemType Directory -Path (Join-Path $destination 'supporto/docs') -Force | Out-Null
foreach ($document in @('libreria-calcolo.md', 'validazione-libreria-calcolo.md')) {
    Copy-Item -LiteralPath (Join-Path $repository "supporto/docs/$document") -Destination (Join-Path $destination "supporto/docs/$document")
}
Get-ChildItem -LiteralPath (Join-Path $destination 'lib/Checker') -Filter '*.dll' | Get-FileHash -Algorithm SHA256 |
    Select-Object @{N='File';E={[IO.Path]::GetFileName($_.Path)}},Hash | Export-Csv -LiteralPath (Join-Path $destination 'dipendenze-sha256.csv') -NoTypeInformation
Write-Output "Libreria esportata in $destination"
Write-Output 'Verifica: dotnet run --project supporto/test/CalculationLibrary.Checks -c Release'
