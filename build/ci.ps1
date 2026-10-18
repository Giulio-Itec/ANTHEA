<#
Single runner for the ANTHEA verifications (refactoring, phase F0).

  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1                          # profile quick
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Profile standard        # everything except the WPF smokes
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Profile full -Tag run0  # standard + WPF smokes (needs the desktop)
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Stage ui -Only 'smoke-bridge'
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Profile baseline -Tag B1 -BaselineRef supporto\artefatti\baseline\F0-B0\headless
  powershell -NoProfile -ExecutionPolicy Bypass -File build\ci.ps1 -Profile baseline -Only '^baseline/banco' -DenseRef <F2-pre-m4-v2>\a\tutte

Stages: build, fast, regression, wiki, baseline, ui. The stage word was removed on 14/10/2026: reports are written without Word.
Profiles: quick = build fast wiki; standard = quick + regression; baseline = standard + baseline; full = standard + ui.
Stage baseline: headless capture of the corpus with tests\ANTHEA.Testing (results, engines, report text, archives, fallbacks);
with -BaselineRef <capture folder> also its comparison with that reference capture (tests\ANTHEA.Testing\tolerances.json).
With -CaptureCandidate <existing capture> only compare that immutable capture, without recalculating the corpus. Requires -BaselineRef.
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
With -Installer the fast stage also runs isolated NSIS installation/cleanup checks (requires NSIS 3).
#>
[CmdletBinding()]
param(
    [ValidateSet('quick', 'standard', 'baseline', 'full')] [string] $Profile = 'quick',
    [ValidateSet('build', 'fast', 'regression', 'wiki', 'baseline', 'ui')] [string[]] $Stage,
    [string] $Tag = 'run',
    [string] $Output,
    [switch] $NoBuild,
    [switch] $Installer,
    [string] $GpcLibDir,
    [string] $CompareTo,
    [string] $Only,
    [string] $BaselineRef,
    [string] $CaptureCandidate,
    [string] $F4CaptureCandidate,
    [string] $F4CandidateConcrete,
    [switch] $F4GravityCapture,
    [switch] $F4DetailCapture,
    [switch] $F4DetailAdapter,
    [switch] $F4StructuralAdapter,
    [switch] $F4GravityAdapter,
    [switch] $F4PileOutcomes,
    [switch] $F4PileDesign,
    [switch] $F4WallDesign,
    [switch] $F4SectionDesign,
    [switch] $F4InputContractCapture,
    [switch] $F4InputContracts,
    [switch] $F4ResidualCapture,
    [switch] $F4LastAdaptersCapture,
    [switch] $F4LastAdapters,
    [switch] $F4Residual,
    [switch] $F4BridgeCapture,
    [switch] $F4Bridge,
    [switch] $CaptureConcreteFigures,
    [string] $CheckerFixtures,
    [string] $DenseRef,
    [string] $ConcurrencyRef,
    [string] $DenseSet = 'f2-libreria'
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $Root
if ($CaptureCandidate) {
    if (-not $BaselineRef) { throw 'CaptureCandidate richiede BaselineRef.' }
    $CaptureCandidate = [IO.Path]::GetFullPath($CaptureCandidate)
    if (-not (Test-Path -LiteralPath (Join-Path $CaptureCandidate 'manifest.json') -PathType Leaf)) { throw "Cattura candidata senza manifest: $CaptureCandidate" }
}

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
if ($Installer) {
    Add-Suite @{ Name = 'installer/safety'; Stage = 'fast'; Kind = 'powershell'; Script = 'tests\Installer\Test-Safety.ps1'; Args = @('-Output', '{out}'); Timeout = 180; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
    Add-Suite @{ Name = 'installer/guides'; Stage = 'fast'; Kind = 'powershell'; Script = 'supporto\test\installer\Test-Guide.ps1'; Args = @(); Timeout = 180 }
}
Add-Suite @{ Name = 'ModelWorkspace.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ModelWorkspace.Checks\ModelWorkspace.Checks.csproj'; Args = @('{out}'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ModelViewer.CombinationBench.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ModelViewer.CombinationBench.Checks\ModelViewer.CombinationBench.Checks.csproj'; Args = @('{out}'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'Architecture.Tests'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ANTHEA.Architecture.Tests\ANTHEA.Architecture.Tests.csproj'; Args = @('{out}') + $(if ($GpcLibDir) { @('--gpc-snapshot', [IO.Path]::GetFullPath($GpcLibDir)) }); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' }; Builds = @(@{ Project = $Desktop; Configuration = 'Release' }) }
Add-Suite @{ Name = 'Reports.Tests'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ANTHEA.Reports.Tests\ANTHEA.Reports.Tests.csproj'; Args = @('{out}'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'Application.Tests'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ANTHEA.Application.Tests\ANTHEA.Application.Tests.csproj'; Args = @('{out}'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'Wiki.Tests'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ANTHEA.Wiki.Tests\ANTHEA.Wiki.Tests.csproj'; Args = @('{out}'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
# ViewModels, services and calculation scheduling of the MVVM interface without WPF (refactoring F5.1, proposal F5 section 7.1).
Add-Suite @{ Name = 'Presentation.Tests'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ANTHEA.Presentation.Tests\ANTHEA.Presentation.Tests.csproj'; Args = @('{out}'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'Spettri.Tests'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ANTHEA.Presentation.Tests\ANTHEA.Presentation.Tests.csproj'; Args = @('{out}', '--spettri'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'Architecture.F4.Inventory'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ANTHEA.Architecture.Tests\ANTHEA.Architecture.Tests.csproj'; Args = @('{out}', '--f4-inventory'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS F4 inventory:' } }
Add-Suite @{ Name = 'Testing.Tests'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ANTHEA.Testing.Tests\ANTHEA.Testing.Tests.csproj'; Args = @() }
Add-Suite @{ Name = 'Headless/help'; Stage = 'fast'; Kind = 'run'; Project = 'tools\ANTHEA.Headless\ANTHEA.Headless.csproj'; Args = @('--help') }
Add-Suite @{ Name = 'ModelViewer.UiChecks'; Stage = 'ui'; Kind = 'run'; Project = 'tests\ModelViewer.UiChecks\ModelViewer.UiChecks.csproj'; Args = @('{out}'); Timeout = 180; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'Stratigraphy.UiChecks'; Stage = 'ui'; Kind = 'run'; Project = 'tests\ModelViewer.UiChecks\ModelViewer.UiChecks.csproj'; Args = @('{out}', '--stratigraphy'); Timeout = 120; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS \d+ controlli stratigrafia' } }
Add-Suite @{ Name = 'ModelViewer.Combinations.UiChecks'; Stage = 'ui'; Kind = 'run'; Project = 'tests\ModelViewer.UiChecks\ModelViewer.UiChecks.csproj'; Args = @('{out}', '--viewer-only', '--combinations'); Timeout = 180; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ModelViewer.LiveImport.UiChecks'; Stage = 'ui'; Kind = 'run'; Project = 'tests\ModelViewer.UiChecks\ModelViewer.UiChecks.csproj'; Args = @('{out}', '--viewer-only', '--live-import'); Timeout = 180; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ModelViewer.Sections.UiChecks'; Stage = 'ui'; Kind = 'run'; Project = 'tests\ModelViewer.UiChecks\ModelViewer.UiChecks.csproj'; Args = @('{out}', '--viewer-only', '--sections'); Timeout = 180; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ConcreteCode.Checks'; Stage = 'fast'; Kind = 'run'; Project = (TestProject 'ConcreteCode.Checks'); Args = @() }
Add-Suite @{ Name = 'FoundationWorkspace.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\FoundationWorkspace.Checks\FoundationWorkspace.Checks.csproj'; Args = @('{out}'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'FoundationWorkspace.UiChecks'; Stage = 'ui'; Kind = 'run'; Project = 'tests\FoundationWorkspace.Checks\FoundationWorkspace.Checks.csproj'; Args = @('{out}', '--ui'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
# Adapters to GPCChecker.Concrete (refactoring F2.5-F2.9): mapping layer, switches, legacy-library equivalence. The complete run
# (durability and SLE grids, about 2.5 minutes) is in the regression stage; the fast stage runs sections 1-4, the switches and
# section 13, the method-page examples (--rapido), user decision of 9/10.
Add-Suite @{ Name = 'ConcreteLibraryAdapter.Checks/rapido'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'; Args = @('{out}', '--rapido') }
Add-Suite @{ Name = 'ANTHEA.ApiReuse.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ANTHEA.ApiReuse.Checks\ANTHEA.ApiReuse.Checks.csproj'; Args = @() }
Add-Suite @{ Name = 'ConcreteTimeEffects.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ConcreteTimeEffects.Checks\ConcreteTimeEffects.Checks.csproj'; Args = @('{out}'); Timeout = 180 }
Add-Suite @{ Name = 'ConcreteTimeEffects.UiChecks'; Stage = 'ui'; Kind = 'run'; Project = 'tests\ConcreteTimeEffects.Checks\ConcreteTimeEffects.Checks.csproj'; Args = @('{out}', '--ui'); Timeout = 180 }
Add-Suite @{ Name = 'ConcreteInput.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ConcreteInput.Checks\ConcreteInput.Checks.csproj'; Args = @('{out}'); Timeout = 180 }
Add-Suite @{ Name = 'ConcreteInput.UiChecks'; Stage = 'ui'; Kind = 'run'; Project = 'tests\ConcreteInput.Checks\ConcreteInput.Checks.csproj'; Args = @('{out}', '--ui'); Timeout = 180 }

Add-Suite @{ Name = 'ConcreteLibraryAdapter.Checks'; Stage = 'regression'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'; Args = @('{out}')
    Proof = @{ File = '{out}\misura.json'; Pattern = '"strumento": "ConcreteLibraryAdapter.Checks"' } }
# F2.1 bench: legacy against GPCChecker.Concrete near the branch thresholds and on closed-form sections (independent Python
# expected values, regenerated and compared by the second suite).
Add-Suite @{ Name = 'ConcreteBench.Checks'; Stage = 'fast'; Kind = 'run'; Project = 'tests\ConcreteBench.Checks\ConcreteBench.Checks.csproj'; Args = @('{out}')
    Proof = @{ File = '{out}\misura.json'; Pattern = '"strumento": "ConcreteBench.Checks"' } }
Add-Suite @{ Name = 'ConcreteBench.Checks/attesi'; Stage = 'fast'; Kind = 'python'; Script = 'tests\ConcreteBench.Checks\attesi\genera_attesi.py'; Args = @('--check') }
# F2.8 (decision E3 of 15/10/2026): independent expected values of the Checker method-page examples (anchorages, detailing, M-chi) used by
# section 13 of ConcreteLibraryAdapter.Checks, regenerated by the Python script and compared with the versioned JSON.
Add-Suite @{ Name = 'ConcreteLibraryAdapter.Checks/attesi'; Stage = 'fast'; Kind = 'python'; Script = 'tests\ConcreteLibraryAdapter.Checks\attesi\pagine_metodi.py'; Args = @('--check') }
# F2.12 (decision E6 of 15/10/2026): Python oracle of the concrete sections (tools\attesi\sezione_ca.py, no ANTHEA code) on
# supporto\test\casi_confronto.json: --valida, bit-for-bit fidelity to the original program on the rectangular and circular cases;
# --confronta, the T sections with 'fonte_atteso' under the current side-bar rule.
Add-Suite @{ Name = 'attesi/sezione_ca-valida'; Stage = 'fast'; Kind = 'python'; Script = 'tools\attesi\sezione_ca.py'; Args = @('--valida', '{cases}') }
Add-Suite @{ Name = 'attesi/sezione_ca-confronta'; Stage = 'fast'; Kind = 'python'; Script = 'tools\attesi\sezione_ca.py'; Args = @('--confronta', '{cases}') }

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

if ($true) { # F4 comparisons always run in the baseline stage (audit of 14/10/2026); only the captures are opt-in
    Add-Suite @{ Name = 'baseline/f4-last-adapters'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @($(if ($F4LastAdaptersCapture) { '--f4-last-adapters-capture' } else { '--f4-last-adapters' }), '{root}', '{out}'); Timeout = 180 }
}
if ($F4ResidualCapture) {
    Add-Suite @{ Name = 'baseline/f4-residual-capture'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-residual-capture', '{root}', '{out}'); Timeout = 180 }
}
if (!$F4ResidualCapture) {
    Add-Suite @{ Name = 'baseline/f4-residual'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-residual', '{root}', '{out}'); Timeout = 180 }
}
if (!$F4BridgeCapture) {
    Add-Suite @{ Name = 'baseline/f4-bridge'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-bridge', '{root}', '{out}'); Timeout = 180 }
}
if ($F4BridgeCapture) {
    Add-Suite @{ Name = 'baseline/f4-bridge-capture'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-bridge-capture', '{root}', '{out}'); Timeout = 180 }
}
if (!$F4InputContractCapture) {
    Add-Suite @{ Name = 'baseline/f4-input-contracts'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-input-contracts', '{root}', '{out}'); Timeout = 180 }
}
if ($F4InputContractCapture) {
    Add-Suite @{ Name = 'baseline/f4-input-contract-capture'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-input-contract-capture', '{root}', '{out}'); Timeout = 120 }
}
if ($true) {
    Add-Suite @{ Name = 'baseline/f4-section-design'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-section-design', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 900 }
}
if ($true) {
    Add-Suite @{ Name = 'baseline/f4-wall-design'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-wall-design', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 900 }
}
if ($true) {
    Add-Suite @{ Name = 'baseline/f4-pile-design'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-pile-design', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 600 }
}
if ($true) {
    Add-Suite @{ Name = 'baseline/f4-pile-outcomes'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-pile-outcomes', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 600 }
}
if (!$F4GravityCapture) {
    Add-Suite @{ Name = 'baseline/f4-gravity-adapter'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-gravity-adapter', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 600 }
}
if ($true) {
    Add-Suite @{ Name = 'baseline/f4-structural-adapter'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-structures', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 600 }
}
if (!$F4DetailCapture) {
    Add-Suite @{ Name = 'baseline/f4-detail-adapter'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-details', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 120 }
}
if ($F4DetailCapture) {
    Add-Suite @{ Name = 'baseline/f4-detail-reference'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-detail-capture', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 120 }
}
if ($F4GravityCapture) {
    Add-Suite @{ Name = 'baseline/f4-gravity-reference'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'
        Args = @('--f4-gravity-capture', '{root}', (Join-Path $Root 'supporto\artefatti\refactoring\f4\before-port-1.7.1'), '{out}'); Timeout = 120 }
}
# The observed reference of the F4 comparisons is unversioned (37 MB): its files must match the versioned SHA-256 list.
Add-Suite @{ Name = 'baseline/f4-reference'; Stage = 'baseline'; Kind = 'powershell'; Script = 'tools\qa\Test-F4Reference.ps1'; Args = @('-Output', '{out}'); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
# Optional diagnostic candidate: no production snapshot replacement.
if ($F4CandidateConcrete) {
    Add-Suite @{ Name = 'baseline/f4-wall-port'; Stage = 'baseline'; Kind = 'powershell'; Script = 'tools\qa\Invoke-F4WallPort.ps1'
        Args = @('-Output', '{out}', '-CandidateConcrete', [IO.Path]::GetFullPath($F4CandidateConcrete)); Timeout = 1800
        Builds = @(@{ Project = 'tests\ConcreteLibraryAdapter.Checks\ConcreteLibraryAdapter.Checks.csproj'; Configuration = 'Release' }) }
}
# Placeholders of the baseline suites: {root} repository, {output} run folder, {tag}, {commit}.
$Testing = 'tests\ANTHEA.Testing\ANTHEA.Testing.csproj'
Add-Suite @{ Name = 'baseline/f4'; Stage = 'baseline'; Kind = 'powershell'; Script = 'tools\qa\Invoke-F4Capture.ps1'; Args = @('-Output', '{out}') + $(if ($F4CaptureCandidate) { @('-CapturedRoot', [IO.Path]::GetFullPath($F4CaptureCandidate)) }); Timeout = 1800
    Builds = @(@{ Project = $Testing; Configuration = 'Release' }); Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS F4:' } }
if (-not $CaptureCandidate) { Add-Suite @{ Name = 'baseline/cattura'; Stage = 'baseline'; Kind = 'run'; Project = $Testing; Timeout = 3600
    Args = @('capture', '{out}\cattura', '--root', '{root}', '--tag', '{tag}', '--commit', '{commit}')
    Proof = @{ File = '{out}\cattura\manifest.json'; Pattern = '"strumento": "ANTHEA.Testing"' } } }
if ($BaselineRef) {
    Add-Suite @{ Name = 'baseline/confronto'; Stage = 'baseline'; Kind = 'run'; Project = $Testing; Timeout = 1800
        Args = @('compare', $(if ([IO.Path]::IsPathRooted($BaselineRef)) { $BaselineRef } else { Join-Path $Root $BaselineRef }), $(if ($CaptureCandidate) { $CaptureCandidate } else { '{output}\baseline_cattura\cattura' }), '--report', '{out}\confronto.json') }
}

# Bench of the concrete migration (F2.1): the Before step writes the dense capture (mode tutte) in {out}\cattura, then compare-dense.
$Dense = 'supporto\test\CheckerMigration.Capture\CheckerMigration.Capture.csproj'
$concurrencyArgs = @('{out}')
if ($ConcurrencyRef) { $concurrencyArgs += @('--reference', $(if ([IO.Path]::IsPathRooted($ConcurrencyRef)) { $ConcurrencyRef } else { Join-Path $Root $ConcurrencyRef })) }
Add-Suite @{ Name = 'ConcreteConcurrency.Checks'; Stage = 'regression'; Kind = 'run'; Project = 'tests\ConcreteConcurrency.Checks\ConcreteConcurrency.Checks.csproj'; Timeout = 600
    Before = @{ Project = $Dense; Args = @('{out}\meshes', '{commit}', 'mesh') }; Builds = @(@{ Project = $Dense; Configuration = 'Release' })
    Args = $concurrencyArgs }
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
    # The Checker fixtures follow the crack rules before 15/10/2026 (R1, R2, F2-7 point 3): the capture uses them too; ConcreteLibraryAdapter.Checks 5n measures the new ones.
    Add-Suite @{ Name = 'baseline/banco-ca'; Stage = 'baseline'; Kind = 'run'; Project = $Testing; Timeout = 1800
        Before = @{ Project = $Dense; Args = @('{out}\cattura', '{commit}', 'tutte', '--manifest', '--regole-fessurazione', 'precedenti') }; Builds = @(@{ Project = $Dense; Configuration = 'Release' })
        Args = @('compare-dense', $CheckerFixtures, '{out}\cattura', '--confronto', 'fixture-checker', '--report', '{out}\confronto-fixture-checker.json')
        Proof = @{ File = '{out}\confronto-fixture-checker.json'; Pattern = '"esito": "differenze tutte ammesse o classificate"' } }
} else {
    $benchNotRun += @{ Name = 'baseline/banco-ca'; Reason = 'fixture di Checker non trovate: passare -CheckerFixtures <Checker>\GPCChecker.Test.Concrete\Fixtures' }
}
if ($DenseRef) {
    Add-Suite @{ Name = 'baseline/banco-denso'; Stage = 'baseline'; Kind = 'run'; Project = $Testing; Timeout = 1800
        Before = @{ Project = $Dense; Args = @('{out}\cattura', '{commit}', 'tutte', '--manifest', '--regole-fessurazione', 'precedenti') }; Builds = @(@{ Project = $Dense; Configuration = 'Release' })
        Args = @('compare-dense', $(if ([IO.Path]::IsPathRooted($DenseRef)) { $DenseRef } else { Join-Path $Root $DenseRef }), '{out}\cattura', '--confronto', $DenseSet, '--report', '{out}\confronto-denso.json')
        Proof = @{ File = '{out}\confronto-denso.json'; Pattern = '"esito": "differenze tutte ammesse o classificate"' } }
}

# The text catalogue is keyed by first occurrence, which changes after U2 rejects invalid beams.
# Audit every text and subtract only the five formerly accepted invalid contributions.
if ($DenseRef -and $DenseSet -eq 'input-validation') {
    Add-Suite @{ Name = 'baseline/input-catalogo'; Stage = 'baseline'; Kind = 'run'; Project = 'tests\ConcreteInput.Checks\ConcreteInput.Checks.csproj'
        Args = @('{out}', '--catalog', $(if ([IO.Path]::IsPathRooted($DenseRef)) { $DenseRef } else { Join-Path $Root $DenseRef }), '{out}\..\baseline_banco-denso\cattura') }
}

$smokes = 'smoke-bridge-traffic', 'smoke-bridge-prices', 'smoke-section-actions', 'smoke', 'smoke-bridge', 'smoke-bridge-curves', 'smoke-bridge-design', 'smoke-bridge-predalle', 'smoke-ca-extensions', 'smoke-ca-features',
    'smoke-display', 'smoke-global-stability', 'smoke-hierarchy', 'smoke-horizontal', 'smoke-material-report', 'smoke-materials', 'smoke-neutral-axis',
    'smoke-project-report', 'smoke-projects', 'smoke-project-workspace', 'smoke-retaining-wall', 'smoke-sharing', 'smoke-steel'
foreach ($s in $smokes) {
    $smokeArgs = if ($s -eq 'smoke') { @("--$s", '{out}', '{cases}') } else { @("--$s", '{out}') }
    Add-Suite @{ Name = "ui/$s"; Stage = 'ui'; Kind = 'smoke'; Args = $smokeArgs; Timeout = 900; Proof = @{ File = '{out}\esito-smoke-completo.txt'; Pattern = "^Completato: --$s$" } }
}
foreach ($c in 'check-appearance', 'check-global-guidance-offscreen', 'check-wall-advanced-offscreen', 'check-wall-materials-offscreen') {
    Add-Suite @{ Name = "ui/$c"; Stage = 'ui'; Kind = 'check'; Args = @("--$c", '{out}'); Timeout = 600 }
}
# f5-suite-ui: the UI suites that open sheets of the seven modules with a XAML view still historic in the Release (shallow foundations, concrete,
# reinforcing steel, vertical pile and micropile, stratigraphy, retaining walls) run also with those seven modules MVVM (bench --module-impl):
# ui/<suite>-mvvm. At the Release switch of a module its pair leaves $MvvmBench; a duplicate goes when every module it opens is MVVM in the
# Release (docs/refactoring/f5-suite-ui.md).
$MvvmBench = @('geo_fondazioni_superficiali', 'mat_calcestruzzo', 'mat_acciaio_armatura', 'geo_palo_verticale', 'geo_micropalo_verticale', 'geo_stratigrafia', 'geo_muri_sostegno') |
    ForEach-Object { '--module-impl'; "$_=mvvm" }
foreach ($s in 'smoke', 'smoke-display', 'smoke-projects', 'smoke-project-workspace', 'smoke-hierarchy', 'smoke-materials', 'smoke-sharing', 'smoke-steel',
    'smoke-retaining-wall', 'smoke-global-stability') {
    $smokeArgs = if ($s -eq 'smoke') { @("--$s", '{out}', '{cases}') } else { @("--$s", '{out}') }
    Add-Suite @{ Name = "ui/$s-mvvm"; Stage = 'ui'; Kind = 'smoke'; Args = $smokeArgs + $MvvmBench; Timeout = 900; Proof = @{ File = '{out}\esito-smoke-completo.txt'; Pattern = "^Completato: --$s$" } }
}
Add-Suite @{ Name = 'ui/check-appearance-mvvm'; Stage = 'ui'; Kind = 'check'; Args = @('--check-appearance', '{out}') + $MvvmBench; Timeout = 600; Proof = @{ File = '{out}\completato.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/check-error-log-offscreen'; Stage = 'ui'; Kind = 'check'; Args = @('--check-error-log-offscreen', '{out}'); Timeout = 300; Proof = @{ File = '{out}\test.txt'; Pattern = '^PASS ' } }
# ModuleHost (F5.2): ModuleImplementations, composition root, bench --module-impl and MvvmSheetEditor with the test-only probe module, off screen.
Add-Suite @{ Name = 'ui/check-module-host-offscreen'; Stage = 'ui'; Kind = 'check'; Args = @('--check-module-host-offscreen', '{out}'); Timeout = 300; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/f3-report-chapters'; Stage = 'ui'; Kind = 'check'; Args = @('--check-f3-report-chapters', '{root}', '{out}'); Timeout = 600; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/f3-figures'; Stage = 'ui'; Kind = 'check'; Args = @('--check-f3-figures', '{root}', '{out}'); Timeout = 120; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/f3-concrete-figures'; Stage = 'ui'; Kind = 'check'; Args = @($(if ($CaptureConcreteFigures) { '--check-f3-concrete-figures-capture' } else { '--check-f3-concrete-figures' }), '{root}', '{out}'); Timeout = 120; Proof = @{ File = '{out}\esito.txt'; Pattern = $(if ($CaptureConcreteFigures) { '^CAPTURE ' } else { '^PASS ' }) } }

Add-Suite @{ Name = 'ui/f3-scheduling'; Stage = 'ui'; Kind = 'check'; Args = @('--check-f3-scheduling', '{root}', '{out}'); Timeout = 120; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/f3-equivalence'; Stage = 'ui'; Kind = 'check'; Args = @('--check-f3-equivalence', '{root}', '{out}\cattura'); Timeout = 1800; Proof = @{ File = '{out}\cattura\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/check-wiki-offscreen'; Stage = 'ui'; Kind = 'check'; Args = @('--check-wiki-offscreen', '{out}'); Timeout = 600; Proof = @{ File = '{out}\exit-code.txt'; Pattern = '^0\s*$' } }
# F5.4: the XAML view of the pile group hosted by MvvmSheetEditor off screen: traces through its controls equal to those of the historic view
# frozen in tests/fixtures/f5-palificata, visit without writes, exports equal to those of the historic view, the former UI part of
# HorizontalPileGroup.Checks, screenshots, help, automation names, read-only. The historic view and its suite ui/check-pile-group-historic
# are archived in supporto/SUPERATI/ui-storica/palificata since the Release switch (Q11).
Add-Suite @{ Name = 'ui/check-pile-group-view'; Stage = 'ui'; Kind = 'check'; Args = @('--check-pile-group-view', '{root}', '{out}\vista'); Timeout = 600; Proof = @{ File = '{out}\vista\esito.txt'; Pattern = '^PASS ' } }
# F5.5 (proposal 7.2, capture first): the historic material views (concrete and reinforcing steel) still give the traces frozen in
# tests/fixtures/f5-materiali, data and preview included, which Presentation.Tests compares with the ViewModels; off screen, until
# the historic views are archived (Q11).
Add-Suite @{ Name = 'ui/check-materials-historic'; Stage = 'ui'; Kind = 'check'; Args = @('--check-materials-historic', '{root}', '{out}\traccia'); Timeout = 300; Proof = @{ File = '{out}\traccia\esito.txt'; Pattern = '^PASS ' } }
# F5.5: the XAML views of the materials hosted by MvvmSheetEditor off screen: the 21 traces through their controls equal to those of the historic
# views, visit without writes, exports equal to the historic ones, chapter of a project report, screenshots, help, automation names, read-only.
Add-Suite @{ Name = 'ui/check-materials-view'; Stage = 'ui'; Kind = 'check'; Args = @('--check-materials-view', '{root}', '{out}\vista'); Timeout = 900; Proof = @{ File = '{out}\vista\esito.txt'; Pattern = '^PASS ' } }
# F5.6 (proposal 7.2, capture first): the historic vertical pile and micropile view still gives the traces frozen in
# tests/fixtures/f5-palo-verticale, which Presentation.Tests compares with the ViewModel; off screen, until the historic view is archived (Q11).
Add-Suite @{ Name = 'ui/check-palo-verticale-historic'; Stage = 'ui'; Kind = 'check'; Args = @('--check-palo-verticale-historic', '{root}', '{out}\traccia'); Timeout = 600; Proof = @{ File = '{out}\traccia\esito.txt'; Pattern = '^PASS ' } }
# F5.6: the XAML view of the vertical pile and micropile hosted by MvvmSheetEditor off screen (bench --module-impl, Release still historic):
# the 18 traces through its controls equal to those frozen in tests/fixtures/f5-palo-verticale, visit without writes, exports equal to those of
# the historic view, V3 (F5-01), screenshots, help, automation names, read-only, the shared soil transfer dialog against the historic window.
Add-Suite @{ Name = 'ui/check-palo-verticale-view'; Stage = 'ui'; Kind = 'check'; Args = @('--check-palo-verticale-view', '{root}', '{out}\vista'); Timeout = 900; Proof = @{ File = '{out}\vista\esito.txt'; Pattern = '^PASS ' } }
# F5.6: ui/f3-figures and ui/f3-scheduling with the MVVM view of the vertical pile and micropile (bench --module-impl): the frozen capacity
# series compared exactly through the Plot of the view, the recalculation and the closing through its general form.
Add-Suite @{ Name = 'ui/f3-figures-palo-verticale-mvvm'; Stage = 'ui'; Kind = 'check'; Args = @('--check-f3-figures', '{root}', '{out}', '--module-impl', 'geo_palo_verticale=mvvm', '--module-impl', 'geo_micropalo_verticale=mvvm'); Timeout = 120; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/f3-scheduling-palo-verticale-mvvm'; Stage = 'ui'; Kind = 'check'; Args = @('--check-f3-scheduling', '{root}', '{out}', '--module-impl', 'geo_palo_verticale=mvvm', '--module-impl', 'geo_micropalo_verticale=mvvm'); Timeout = 120; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
# F5.9b (proposal 7.2, capture first): the historic shallow foundation view still gives the traces frozen in tests/fixtures/f5-fondazioni,
# which Presentation.Tests compares with the ViewModel; off screen, until the historic view is archived (Q11).
Add-Suite @{ Name = 'ui/check-fondazioni-historic'; Stage = 'ui'; Kind = 'check'; Args = @('--check-fondazioni-historic', '{root}', '{out}\traccia'); Timeout = 300; Proof = @{ File = '{out}\traccia\esito.txt'; Pattern = '^PASS ' } }
# F5.9b: the XAML view of the shallow foundations hosted by MvvmSheetEditor off screen: traces through its controls equal to those of the historic
# view, visit without writes, drawings and exports (PDF of MigraDoc, T8) equal to the historic ones, screenshots, help, identifiers of
# FoundationWorkspace.UiChecks, automation names, read-only, soil transfer. The Release keeps the historic view until the test of the user.
Add-Suite @{ Name = 'ui/check-fondazioni-view'; Stage = 'ui'; Kind = 'check'; Args = @('--check-fondazioni-view', '{root}', '{out}\vista'); Timeout = 900; Proof = @{ File = '{out}\vista\esito.txt'; Pattern = '^PASS ' } }
# F5.7 (proposal 7.2, capture first): the historic horizontal pile and micropile view still gives the traces and the drawings frozen in
# tests/fixtures/f5-palo-orizzontale, which Presentation.Tests compares with the ViewModel; off screen, until the historic view is archived (Q11).
Add-Suite @{ Name = 'ui/check-palo-orizzontale-historic'; Stage = 'ui'; Kind = 'check'; Args = @('--check-palo-orizzontale-historic', '{root}', '{out}\traccia'); Timeout = 900; Proof = @{ File = '{out}\traccia\esito.txt'; Pattern = '^PASS ' } }
# F5.6b (proposal 7.2, capture first): the historic stratigraphy view still gives the traces frozen in tests/fixtures/f5-stratigrafia
# (message boxes answered and soil transfer dialog driven off screen), which Presentation.Tests compares with the ViewModel; off screen,
# until the historic view is archived (Q11).
Add-Suite @{ Name = 'ui/check-stratigrafia-historic'; Stage = 'ui'; Kind = 'check'; Args = @('--check-stratigrafia-historic', '{root}', '{out}\traccia'); Timeout = 300; Proof = @{ File = '{out}\traccia\esito.txt'; Pattern = '^PASS ' } }
# F5.6b: the XAML view of the stratigraphy hosted by MvvmSheetEditor off screen (bench --module-impl, Release still historic): the 15 traces
# through its controls and through the view of the shared soil dialog equal to those frozen in tests/fixtures/f5-stratigrafia, visit without
# writes, exports equal to those of the historic view, screenshots, F1, automation names, read-only, AI import with a scripted source (never
# the network), the view of the shared soil dialog against the historic window.
Add-Suite @{ Name = 'ui/check-stratigrafia-view'; Stage = 'ui'; Kind = 'check'; Args = @('--check-stratigrafia-view', '{root}', '{out}\vista'); Timeout = 600; Proof = @{ File = '{out}\vista\esito.txt'; Pattern = '^PASS ' } }
Add-Suite @{ Name = 'ui/check-spettri-view'; Stage = 'ui'; Kind = 'check'; Args = @('--check-spettri-view', '{root}', '{out}\vista'); Timeout = 180; Proof = @{ File = '{out}\vista\esito.txt'; Pattern = '^PASS ' } }
# F5.9 (proposal 7.2, capture first): the historic retaining wall view still gives the traces frozen in tests/fixtures/f5-muri,
# which Presentation.Tests compares with the ViewModel; off screen, until the historic view is archived (Q11).
Add-Suite @{ Name = 'ui/check-muri-historic'; Stage = 'ui'; Kind = 'check'; Args = @('--check-muri-historic', '{root}', '{out}\traccia'); Timeout = 900; Proof = @{ File = '{out}\traccia\esito.txt'; Pattern = '^PASS ' } }
# F5.9: the XAML view of the retaining walls hosted by MvvmSheetEditor off screen (bench --module-impl, Release still historic): the 24 traces
# through its controls equal to those frozen in tests/fixtures/f5-muri, visit without writes, exports equal to those of the historic view (Word,
# PDF, CSV, Word globale, bar schedule), screenshots, help, automation names, read-only, the dialog of the values and the shared soil dialog.
Add-Suite @{ Name = 'ui/check-muri-view'; Stage = 'ui'; Kind = 'check'; Args = @('--check-muri-view', '{root}', '{out}\vista'); Timeout = 1800; Proof = @{ File = '{out}\vista\esito.txt'; Pattern = '^PASS ' } }
# F5.9: ui/f3-scheduling with the MVVM view of the walls (bench --module-impl): the probes of the recalculation, of the closing and of the window
# of the values on the MVVM sheet and the view of its dialog.
Add-Suite @{ Name = 'ui/f3-scheduling-muri-mvvm'; Stage = 'ui'; Kind = 'check'; Args = @('--check-f3-scheduling', '{root}', '{out}', '--module-impl', 'geo_muri_sostegno=mvvm'); Timeout = 120; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
# F5.8 (proposal 7.2, capture first): the historic view of the elastic response of the horizontal pile and of its reinforcement still gives
# the traces and the drawings frozen in tests/fixtures/f5-elastico, which Presentation.Tests compares with the ViewModel; off screen, until the
# historic view is archived (Q11).
Add-Suite @{ Name = 'ui/check-palo-elastico-historic'; Stage = 'ui'; Kind = 'check'; Args = @('--check-palo-elastico-historic', '{root}', '{out}\traccia'); Timeout = 900; Proof = @{ File = '{out}\traccia\esito.txt'; Pattern = '^PASS ' } }
# F5.7/F5.8: the XAML views of the horizontal pile and micropile hosted by MvvmSheetEditor off screen (bench --module-impl, Release still historic):
# the traces of the tab of the capacity through its controls equal to those frozen in tests/fixtures/f5-palo-orizzontale (with F5-02) and its drawings,
# visit without writes, exports equal to those of the historic view (JSON, Word, PDF, CSV), F5-02, screenshots, help (every «?» and F1 to the
# navigator), automation names, read-only, the shared soil dialog, the bench.
Add-Suite @{ Name = 'ui/check-palo-orizzontale-view'; Stage = 'ui'; Kind = 'check'; Args = @('--check-palo-orizzontale-view', '{root}', '{out}\vista'); Timeout = 1800; Proof = @{ File = '{out}\vista\esito.txt'; Pattern = '^PASS ' } }
# F5.10 (proposal 7.2, capture first): the historic RC section view still gives the traces and the drawings frozen in tests/fixtures/f5-sezione,
# grouped by the sub-steps of F5.10, which the ViewModels of the sub-steps will compare; off screen, until the historic view is archived (Q11).
Add-Suite @{ Name = 'ui/check-sezione-historic'; Stage = 'ui'; Kind = 'check'; Args = @('--check-sezione-historic', '{root}', '{out}\traccia'); Timeout = 900; Proof = @{ File = '{out}\traccia\esito.txt'; Pattern = '^PASS ' } }
# F5.10 S1 (decision C7: the long traces in the ui stage): the ViewModel of the RC section without WPF against the traces frozen in
# tests/fixtures/f5-sezione (common part and [ingressi] of ingressi/*, percorso-f3/*, calcolo/* and muri/*), the files of the data of the F3
# capture and the sections sent by the walls; the fast stage keeps three scenarios in Presentation.Tests.
Add-Suite @{ Name = 'Presentation.Tests/sezione-ca'; Stage = 'ui'; Kind = 'run'; Project = 'tests\ANTHEA.Presentation.Tests\ANTHEA.Presentation.Tests.csproj'; Args = @('{out}', '--sezione-tracce'); Timeout = 900; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
# FieldForm (XAML + FormViewModel, F5.2) against the historic InputForm: same edit sequences on fields of real modules, same bytes of the document, same layout; windows outside the screen.
Add-Suite @{ Name = 'ui/check-field-form-parity'; Stage = 'ui'; Kind = 'check'; Args = @('--check-field-form-parity', '{out}'); Timeout = 300; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
# Theme tokens (F5.3): catalogue, colour equality with the palette in the three appearances, dictionaries of X.Desktop/Themes, light scope of the documents; off screen.
Add-Suite @{ Name = 'ui/check-theme-tokens'; Stage = 'ui'; Kind = 'check'; Args = @('--check-theme-tokens', '{out}'); Timeout = 300; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
# Contrast of every view, dialog and popup in the three appearances, off screen (F5.3, about 13 minutes): 0 defects in Scuro and Molto scuro,
# Chiara within the frozen baseline, which may only go down (tests/Desktop/ContrastBaseline.cs).
Add-Suite @{ Name = 'ui/check-contrast'; Stage = 'ui'; Kind = 'check'; Args = @('--check-contrast', '{out}', '--baseline', '{root}\tests\fixtures\f5\contrasto-chiara.json'); Timeout = 2400; Proof = @{ File = '{out}\esito.txt'; Pattern = '^PASS ' } }
foreach ($name in 'HorizontalPileGroup.Checks', 'ElasticPile.UiChecks', 'ConcreteShort.UiChecks') {
    Add-Suite @{ Name = $name; Stage = 'ui'; Kind = 'run'; Project = (TestProject $name); Args = @('{out}'); Timeout = 900 }
}
# Second ring of bars of the horizontal pile (user report of 8/10): main section, segments, c.a. verifier, off screen.
Add-Suite @{ Name = 'ElasticPile.UiChecks/anello-interno'; Stage = 'ui'; Kind = 'run'; Project = (TestProject 'ElasticPile.UiChecks'); Args = @('{out}', '--inner-ring-only'); Timeout = 300 }
# Notice of the stress block under the concrete law (decision of the user of 8/10), c.a. section and pile, off screen.
Add-Suite @{ Name = 'ElasticPile.UiChecks/nota-stress-block'; Stage = 'ui'; Kind = 'run'; Project = (TestProject 'ElasticPile.UiChecks'); Args = @('{out}', '--stress-block-note-only'); Timeout = 300 }
# Formerly ConcreteDesign.DesktopChecks (moved to supporto/SUPERATI/test in F1.6): ConcreteDesign.Checks writes ui-fixture.json first.
Add-Suite @{ Name = 'ui/check-concrete-design'; Stage = 'ui'; Kind = 'check'; Args = @('--check-concrete-design', '{out}'); Timeout = 900
    Before = @{ Project = (TestProject 'ConcreteDesign.Checks'); Args = @('{out}') }; Builds = @(@{ Project = (TestProject 'ConcreteDesign.Checks'); Configuration = 'Release' })
    Proof = @{ File = '{out}\ui-pass.txt'; Pattern = '^PASS' } }
# Short report selection and its DOCX + PDF without Word (F4-46): moved from the former stage word.
Add-Suite @{ Name = 'ConcreteShort.Checks'; Stage = 'regression'; Kind = 'run'; Project = (TestProject 'ConcreteShort.Checks'); Args = @('{out}'); Timeout = 900; Proof = @{ File = '{out}\test-results.txt'; Pattern = '^PASS \d+ assertions' } }

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
$manifest = if ($GpcLibDir) { Join-Path $GpcLibDir 'manifest.json' } else { Join-Path $Root 'lib\Checker\manifest.json' }
$started = Get-Date

function Say([string] $text)
{
    Write-Host $text
    # summary.txt is the record of the run: a file held for an instant by another process ("Flusso illeggibile", 14/10)
    # must neither lose the line nor fail the run. Add-Content reopens the file to read its encoding and kept failing
    # (16/10, also after its retries): append through a stream shared for reading and writing, with the same bytes
    # (UTF-8, BOM only when the file is created, CRLF). Bounded retries, then the original error.
    $path = Join-Path $Output 'summary.txt'
    for ($attempt = 1; ; $attempt++) {
        try {
            $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]::ReadWrite)
            try { $writer = New-Object System.IO.StreamWriter($stream, (New-Object System.Text.UTF8Encoding($true))); $writer.Write($text + "`r`n"); $writer.Flush() }
            finally { $stream.Dispose() }
            return
        }
        catch { if ($attempt -ge 20) { throw }; Start-Sleep -Milliseconds (50 * $attempt) }
    }
}
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
