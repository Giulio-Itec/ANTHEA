param([string[]] $Suites = @('projects', 'project-workspace', 'hierarchy', 'sharing', 'materials', 'steel', 'ca-features', 'ca-extensions', 'bridge-curves', 'horizontal', 'project-report'), [string] $OutputDirectory, [string] $Executable)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository ('supporto/artefatti/calculation-library/ui-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
if (-not $Executable) { $Executable = Join-Path $repository 'X.Desktop/bin/Release/net8.0-windows/ANTHEA.exe' }
foreach ($suite in $Suites) {
    $folder = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $suite
    if ((Test-Path -LiteralPath $folder) -and (Get-ChildItem -LiteralPath $folder -Force | Select-Object -First 1)) { throw "Usare una cartella nuova per UI ${suite}: output precedenti presenti." }
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $process = Start-Process -FilePath $Executable -ArgumentList @("--smoke-$suite", ('"' + $folder + '"')) -WindowStyle Hidden -PassThru
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    while (-not $process.WaitForExit(1000)) {
        if ($elapsed.Elapsed.TotalMinutes -gt 5) { $process.Kill(); throw "Timeout UI $suite" }
    }
    if ($process.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $folder 'errore.txt'))) {
        if (Test-Path -LiteralPath (Join-Path $folder 'errore.txt')) { Get-Content -LiteralPath (Join-Path $folder 'errore.txt') }
        throw "UI $suite fallita (codice $($process.ExitCode))"
    }
    $sentinel = switch ($suite) { 'ca-features' {'features.txt'} 'ca-extensions' {'esito.txt'} 'horizontal' {'orizzontale_smoke.txt'} default {'smoke.txt'} }
    $proof = Join-Path $folder $sentinel
    if (-not (Test-Path -LiteralPath $proof) -or -not (Select-String -LiteralPath $proof -Pattern 'OK:|superati|completato' -Quiet)) { throw "UI $suite senza attestazione finale: $sentinel" }
    Write-Output "PASS UI $suite ($([Math]::Round($elapsed.Elapsed.TotalSeconds, 1)) s)"
}
