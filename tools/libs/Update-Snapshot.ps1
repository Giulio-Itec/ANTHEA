<#
Rebuilds the GPC DLL snapshot from committed sources and, with -Install, replaces lib\Checker.

  powershell -NoProfile -ExecutionPolicy Bypass -File tools\libs\Update-Snapshot.ps1 [-Staging <folder>] [-Install] [-RequirePushed]

Steps:
1. The library repositories (Utilities, Geometry, Model, Checker, siblings of ANTHEA) must have a clean tree:
   the snapshot must come from commits. The commit, branch and push state of each one are recorded.
2. Utilities and Geometry version their bin folders: their committed binaries are used as they are, never rebuilt
   (SourceLink writes the commit SHA into the PDB and, through the PDB id, into the DLL, so a committed binary can
   never equal a rebuild made after its own commit). Model and Checker are built from the commit, Release, with the
   SDK pinned by each global.json, into their (ignored) bin folders; they reference the committed Geometry and
   Utilities binaries through their HintPaths.
3. The 8 DLLs go to the staging folder with manifest.json (file, version, SHA-256, repository, commit, SDK) and
   manifest.props. A DLL whose SHA-256 differs from the installed one must have a higher assembly version.
4. -Install copies staging into lib\Checker. Without -RequirePushed unpushed commits are allowed but recorded.

