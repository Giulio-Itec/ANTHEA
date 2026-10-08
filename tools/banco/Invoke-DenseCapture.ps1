<#
Dense captures of CheckerMigration.Capture (refactoring F2.1, docs/refactoring/f2.1-banco.md).

  powershell -NoProfile -ExecutionPolicy Bypass -File tools\banco\Invoke-DenseCapture.ps1 -Output <cartella nuova> [-Modes tutte,muri,pali,mesh] [-Commit <sha>] [-NoBuild] [-Motore legacy|libreria]

Builds supporto\test\CheckerMigration.Capture (Release) unless -NoBuild, then runs every mode with --manifest into <Output>\<mode>,
with 'dotnet <dll>' (not the apphost .exe, which the antivirus of this machine may refuse) and the logs in <Output>\log.
-Motore passes '--motore <value>' to every mode (shear and torsion engine of ConcreteShearTorsionAdapter, refactoring F2.5-F2.6);
without it the default engine of the adapter. The F2-pre-f28 reference (F2.8-A0) is captured with -Motore legacy.
The commit written in the first line of every file is HEAD of this checkout unless -Commit is given.
Exit code 0 only if every mode exits with 0.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Output,
    [string[]] $Modes = @('tutte', 'muri', 'pali', 'mesh'),
    [string] $Commit,
    [switch] $NoBuild,
    [string] $GpcLibDir,
    [ValidateSet('legacy', 'libreria')] [string] $Motore
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$Project = Join-Path $Root 'supporto\test\CheckerMigration.Capture\CheckerMigration.Capture.csproj'
$Dll = Join-Path $Root 'supporto\test\CheckerMigration.Capture\bin\Release\net8.0\CheckerMigration.Capture.dll'
$Output = [IO.Path]::GetFullPath($Output)

function Find-Git {
    $cmd = Get-Command git -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $vs = 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
    if (Test-Path -LiteralPath $vs) { return $vs }
    return $null
}
if (-not $Commit) {
    $git = Find-Git
    if (-not $git) { throw 'git non trovato: passare -Commit.' }
    $Commit = (& $git -C $Root rev-parse HEAD).Trim()
}

$logs = Join-Path $Output 'log'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
if (-not $NoBuild) {
    $arguments = @('build', ('"' + $Project + '"'), '-c', 'Release', '-nologo', '-v:minimal')
    if ($GpcLibDir) { $arguments += ('"-p:GpcLibDir=' + $GpcLibDir + '"') }
    $b = Start-Process -FilePath 'dotnet' -ArgumentList ($arguments -join ' ') -WorkingDirectory $Root -NoNewWindow -PassThru `
        -RedirectStandardOutput (Join-Path $logs 'build.out.log') -RedirectStandardError (Join-Path $logs 'build.err.log')
    $null = $b.Handle
    $b.WaitForExit()
    if ($b.ExitCode -ne 0) { throw "Compilazione di CheckerMigration.Capture fallita: $(Join-Path $logs 'build.out.log')" }
}
if (-not (Test-Path -LiteralPath $Dll)) { throw "DLL non trovata: $Dll" }

$failed = 0
foreach ($mode in @($Modes | ForEach-Object { $_ -split ',' } | Where-Object { $_ })) {
    $out = Join-Path $Output $mode
    if ((Test-Path -LiteralPath $out) -and (Get-ChildItem -LiteralPath $out -Force | Select-Object -First 1)) { throw "Cartella non vuota: $out" }
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $captureArgs = @($Dll, $out, $Commit, $mode, '--manifest')
    if ($Motore) { $captureArgs += @('--motore', $Motore) }
    $line = ($captureArgs | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath 'dotnet' -ArgumentList $line -WorkingDirectory $Root -NoNewWindow -PassThru `
        -RedirectStandardOutput (Join-Path $logs "$mode.out.log") -RedirectStandardError (Join-Path $logs "$mode.err.log")
    $null = $p.Handle
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) { $failed++ }
    $summary = '{0,-6} exit {1}  {2,6:F1} s  {3} file  dotnet {4}' -f $mode, $p.ExitCode, $sw.Elapsed.TotalSeconds, @(Get-ChildItem -LiteralPath $out -File).Count, $line
    Write-Host $summary
    Add-Content -LiteralPath (Join-Path $logs 'corse.txt') -Value $summary -Encoding UTF8
}
if ($failed -gt 0) { exit 1 }
exit 0
