[CmdletBinding()]
param([Parameter(Mandatory)] [string] $Output, [string] $CapturedRoot)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tool = Join-Path $root 'tests/ANTHEA.Testing/bin/Release/net8.0/ANTHEA.Testing.dll'
function FileHash([string] $path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $algorithm.Dispose() }
}
if (!$CapturedRoot) {
    $CapturedRoot = $Output
    foreach ($name in 'a','b') {
        & dotnet $tool capture-f4 (Join-Path $CapturedRoot $name) --root $root
        if ($LASTEXITCODE -ne 0) { throw "Cattura F4 $name fallita ($LASTEXITCODE)" }
    }
}
New-Item -ItemType Directory -Force -Path $Output | Out-Null
& dotnet $tool compare (Join-Path $CapturedRoot 'a') (Join-Path $CapturedRoot 'b') --report (Join-Path $Output 'comparison.json')
if ($LASTEXITCODE -ne 0) { throw 'Catture F4 non equivalenti' }
# The capture's only volatile data is stored in raw/ and the common manifest.
# Require identical bytes as well as the diagnostic value comparison above.
$left = [IO.Path]::GetFullPath((Join-Path $CapturedRoot 'a'))
$right = [IO.Path]::GetFullPath((Join-Path $CapturedRoot 'b'))
$prefixLength = $left.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1
$files = @(Get-ChildItem -LiteralPath $left -File -Recurse | Where-Object {
    $relative = $_.FullName.Substring($prefixLength)
    $relative -ne 'manifest.json' -and -not $relative.StartsWith('raw' + [IO.Path]::DirectorySeparatorChar)
})
foreach ($file in $files) {
    $relative = $file.FullName.Substring($prefixLength)
    if ((FileHash $file.FullName) -ne (FileHash (Join-Path $right $relative))) {
        throw "Cattura F4 non identica byte per byte: $relative"
    }
}
$manifest = Get-Content -LiteralPath (Join-Path $left 'manifest.json') -Raw | ConvertFrom-Json
$expected = @{ walls = 35; schedules = 32; 'wall-design' = 15; piles = 24; 'section-design' = 9; casi = 115; errori_registrati = 0 }
foreach ($key in $expected.Keys) {
    if ($manifest.conteggi.$key -ne $expected[$key]) { throw "Copertura F4 alterata: $key" }
}
if ($manifest.rifiuti_attesi -ne 14) { throw 'Rifiuti F4 diversi dai 14 previsti' }
foreach ($shape in 'Rettangolare','Circolare') {
    $sequential = Join-Path $left "results/section-design/$shape-workers-1.json"
    $parallel = Join-Path $left "results/section-design/$shape-workers-4.json"
    if ((FileHash $sequential) -ne (FileHash $parallel)) {
        throw "Ricerca CA $shape diversa con uno e quattro worker"
    }
}
$summary = "PASS F4: $($manifest.conteggi.casi) casi, $($manifest.rifiuti_attesi) rifiuti attesi, $($files.Count) file identici, cinque ambiti, Bridge Design esclusa"
Set-Content -LiteralPath (Join-Path $Output 'esito.txt') -Value $summary -Encoding utf8
Write-Host $summary