After installing: build\ci.ps1 -Profile full, the baseline comparison and the library tests (see AGENTS.md).
#>
[CmdletBinding()]
param([string] $Staging, [switch] $Install, [switch] $RequirePushed)
$ErrorActionPreference = 'Stop'
$Anthea = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$Repos = Split-Path $Anthea -Parent
$Lib = Join-Path $Anthea 'lib\Checker'
$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $git) { $git = 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe' }
if (-not $Staging) { $Staging = Join-Path $Anthea ('supporto\artefatti\lib-staging\' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Staging = [IO.Path]::GetFullPath($Staging)
New-Item -ItemType Directory -Force -Path $Staging | Out-Null
$log = Join-Path $Staging 'build.log'

$libraries = @(
    @{ File = 'GPCUtilities.dll'; Repo = 'Utilities'; Project = 'GPCUtilities\GPCUtilities.csproj'; Output = 'GPCUtilities\bin\Release\netstandard2.0'; TrackedBin = $true; Build = $false },
    @{ File = 'GPCGeometry.dll'; Repo = 'Geometry'; Project = 'GPCGeometry\GPCGeometry.csproj'; Output = 'GPCGeometry\bin\Release\netstandard2.0'; TrackedBin = $true; Build = $false },
    @{ File = 'DelaunayMesh.dll'; Repo = 'Geometry'; Project = 'DelaunayMesh\DelaunayMesh.csproj'; Output = 'DelaunayMesh\bin\Release\netstandard2.0'; TrackedBin = $true; Build = $false },
    @{ File = 'GPCModel.dll'; Repo = 'Model'; Project = 'Model\GPCModel.csproj'; Output = 'Model\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true },
    @{ File = 'GPCModelData.dll'; Repo = 'Model'; Project = 'ModelData\GPCModelData.csproj'; Output = 'ModelData\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true },
    @{ File = 'GPCChecker.Concrete.dll'; Repo = 'Checker'; Project = 'GPCChecker.Concrete\GPCChecker.Concrete.csproj'; Output = 'GPCChecker.Concrete\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true },
    @{ File = 'GPCChecker.Geotechnics.dll'; Repo = 'Checker'; Project = 'GPCChecker.Geotechnics\GPCChecker.Geotechnics.csproj'; Output = 'GPCChecker.Geotechnics\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true },
    @{ File = 'GPCChecker.CompositeBridge.dll'; Repo = 'Checker'; Project = 'GPCChecker.CompositeBridge\GPCChecker.CompositeBridge.csproj'; Output = 'GPCChecker.CompositeBridge\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true }
)

function Git([string] $repo) { & $git -C (Join-Path $Repos $repo) @args }
function RepoState([string] $repo) {
    $dir = Join-Path $Repos $repo
    $status = @(& $git -C $dir status --porcelain=v1)
    $branch = (& $git -C $dir rev-parse --abbrev-ref HEAD).Trim()
    $commit = (& $git -C $dir rev-parse HEAD).Trim()
    $ahead = (& $git -C $dir rev-list --count '@{u}..HEAD' 2>$null)
    [pscustomobject]@{ Repo = $repo; Branch = $branch; Commit = $commit; Dirty = $status.Count; Unpushed = $(if ($ahead) { [int] $ahead } else { -1 }); Sdk = '' }
}

# 1. Clean trees
$states = @{}
foreach ($r in 'Utilities', 'Geometry', 'Model', 'Checker') {
    $s = RepoState $r
    if ($s.Dirty -gt 0) { throw "$r ha modifiche non committate: lo snapshot deve nascere da commit." }
    if ($RequirePushed -and $s.Unpushed -ne 0) { throw "$r ha commit non pushati ($($s.Unpushed))." }
    Push-Location (Join-Path $Repos $r); $s.Sdk = (& dotnet --version).Trim(); Pop-Location
    $states[$r] = $s
}

# 2. Build
foreach ($l in $libraries | Where-Object { $_.Build }) {
    $proj = Join-Path (Join-Path $Repos $l.Repo) $l.Project
    Push-Location (Join-Path $Repos $l.Repo)
    & dotnet build $proj -c Release -nologo -v:minimal *>> $log
    $code = $LASTEXITCODE
    Pop-Location
    if ($code -ne 0) { throw "Build fallita: $($l.Project) (vedi $log)" }
}
foreach ($r in 'Utilities', 'Geometry') {
    $after = @(& $git -C (Join-Path $Repos $r) status --porcelain=v1)
    if ($after.Count -gt 0) { throw "$r : la build ha cambiato file versionati (binari committati diversi da quelli prodotti dal commit): $($after -join '; ')" }
}

# 3. Staging and manifest
$installed = @{}
if (Test-Path -LiteralPath (Join-Path $Lib 'manifest.json')) {
    foreach ($a in (Get-Content -LiteralPath (Join-Path $Lib 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json).assemblies) { $installed[$a.file] = $a }
}
$assemblies = foreach ($l in $libraries) {
    $src = Join-Path (Join-Path (Join-Path $Repos $l.Repo) $l.Output) $l.File
    Copy-Item -LiteralPath $src -Destination $Staging -Force
    $hash = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash
    $version = [Reflection.AssemblyName]::GetAssemblyName($src).Version.ToString()
    $old = $installed[$l.File]
    if ($old -and $old.sha256 -ne $hash -and [version] $version -le [version] $old.assemblyVersion) {
        throw "$($l.File): contenuto diverso da quello installato con versione $version non superiore a $($old.assemblyVersion)."
    }
    $s = $states[$l.Repo]
    [ordered]@{ file = $l.File; source = "$($l.Repo)/$($l.Output.Replace('\', '/'))"; assemblyVersion = $version; sha256 = $hash
        repository = $l.Repo; branch = $s.Branch; commit = $s.Commit; pushed = ($s.Unpushed -eq 0); sdk = $s.Sdk }
}
$manifest = [ordered]@{
    snapshotDate = (Get-Date -Format 'yyyy-MM-dd'); generator = 'tools/libs/Update-Snapshot.ps1'; targetFramework = 'netstandard2.0'
    nuget = @(@{ name = 'MathNet.Numerics'; version = '5.0.0' }); assemblies = @($assemblies)
}
$json = $manifest | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $Staging 'manifest.json'), $json, (New-Object Text.UTF8Encoding($false)))
& (Join-Path $PSScriptRoot 'Write-ManifestProps.ps1') -LibDir $Staging | Out-Null
Write-Output "Snapshot in $Staging"
$assemblies | ForEach-Object { "{0,-32} {1,-10} {2} {3}@{4}" -f $_.file, $_.assemblyVersion, $_.sha256.Substring(0, 12), $_.repository, $_.commit.Substring(0, 8) }

# 4. Install
if ($Install) {
    foreach ($a in $assemblies) { Copy-Item -LiteralPath (Join-Path $Staging $a.file) -Destination $Lib -Force }
    Copy-Item -LiteralPath (Join-Path $Staging 'manifest.json') -Destination $Lib -Force
    Copy-Item -LiteralPath (Join-Path $Staging 'manifest.props') -Destination $Lib -Force
    Write-Output "Installato in $Lib. Aggiornare lib\Checker\README.md e lanciare build\ci.ps1 -Profile full."
}
