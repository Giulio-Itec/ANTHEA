param(
    [string] $Cases,
    [string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $Cases) { $Cases = Join-Path $repository 'supporto/test/casi_confronto.json' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository 'supporto/artefatti/verifiche-software' }
$run = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('suite-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$trace = Join-Path $run 'avanzamento.txt'
$log = Join-Path $run 'esecuzione.log'
& dotnet run --project (Join-Path $repository 'supporto/test/X.Verifiche') -c Release -- --software $Cases $trace *> $log
$testExit = $LASTEXITCODE
$complete = (Test-Path -LiteralPath $trace) -and (Select-String -LiteralPath $trace -Pattern '^Completato: ' -Quiet)
if ($testExit -ne 0 -or -not $complete) {
    throw "Verifica software/report non completata (codice $testExit). Consultare $log e $trace. Il solo codice zero non prova il completamento."
}
Get-Content -LiteralPath $trace -Tail 1
Write-Output "Artefatti: $run"
