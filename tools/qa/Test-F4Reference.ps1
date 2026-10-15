<#
Integrity of the observed F4 reference (supporto/artefatti/refactoring/f4/before-port-1.7.1, not versioned: 37 MB) used by the
mandatory F4 comparisons of the baseline stage: every file listed in tests/fixtures/f4/reference.json must exist with the same
SHA-256 and size. A missing or altered reference fails instead of being skipped (audit of 14/10/2026).
#>
[CmdletBinding()]
param([Parameter(Mandatory)] [string] $Output)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$list = Get-Content -LiteralPath (Join-Path $root 'tests/fixtures/f4/reference.json') -Raw | ConvertFrom-Json
$reference = Join-Path $root $list.path
if (-not (Test-Path -LiteralPath $reference -PathType Container)) { throw "Riferimento F4 assente: $reference" }
$checked = 0
foreach ($entry in $list.files) {
    $path = Join-Path $reference $entry.file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "File del riferimento F4 assente: $($entry.file)" }
    $item = Get-Item -LiteralPath $path
    if ($item.Length -ne [long]$entry.bytes) { throw "Dimensione diversa nel riferimento F4: $($entry.file)" }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "SHA-256 diverso nel riferimento F4: $($entry.file)" }
    $checked++
}
if ($checked -eq 0) { throw 'Elenco del riferimento F4 vuoto' }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$summary = "PASS F4 reference: $checked file identici all'elenco versionato"
Set-Content -LiteralPath (Join-Path $Output 'esito.txt') -Value $summary -Encoding ASCII
Write-Output $summary
