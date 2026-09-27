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
@'
# ANTHEA.Calculations

Libreria .NET 8 autonoma, con dipendenze GPC e test.

- [Guida alla libreria e al trasferimento](supporto/docs/libreria-calcolo.md)
- [Audit di calcoli, progetti e prestazioni](supporto/docs/audit-calcoli-progetti-2026-09-27.md)
- [Validazione storica della separazione](supporto/docs/validazione-libreria-calcolo.md)

Eseguire dalla cartella di questo file:

```powershell
dotnet build X.Calculations/X.Calculations.csproj -c Release
dotnet run --project supporto/test/CalculationLibrary.Checks -c Release
```
'@ | Set-Content -LiteralPath (Join-Path $destination 'README.md') -Encoding utf8
New-Item -ItemType Directory -Path (Join-Path $destination 'supporto/docs') -Force | Out-Null
foreach ($document in @('libreria-calcolo.md', 'validazione-libreria-calcolo.md', 'audit-calcoli-progetti-2026-09-27.md')) {
    Copy-Item -LiteralPath (Join-Path $repository "supporto/docs/$document") -Destination (Join-Path $destination "supporto/docs/$document")
}
Get-ChildItem -LiteralPath (Join-Path $destination 'lib/Checker') -Filter '*.dll' | Get-FileHash -Algorithm SHA256 |
    Select-Object @{N='File';E={[IO.Path]::GetFileName($_.Path)}},Hash | Export-Csv -LiteralPath (Join-Path $destination 'dipendenze-sha256.csv') -NoTypeInformation
Write-Output "Libreria esportata in $destination"
Write-Output 'Verifica: dotnet run --project supporto/test/CalculationLibrary.Checks -c Release'
