<#
.SYNOPSIS
  Tempi delle combinazioni del modulo Modelli su modelli MIDAS sintetici (refactoring combo-modelli, 29/10/2026).
.DESCRIPTION
  Compila ModelWorkspace.Checks in Release e lancia la modalità --combination-timing: per ogni modello (100 elementi × 50 casi,
  500 elementi × 100 casi) genera le combinazioni NTC 2018 con il generatore di Model, aggiunge le definizioni dell'utente
  (somma, inviluppi SLU e SLV, inviluppo annidato con coefficiente negativo) e misura ogni fase: generazione, catalogo,
  preparazione GPC, somma dei casi, campi ANTHEA, Attach, salvataggio e riapertura, view model (tabelle, diagrammi, modifica e
  calcolo dal view model). Corse di riscaldamento escluse; mediana, minimo e massimo delle corse misurate in tempi.md e tempi.json.
  Ogni corsa calcola l'impronta di tutti i risultati: per il modello 100 × 50 (8 lineari) deve coincidere con quella della 1.16.0.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\model-viewer\Measure-CombinationTimes.ps1 -Models 100x50,500x100 -Runs 7 -Warmup 2
#>
param(
    [string[]] $Models = @('100x50', '500x100'),
    [int] $Runs = 3,
    [int] $Warmup = 1,
    [int] $Linear = 8,
    [string] $Output,
    [switch] $NoBuild
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $Output) { $Output = Join-Path $root ('supporto\artefatti\combo-modelli\tempi-' + (Get-Date -Format 'yyyyMMdd-HHmm')) }
New-Item -ItemType Directory -Force $Output | Out-Null
$project = Join-Path $root 'tests\ModelWorkspace.Checks\ModelWorkspace.Checks.csproj'
if (-not $NoBuild) {
    # The antivirus may block a freshly built apphost: the checks run through dotnet exec, without apphost.
    & dotnet build $project -c Release -nologo -v:minimal -p:UseAppHost=false | Tee-Object -FilePath (Join-Path $Output 'build.log') | Select-Object -Last 3
    if ($LASTEXITCODE -ne 0) { throw "Build non riuscita: vedi $Output\build.log" }
}
$dll = (& dotnet msbuild $project -getProperty:TargetPath -p:Configuration=Release -nologo | Select-Object -Last 1).Trim()
$arguments = @('exec', $dll, $Output, '--combination-timing', '--models', ($Models -join ','), '--runs', $Runs, '--warmup', $Warmup, '--linear', $Linear)
Write-Host "dotnet $($arguments -join ' ')"
$watch = [Diagnostics.Stopwatch]::StartNew()
& dotnet @arguments 2>&1 | Tee-Object -FilePath (Join-Path $Output 'corsa.log')
$code = $LASTEXITCODE
Write-Host ("Durata {0:F1} min · esito {1} · {2}" -f $watch.Elapsed.TotalMinutes, $code, (Join-Path $Output 'tempi.md'))
exit $code
