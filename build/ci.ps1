<#
Single runner for the ANTHEA verifications (refactoring, phase F0).

  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1                          # profile quick
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Profile standard        # everything except the WPF smokes
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Profile full -Tag run0  # standard + WPF smokes (needs the desktop)
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Stage ui -Only 'smoke-bridge'
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Profile baseline -Tag B1 -BaselineRef supporto\artefatti\baseline\F0-B0\headless
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Profile baseline -Only '^baseline/banco' -DenseRef <F2-pre-m4-v2>\a\tutte

Stages: build, fast, regression, wiki, baseline, ui, word (word needs Microsoft Word, never in a profile).
Profiles: quick = build fast wiki; standard = quick + regression; baseline = standard + baseline; full = standard + ui.
Stage baseline: headless capture of the corpus with tests\ANTHEA.Testing (results, engines, report text, archives, fallbacks);
with -BaselineRef <capture folder> also its comparison with that reference capture (tests\ANTHEA.Testing\tolerances.json).
Bench of the concrete migration (F2.1, docs\refactoring\f2.1-banco.md), also in the stage baseline: dense capture of
supporto\test\CheckerMigration.Capture (mode tutte) compared with the concrete fixtures of Checker, recaptured on 7/10/2026 from
the dense capture F2-pre-m4-v2 (compare-dense, comparison 'fixture-checker' of tests\ANTHEA.Testing\f2-classificazione.json, no
expected difference). The fixtures folder is -CheckerFixtures, by default ..\Checker\GPCChecker.Test.Concrete\Fixtures next to this
checkout or next to the main working tree of a git worktree; without it the suite is listed as not run. With -DenseRef <dense
'tutte' folder> a second dense capture is compared with that reference (comparison -DenseSet, default 'f2-libreria' from step F2.6: the reference
supporto\artefatti\baseline\F2-pre-m4-v2\a\tutte of the main repository, with 1e-9 on the numbers of shear and torsion now computed by
GPCChecker.Concrete; 'pre-m4' is the exact comparison, for a capture with --motore legacy or a double run). The headless reference of
steps F2.6-F2.9 is supporto\artefatti\baseline\F2-B3\headless (-BaselineRef), with concrete sections with shear and torsion.
The WPF checks (--smoke-*, --check-*) exist only in the UiTests configuration (refactoring F1.2): the build stage
compiles X.Desktop with -c UiTests and the ui stage runs X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.exe.
Every outcome is classified against build/known-failures.json: PASS, KNOWN, NEW-FAIL, FIXED, BLOCKED, NOT-RUN.
Exit code 0 only without NEW-FAIL and without tracked files modified by the suites.
Outputs: supporto/artefatti/ci/<yyyyMMdd-HHmmss>-<Tag>/ (summary.json, summary.txt, one folder and log per suite).
#>
[CmdletBinding()]
param(
    [ValidateSet('quick', 'standard', 'baseline', 'full')] [string] $Profile = 'quick',
    [ValidateSet('build', 'fast', 'regression', 'wiki', 'baseline', 'ui', 'word')] [string[]] $Stage,
    [string] $Tag = 'run',
    [string] $Output,
    [switch] $NoBuild,
    [string] $GpcLibDir,
    [string] $CompareTo,
    [string] $Only,
    [string] $BaselineRef,
    [string] $CheckerFixtures,
    [string] $DenseRef,
    [string] $DenseSet = 'f2-libreria'
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $Root

if (-not $Stage) {
    $Stage = switch ($Profile) {
        'quick' { @('build', 'fast', 'wiki') }
        'standard' { @('build', 'fast', 'regression', 'wiki') }
        'baseline' { @('build', 'fast', 'regression', 'wiki', 'baseline') }
        'full' { @('build', 'fast', 'regression', 'wiki', 'ui') }
    }
}
if ($NoBuild) { $Stage = @($Stage | Where-Object { $_ -ne 'build' }) }

function Find-Git {
    $cmd = Get-Command git -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $vs = 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
    if (Test-Path -LiteralPath $vs) { return $vs }
    return $null
}
$Git = Find-Git

if (-not $Output) { $Output = Join-Path $Root ('supporto\artefatti\ci\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Tag) }
$Output = [IO.Path]::GetFullPath($Output)
if ((Test-Path -LiteralPath $Output) -and (Get-ChildItem -LiteralPath $Output -Force | Select-Object -First 1)) { throw "Cartella di output non vuota: $Output" }
New-Item -ItemType Directory -Force -Path $Output | Out-Null

$Cases = Join-Path $Root 'supporto\test\casi_confronto.json'
$Exe = Join-Path $Root 'X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.exe'
$Desktop = 'X.Desktop\X.Desktop.csproj'
$Verifiche = 'supporto\test\X.Verifiche\X.Verifiche.csproj'
function TestProject([string] $name) { "supporto\test\$name\$name.csproj" }

# ---------------------------------------------------------------- suite registry
# Kind: run (dotnet exec of the built Project assembly), smoke/check (flags of the UiTests ANTHEA.exe), python,
# powershell (Script with Args), build (build only). Builds: extra @{ Project; Configuration } for the build stage.
# Args placeholders: {cases}, {out} (the suite folder). Proof: file + regex that must exist after the run.
$Suites = New-Object System.Collections.ArrayList
function Add-Suite([hashtable] $s) { [void] $Suites.Add($s) }

# The shipped assemblies (Release) must not contain the WPF checks of the UiTests configuration.
Add-Suite @{ Name = 'qa/no-test-code'; Stage = 'fast'; Kind = 'powershell'; Script = 'tools\qa\Assert-NoTestCode.ps1'; Args = @('-Path', 'X.Desktop\bin\Release\net8.0-windows')
    Builds = @(@{ Project = $Desktop; Configuration = 'Release' }) }

foreach ($flag in 'checker', 'bridge', 'horizontal', 'coesione', 'gamma-sat', 'pile-factors', 'ca-module', 'ca-data') {
    Add-Suite @{ Name = "verifiche/$flag"; Stage = 'fast'; Kind = 'run'; Project = $Verifiche; Args = @("--$flag") }
}
Add-Suite @{ Name = 'verifiche/micropalo'; Stage = 'fast'; Kind = 'run'; Project = $Verifiche; Args = @('--micropalo', '{cases}') }
Add-Suite @{ Name = 'CalculationLibrary.Checks'; Stage = 'fast'; Kind = 'run'; Project = (TestProject 'CalculationLibrary.Checks'); Args = @() }
Add-Suite @{ Name = 'ConcreteCode.Checks'; Stage = 'fast'; Kind = 'run'; Project = (TestProject 'ConcreteCode.Checks'); Args = @() }
# Adapter of shear and torsion to GPCChecker.Concrete (refactoring F2.5-F2.6): mapping layer, switch, legacy-library equivalence.
Add-Suite @{ Name = 'ConcreteLibraryAdapter.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'; Args = @('{out}')
    Proof = @{ File = '{out}\misura.json'; Pattern = '"strumento": "ConcreteLibraryAdapter.Checks"' } }
# F2.1 bench: legacy against GPCChecker.Concrete near the branch thresholds and on closed-form sections (independent Python
# expected values, regenerated and compared by the second suite).
Add-Suite @{ Name = 'ConcreteBench.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ConcreteBench.Checks\ConcreteBench.Checks.csproj'; Args = @('{out}')
    Proof = @{ File = '{out}\misura.json'; Pattern = '"strumento": "ConcreteBench.Checks"' } }
Add-Suite @{ Name = 'ConcreteBench.Checks/attesi'; Stage = 'fast'; Kind = 'python'; Script = 'tests\ConcreteBench.Checks\attesi\genera_attesi.py'; Args = @('--check') }

Add-Suite @{ Name = 'verifiche/regressione'; Stage = 'regression'; Kind = 'run'; Project = $Verifiche; Args = @('{cases}', '{out}\confronto_numerico.json') }
Add-Suite @{ Name = 'verifiche/software'; Stage = 'regression'; Kind = 'run'; Project = $Verifiche; Args = @('--software', '{cases}', '{out}\avanzamento.txt'); Proof = @{ File = '{out}\avanzamento.txt'; Pattern = '^Completato: ' } }
Add-Suite @{ Name = 'verifiche/project-audit'; Stage = 'regression'; Kind = 'run'; Project = $Verifiche; Args = @('--project-audit') }
Add-Suite @{ Name = 'verifiche/project-calculations'; Stage = 'regression'; Kind = 'run'; Project = $Verifiche; Args = @('--project-calculations', '{out}') }
Add-Suite @{ Name = 'verifiche/ca-benchmark'; Stage = 'regression'; Kind = 'run'; Project = $Verifiche; Args = @('--ca-benchmark', '{out}') }
Add-Suite @{ Name = 'verifiche/audit-benchmark'; Stage = 'regression'; Kind = 'run'; Project = $Verifiche; Args = @('--audit-benchmark', '{out}\benchmark.json') }
Add-Suite @{ Name = 'ConcreteDesign.Checks'; Stage = 'regression'; Kind = 'run'; Project = (TestProject 'ConcreteDesign.Checks'); Args = @() }
foreach ($name in 'BridgeDesign.Checks', 'BridgeDesign.IndependentChecks', 'GlobalStability.Checks', 'RetainingWall.Checks') {
    Add-Suite @{ Name = $name; Stage = 'regression'; Kind = 'run'; Project = (TestProject $name); Args = @('{out}') }
}

Add-Suite @{ Name = 'wiki/indice'; Stage = 'wiki'; Kind = 'python'; Script = 'supporto\scripts\wiki\build-wiki-index.py'; Args = @('--check') }
Add-Suite @{ Name = 'wiki/manuale'; Stage = 'wiki'; Kind = 'python'; Script = 'supporto\test\wiki-handbook-checks.py'; Args = @() }

# Placeholders of the baseline suites: {root} repository, {output} run folder, {tag}, {commit}.
$Testing = 'tests\ANTHEA.Testing\ANTHEA.Testing.csproj'
Add-Suite @{ Name = 'baseline/cattura'; Stage = 'baseline'; Kind = 'run'; Project = $Testing; Timeout = 3600
    Args = @('capture', '{out}\cattura', '--root', '{root}', '--tag', '{tag}', '--commit', '{commit}')
    Proof = @{ File = '{out}\cattura\manifest.json'; Pattern = '"strumento": "ANTHEA.Testing"' } }
if ($BaselineRef) {
    Add-Suite @{ Name = 'baseline/confronto'; Stage = 'baseline'; Kind = 'run'; Project = $Testing; Timeout = 1800
        Args = @('compare', $(if ([IO.Path]::IsPathRooted($BaselineRef)) { $BaselineRef } else { Join-Path $Root $BaselineRef }), '{output}\baseline_cattura\cattura', '--report', '{out}\confronto.json') }
}

# Bench of the concrete migration (F2.1): the Before step writes the dense capture (mode tutte) in {out}\cattura, then compare-dense.
$Dense = 'supporto\test\CheckerMigration.Capture\CheckerMigration.Capture.csproj'
$benchNotRun = @()
if (-not $CheckerFixtures) {
    $candidates = @(Join-Path $Root '..\Checker\GPCChecker.Test.Concrete\Fixtures')
    if ($Git) {
        # In a git worktree the libraries sit next to the main working tree, not next to the worktree.
        try { $common = (& $Git -C $Root rev-parse --path-format=absolute --git-common-dir | Select-Object -First 1) } catch { $common = $null }
        if ($common) { $candidates += Join-Path (Split-Path ([IO.Path]::GetFullPath($common)) -Parent) '..\Checker\GPCChecker.Test.Concrete\Fixtures' }
    }
    $CheckerFixtures = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ($CheckerFixtures) {
    $CheckerFixtures = [IO.Path]::GetFullPath($CheckerFixtures)
    Add-Suite @{ Name = 'baseline/banco-ca'; Stage = 'baseline'; Kind = 'run'; Project = $Testing; Timeout = 1800
        Before = @{ Project = $Dense; Args = @('{out}\cattura', '{commit}', 'tutte', '--manifest') }; Builds = @(@{ Project = $Dense; Configuration = 'Release' })
        Args = @('compare-dense', $CheckerFixtures, '{out}\cattura', '--confronto', 'fixture-checker', '--report', '{out}\confronto-fixture-checker.json')
        Proof = @{ File = '{out}\confronto-fixture-checker.json'; Pattern = '"esito": "differenze tutte ammesse o classificate"' } }
} else {
    $benchNotRun += @{ Name = 'baseline/banco-ca'; Reason = 'fixture di Checker non trovate: passare -CheckerFixtures <Checker>\GPCChecker.Test.Concrete\Fixtures' }
}
if ($DenseRef) {
    Add-Suite @{ Name = 'baseline/banco-denso'; Stage = 'baseline'; Kind = 'run'; Project = $Testing; Timeout = 1800
        Before = @{ Project = $Dense; Args = @('{out}\cattura', '{commit}', 'tutte', '--manifest') }; Builds = @(@{ Project = $Dense; Configuration = 'Release' })
        Args = @('compare-dense', $(if ([IO.Path]::IsPathRooted($DenseRef)) { $DenseRef } else { Join-Path $Root $DenseRef }), '{out}\cattura', '--confronto', $DenseSet, '--report', '{out}\confronto-denso.json')
        Proof = @{ File = '{out}\confronto-denso.json'; Pattern = '"esito": "differenze tutte ammesse o classificate"' } }
}

$smokes = 'smoke', 'smoke-bridge', 'smoke-bridge-curves', 'smoke-bridge-design', 'smoke-bridge-predalle', 'smoke-ca-extensions', 'smoke-ca-features',
    'smoke-display', 'smoke-global-stability', 'smoke-hierarchy', 'smoke-horizontal', 'smoke-material-report', 'smoke-materials', 'smoke-neutral-axis',
    'smoke-project-report', 'smoke-projects', 'smoke-project-workspace', 'smoke-retaining-wall', 'smoke-sharing', 'smoke-steel'
foreach ($s in $smokes) {
    $smokeArgs = if ($s -eq 'smoke') { @("--$s", '{out}', '{cases}') } else { @("--$s", '{out}') }
    Add-Suite @{ Name = "ui/$s"; Stage = 'ui'; Kind = 'smoke'; Args = $smokeArgs; Timeout = 900; Proof = @{ File = '{out}\esito-smoke-completo.txt'; Pattern = "^Completato: --$s$" } }
}
foreach ($c in 'check-appearance', 'check-global-guidance-offscreen', 'check-wall-advanced-offscreen', 'check-wall-materials-offscreen') {
    Add-Suite @{ Name = "ui/$c"; Stage = 'ui'; Kind = 'check'; Args = @("--$c", '{out}'); Timeout = 600 }
}
Add-Suite @{ Name = 'ui/check-error-log-offscreen'; Stage = 'ui'; Kind = 'check'; Args = @('--check-error-log-offscreen', '{out}'); Timeout = 300; Proof = @{ File = '{out}\test.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/check-wiki-offscreen'; Stage = 'ui'; Kind = 'check'; Args = @('--check-wiki-offscreen', '{out}'); Timeout = 600; Proof = @{ File = '{out}\exit-code.txt'; Pattern = '^0\s*$' } }
foreach ($name in 'HorizontalPileGroup.Checks', 'ElasticPile.UiChecks', 'ConcreteShort.UiChecks') {
    Add-Suite @{ Name = $name; Stage = 'ui'; Kind = 'run'; Project = (TestProject $name); Args = @('{out}'); Timeout = 900 }
}
# Formerly ConcreteDesign.DesktopChecks (moved to supporto/SUPERATI/test in F1.6): ConcreteDesign.Checks writes ui-fixture.json first.
Add-Suite @{ Name = 'ui/check-concrete-design'; Stage = 'ui'; Kind = 'check'; Args = @('--check-concrete-design', '{out}'); Timeout = 900
    Before = @{ Project = (TestProject 'ConcreteDesign.Checks'); Args = @('{out}') }; Builds = @(@{ Project = (TestProject 'ConcreteDesign.Checks'); Configuration = 'Release' })
    Proof = @{ File = '{out}\ui-pass.txt'; Pattern = '^PASS' } }
Add-Suite @{ Name = 'ConcreteShort.Checks'; Stage = 'word'; Kind = 'run'; Project = (TestProject 'ConcreteShort.Checks'); Args = @('{out}'); Timeout = 900 }

# Projects of supporto\test that the runner does not execute. The external comparisons and one-off tools (MAX, web
# site, stress diagnosis, validation of 25/9) were moved to supporto\SUPERATI\test in F1.6.
$NotRun = @(
    @{ Name = 'ElasticPile.Checks'; Reason = 'compila progetti del working tree di Checker; passa nei test di libreria (traccia infrastruttura)' },
    @{ Name = 'BridgeValidationCurrent'; Reason = 'compila i sorgenti di test di Checker; passa nei test di libreria (traccia infrastruttura)' },
    @{ Name = 'ElasticPile.Performance'; Reason = 'misura dei tempi con input in supporto/artefatti' }
) + $benchNotRun

# ---------------------------------------------------------------- helpers
function Expand([string] $text, [string] $out) {
    $text.Replace('{cases}', $Cases).Replace('{out}', $out).Replace('{output}', $Output).Replace('{root}', $Root).Replace('{tag}', $Tag).Replace('{commit}', $commit)
}
function Quote([string] $a) { if ($a -match '[\s"]') { '"' + $a.Replace('"', '\"') + '"' } else { $a } }

# Suites run with dotnet exec of the built assembly, not dotnet run: the apphost .exe changes hash at every commit
# (SourceLink version resources) and the antivirus on this machine can refuse to start it (Win32Exception 5, 6/10/2026).
function Target-Path([string] $project) {
    (& dotnet msbuild (Join-Path $Root $project) -getProperty:TargetPath -p:Configuration=Release -nologo | Select-Object -Last 1).Trim()
}

function Invoke-Process([string] $file, [string[]] $arguments, [string] $logBase, [int] $timeout, [switch] $Window) {
    $argLine = ($arguments | ForEach-Object { Quote $_ }) -join ' '
    $sw = [Diagnostics.Stopwatch]::StartNew()
    if ($Window) {
        $p = Start-Process -FilePath $file -ArgumentList $argLine -WorkingDirectory $Root -PassThru
    } else {
        $p = Start-Process -FilePath $file -ArgumentList $argLine -WorkingDirectory $Root -PassThru -NoNewWindow `
            -RedirectStandardOutput "$logBase.out.log" -RedirectStandardError "$logBase.err.log"
    }
    $null = $p.Handle  # keeps ExitCode available after the exit
    $timedOut = -not $p.WaitForExit($timeout * 1000)
    if ($timedOut) { & taskkill.exe /T /F /PID $p.Id | Out-Null; $p.WaitForExit() }
    $sw.Stop()
    [pscustomobject]@{ ExitCode = $(if ($timedOut) { -1 } else { $p.ExitCode }); TimedOut = $timedOut; Seconds = [Math]::Round($sw.Elapsed.TotalSeconds, 1) }
}

function First-Line([string] $path) {
    if (Test-Path -LiteralPath $path) {
        $line = Get-Content -LiteralPath $path -Encoding UTF8 | Where-Object { $_.Trim() } | Select-Object -First 1
        if ($line) { return ([regex]::Replace([string] $line, '\s+', ' ')).Trim() }
    }
    return ''
}

function Tracked-Status {
    if (-not $Git) { return @() }
    @(& $Git -C $Root status --porcelain=v1 --untracked-files=normal 2>$null)
}

# ---------------------------------------------------------------- known failures
$knownPath = Join-Path $Root 'build\known-failures.json'
$Known = @()
if (Test-Path -LiteralPath $knownPath) { $Known = @((Get-Content -LiteralPath $knownPath -Raw -Encoding UTF8 | ConvertFrom-Json).entries) }
function Known-Entry([string] $suite) { $Known | Where-Object { $_.suite -eq $suite } | Select-Object -First 1 }

# ---------------------------------------------------------------- run
$selected = @($Suites | Where-Object { $Stage -contains $_.Stage -and (-not $Only -or $_.Name -match $Only) })
$results = New-Object System.Collections.ArrayList
$statusBefore = Tracked-Status
$commit = if ($Git) { (& $Git -C $Root rev-parse HEAD).Trim() } else { '' }
$manifest = Join-Path $Root 'lib\Checker\manifest.json'
$started = Get-Date

function Say([string] $text) { Write-Host $text; Add-Content -LiteralPath (Join-Path $Output 'summary.txt') -Value $text -Encoding UTF8 }
Say ("ANTHEA ci  profilo {0}  stadi {1}  tag {2}" -f $Profile, ($Stage -join ','), $Tag)
Say ("commit {0}  output {1}" -f $commit, $Output)

# Build stage: every project needed by the selected suites, X.Desktop first. The WPF checks need the UiTests
# configuration of X.Desktop; every other project is built in Release (build/<name> or build/<name>.<configuration>).
$buildFailed = @{}
function Build-Key([string] $project, [string] $configuration) { "$project|$configuration" }
if ($Stage -contains 'build') {
    $builds = New-Object System.Collections.ArrayList
    function Add-Build([string] $project, [string] $configuration) {
        if (-not ($builds | Where-Object { $_.Project -eq $project -and $_.Configuration -eq $configuration })) { [void] $builds.Add([pscustomobject]@{ Project = $project; Configuration = $configuration }) }
    }
    if ($selected | Where-Object { $_.Kind -in 'smoke', 'check' }) { Add-Build $Desktop 'UiTests' }
    foreach ($s in $selected) { foreach ($b in @($s.Builds)) { if ($b) { Add-Build $b.Project $b.Configuration } } }
    # Build-only suites compile their own project (and record the result) in the suite loop.
    foreach ($s in $selected) { if ($s.Project -and $s.Kind -ne 'build') { Add-Build $s.Project 'Release' } }
    $buildDir = Join-Path $Output 'build'
    New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
    foreach ($b in $builds) {
        $p = $b.Project
        $name = [IO.Path]::GetFileNameWithoutExtension($p) + $(if ($b.Configuration -ne 'Release') { '.' + $b.Configuration } else { '' })
        $arguments = @('build', (Join-Path $Root $p), '-c', $b.Configuration, '-nologo', '-v:minimal')
        if ($GpcLibDir) { $arguments += "-p:GpcLibDir=$GpcLibDir" }
        $r = Invoke-Process 'dotnet' $arguments (Join-Path $buildDir $name) 1800
        $note = ''
        if ($r.ExitCode -ne 0) {
            $buildFailed[(Build-Key $p $b.Configuration)] = $true
            $note = (Select-String -LiteralPath (Join-Path $buildDir "$name.out.log") -Pattern 'error [A-Z]+\d+' | Select-Object -First 1 | ForEach-Object { $_.Line.Trim() })
        }
        [void] $results.Add([pscustomobject]@{ Suite = "build/$name"; Stage = 'build'; Raw = $(if ($r.ExitCode -eq 0) { 'PASS' } else { 'FAIL' }); ExitCode = $r.ExitCode; Seconds = $r.Seconds; Note = [string] $note; Counts = @() })
        Say ("{0,-46} {1,-4} {2,7:F1} s {3}" -f "build/$name", $(if ($r.ExitCode -eq 0) { 'ok' } else { 'FAIL' }), $r.Seconds, $note)
        if ($r.ExitCode -ne 0 -and ($p -eq $Desktop -or $p -eq $Verifiche) -and -not (Known-Entry "build/$name")) {
            Say "Build di $name fallita: le suite non vengono eseguite."
            $selected = @()
            break
        }
    }
}

foreach ($s in $selected) {
    $out = Join-Path $Output ($s.Name -replace '[\\/:]', '_')
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $timeout = if ($s.Timeout) { $s.Timeout } else { 1800 }
    $raw = 'PASS'; $note = ''; $counts = @(); $code = 0; $seconds = 0
    if ($s.Kind -eq 'build') {
        $projectName = [IO.Path]::GetFileNameWithoutExtension($s.Project)
        $r = Invoke-Process 'dotnet' @('build', (Join-Path $Root $s.Project), '-c', 'Release', '-nologo', '-v:minimal') (Join-Path $out 'build') 1800
        $code = $r.ExitCode; $seconds = $r.Seconds
        if ($code -ne 0) { $raw = 'FAIL'; $note = (Select-String -LiteralPath (Join-Path $out 'build.out.log') -Pattern 'error [A-Z]+\d+' | Select-Object -First 1 | ForEach-Object { $_.Line.Trim() }) }
    } elseif (($s.Project -and $buildFailed.ContainsKey((Build-Key $s.Project 'Release'))) -or
        ($s.Kind -in 'smoke', 'check' -and $buildFailed.ContainsKey((Build-Key $Desktop 'UiTests'))) -or
        @($s.Builds | Where-Object { $_ -and $buildFailed.ContainsKey((Build-Key $_.Project $_.Configuration)) }).Count -gt 0) {
        $raw = 'BLOCKED'; $note = 'build del progetto fallita'
    } else {
        $arguments = @($s.Args | ForEach-Object { Expand $_ $out })
        $prepared = $true
        if ($s.Before) {
            # Preparation step (dotnet exec of Before.Project): its failure is the failure of the suite.
            $before = Invoke-Process 'dotnet' (@('exec', (Target-Path $s.Before.Project)) + @($s.Before.Args | ForEach-Object { Expand $_ $out })) (Join-Path $out 'before') $timeout
            $prepared = $before.ExitCode -eq 0
        }
        if (-not $prepared) {
            $r = [pscustomobject]@{ ExitCode = $before.ExitCode; TimedOut = $before.TimedOut; Seconds = $before.Seconds }
            $note = 'preparazione fallita: ' + [IO.Path]::GetFileNameWithoutExtension($s.Before.Project)
        } elseif ($s.Kind -eq 'run') {
            $r = Invoke-Process 'dotnet' (@('exec', (Target-Path $s.Project)) + $arguments) (Join-Path $out 'run') $timeout
        } elseif ($s.Kind -eq 'python') {
            $r = Invoke-Process 'py' (@('-3', (Join-Path $Root $s.Script)) + $arguments) (Join-Path $out 'run') $timeout
        } elseif ($s.Kind -eq 'powershell') {
            $r = Invoke-Process 'powershell' (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $Root $s.Script)) + $arguments) (Join-Path $out 'run') $timeout
        } else {
            $r = Invoke-Process $Exe $arguments (Join-Path $out 'run') $timeout -Window
        }
        $code = $r.ExitCode; $seconds = $r.Seconds
        $errorFile = Join-Path $out 'errore.txt'
        if ($r.TimedOut) { $raw = 'FAIL'; $note = "timeout dopo $timeout s" }
        elseif ($code -ne 0) { $raw = 'FAIL' }
        if (Test-Path -LiteralPath $errorFile) { $raw = 'FAIL'; if (-not $note) { $note = First-Line $errorFile } }
        if ($s.Proof) {
            $proof = Expand $s.Proof.File $out
            if (-not (Test-Path -LiteralPath $proof) -or -not (Select-String -LiteralPath $proof -Pattern $s.Proof.Pattern -Quiet)) {
                $raw = 'FAIL'; if (-not $note) { $note = 'attestazione finale assente: ' + [IO.Path]::GetFileName($proof) }
            }
        }
        $logs = @("$out\run.out.log", "$out\run.err.log") | Where-Object { Test-Path -LiteralPath $_ }
        if ($logs) {
            $counts = @(Select-String -LiteralPath $logs -Pattern 'superat|PASS' | Select-Object -First 30 | ForEach-Object { $_.Line.Trim() })
            if ($raw -eq 'FAIL' -and -not $note) {
                $note = (Select-String -LiteralPath $logs -Pattern '^(FAIL|ERROR):|AssertionError|Exception:|Unhandled exception' | Select-Object -First 1 | ForEach-Object { $_.Line.Trim() })
            }
            if ($raw -eq 'FAIL' -and -not $note) {
                $note = (Get-Content -LiteralPath "$out\run.err.log" -Encoding UTF8 -ErrorAction SilentlyContinue | Where-Object { $_.Trim() } | Select-Object -First 1)
                if (-not $note) { $note = (Get-Content -LiteralPath "$out\run.out.log" -Encoding UTF8 -ErrorAction SilentlyContinue | Where-Object { $_.Trim() } | Select-Object -Last 1) }
            }
        }
    }
    [void] $results.Add([pscustomobject]@{ Suite = $s.Name; Stage = $s.Stage; Raw = $raw; ExitCode = $code; Seconds = $seconds; Note = ([regex]::Replace([string] $note, '\s+', ' ')).Trim(); Counts = $counts })
    Say ("{0,-46} {1,-7} {2,7:F1} s {3}" -f $s.Name, $raw, $seconds, $(if ($note) { ([string] $note).Substring(0, [Math]::Min(160, ([string] $note).Length)) } else { '' }))
}

$statusAfter = Tracked-Status
$touched = @(Compare-Object -ReferenceObject @($statusBefore) -DifferenceObject @($statusAfter) -PassThru -ErrorAction SilentlyContinue | Where-Object { $_ })

# ---------------------------------------------------------------- classification
$final = foreach ($r in $results) {
    $entry = Known-Entry $r.Suite
    $status = switch ($r.Raw) {
        'PASS' { if ($entry) { 'FIXED' } else { 'PASS' } }
        'BLOCKED' { 'BLOCKED' }
        default {
            if ($entry -and (-not $entry.match -or $r.Note -match $entry.match)) { 'KNOWN' } else { 'NEW-FAIL' }
        }
    }
    $r | Add-Member -NotePropertyName Status -NotePropertyValue $status -PassThru
}

$warnings = New-Object System.Collections.ArrayList
if ($CompareTo) {
    $previous = (Get-Content -LiteralPath $CompareTo -Raw -Encoding UTF8 | ConvertFrom-Json).results
    foreach ($r in $final) {
        $old = $previous | Where-Object { $_.Suite -eq $r.Suite } | Select-Object -First 1
        if (-not $old) { continue }
        if ($old.Status -in 'PASS', 'FIXED' -and $r.Status -notin 'PASS', 'FIXED') { [void] $warnings.Add("$($r.Suite): era $($old.Status), ora $($r.Status)") }
        # Durations ('5.08s', '12,3 s') change from run to run: mask them before comparing the count lines.
        $mask = { param($lines) (@($lines) | ForEach-Object { [regex]::Replace([string] $_, '\d+([.,]\d+)?\s?(ms|s)\b', '<t>') }) -join "`n" }
        $oldCounts = & $mask $old.Counts; $newCounts = & $mask $r.Counts
        if ($oldCounts -ne $newCounts) { [void] $warnings.Add("$($r.Suite): righe di conteggio cambiate") }
    }
}

$newFails = @($final | Where-Object { $_.Status -eq 'NEW-FAIL' })
$fixed = @($final | Where-Object { $_.Status -eq 'FIXED' })
Say ''
Say ('PASS {0}  KNOWN {1}  NEW-FAIL {2}  FIXED {3}  BLOCKED {4}' -f @($final | Where-Object { $_.Status -eq 'PASS' }).Count, @($final | Where-Object { $_.Status -eq 'KNOWN' }).Count, $newFails.Count, $fixed.Count, @($final | Where-Object { $_.Status -eq 'BLOCKED' }).Count)
foreach ($r in $newFails) { Say ("NEW-FAIL {0}: {1}" -f $r.Suite, $r.Note) }
foreach ($r in $fixed) { Say ("FIXED    {0}: togliere la voce da build/known-failures.json" -f $r.Suite) }
foreach ($w in $warnings) { Say ("ATTENZIONE {0}" -f $w) }
if ($touched.Count -gt 0) { Say 'File tracciati modificati durante la corsa:'; $touched | ForEach-Object { Say "  $_" } }
Say 'Non eseguite:'
foreach ($n in $NotRun) { Say ("  {0,-30} {1}" -f $n.Name, $n.Reason) }

$summary = [ordered]@{
    tag = $Tag; profile = $Profile; stages = $Stage; only = $Only; commit = $commit
    dirtyBefore = @($statusBefore).Count -gt 0
    gpcLibDir = $GpcLibDir
    libManifestSha256 = $(if (Test-Path -LiteralPath $manifest) { (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash } else { '' })
    dotnet = (& dotnet --version).Trim()
    started = $started.ToString('s'); seconds = [Math]::Round(((Get-Date) - $started).TotalSeconds, 1)
    results = @($final); notRun = $NotRun; trackedModified = $touched; warnings = @($warnings)
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Output 'summary.json') -Encoding UTF8
if ($newFails.Count -gt 0 -or $touched.Count -gt 0) { exit 1 }
exit 0
