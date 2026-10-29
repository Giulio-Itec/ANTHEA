<#
.SYNOPSIS
  Tempi delle combinazioni del modulo Modelli su modelli MIDAS sintetici (refactoring combo-modelli, 29/10/2026).
.DESCRIPTION
  Compila ModelWorkspace.Checks in Release e lancia la modalità --combination-timing: per ogni modello (100 elementi × 50 casi,
  500 elementi × 100 casi, o un'altra misura «elementixcasi» di almeno 24 elementi) genera le combinazioni NTC 2018 con il
  generatore di Model, aggiunge le definizioni dell'utente (somma, inviluppi SLU e SLV, inviluppo annidato con coefficiente
  negativo) e misura ogni fase: generazione, catalogo, preparazione GPC, somma dei casi, campi ANTHEA, Attach, salvataggio e
  riapertura, view model (tabelle, diagrammi, modifica e calcolo dal view model). Corse di riscaldamento escluse; mediana, minimo e
  massimo delle corse misurate in tempi.md e tempi.json. Ogni corsa calcola l'impronta di tutti i risultati: per il modello
  100 × 50 (8 lineari) deve coincidere con quella della 1.16.0, altrimenti la corsa fallisce.
  -Baseline: cartella con ModelWorkspace.Checks.dll compilato dal codice della 1.16.0 (main 61dc4ff3, set AM; per esempio
  supporto\artefatti\combo-modelli\bin-1.16.0): le stesse corse in <Output>\prima, per il confronto prima/dopo.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\model-viewer\Measure-CombinationTimes.ps1 -Models 100x50,500x100 -Runs 7 -Warmup 2 -Baseline supporto\artefatti\combo-modelli\bin-1.16.0
#>
param(
    [string[]] $Models = @('100x50', '500x100'),
    [int] $Runs = 3,
    [int] $Warmup = 1,
    [int] $Linear = 8,
    [string] $Output,
    [string] $Baseline,
    [switch] $NoBuild
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
# The SDK of global.json is found from the current folder: build and run from the root of the checkout, as ci.ps1.
Push-Location $root
if (-not $Output) { $Output = Join-Path $root ('supporto\artefatti\combo-modelli\tempi-' + (Get-Date -Format 'yyyyMMdd-HHmm')) }
New-Item -ItemType Directory -Force $Output | Out-Null
$project = Join-Path $root 'tests\ModelWorkspace.Checks\ModelWorkspace.Checks.csproj'
if (-not $NoBuild) {
    # The antivirus may block a freshly built apphost: the checks run through dotnet exec, without apphost.
    & dotnet build $project -c Release -nologo -v:minimal -p:UseAppHost=false | Tee-Object -FilePath (Join-Path $Output 'build.log') | Select-Object -Last 3
    if ($LASTEXITCODE -ne 0) { throw "Build non riuscita: vedi $Output\build.log" }
}
$dll = (& dotnet msbuild $project -getProperty:TargetPath -p:Configuration=Release -nologo | Select-Object -Last 1).Trim()
$timing = @('--combination-timing', '--models', ($Models -join ','), '--runs', $Runs, '--warmup', $Warmup, '--linear', $Linear)
$watch = [Diagnostics.Stopwatch]::StartNew()
# Windows PowerShell turns the lines of stderr into errors: the runs report their failures themselves (exit code, esito.txt).
$ErrorActionPreference = 'Continue'
$code = 0
if ($Baseline) {
    $before = Join-Path $Output 'prima'; New-Item -ItemType Directory -Force $before | Out-Null
    $old = Join-Path (Resolve-Path $Baseline) 'ModelWorkspace.Checks.dll'
    Write-Host "prima: dotnet exec $old $before $($timing -join ' ')"
    & dotnet exec $old $before @timing 2>&1 | ForEach-Object { "$_" } | Tee-Object -FilePath (Join-Path $before 'corsa.log')
    if ($LASTEXITCODE -ne 0) { $code = $LASTEXITCODE }
}
Write-Host "dopo: dotnet exec $dll $Output $($timing -join ' ')"
& dotnet exec $dll $Output @timing 2>&1 | ForEach-Object { "$_" } | Tee-Object -FilePath (Join-Path $Output 'corsa.log')
if ($LASTEXITCODE -ne 0) { $code = $LASTEXITCODE }
Pop-Location
Write-Host ("Durata {0:F1} min - esito {1} - {2}" -f $watch.Elapsed.TotalMinutes, $code, (Join-Path $Output 'tempi.md'))
exit $code
