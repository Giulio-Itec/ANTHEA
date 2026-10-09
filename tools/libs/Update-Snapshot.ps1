<#
Rebuilds the GPC DLL snapshot from committed sources and, with -Install, replaces lib\Checker.

  powershell -NoProfile -ExecutionPolicy Bypass -File tools\libs\Update-Snapshot.ps1 -FromUpstream [-Staging <folder>] [-Install]
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\libs\Update-Snapshot.ps1 [-Staging <folder>] [-Install] [-RequirePushed]

  -Repos <folder>  folder that contains Utilities, Geometry, Model and Checker; default: the folder that contains this
                   ANTHEA checkout. Pass it when the script runs from a git worktree of ANTHEA placed elsewhere.
  -Lib <folder>    installed snapshot: its manifest.json is the reference of the version check and, with -Install, the
                   folder is the destination; default: lib\Checker of this checkout.
  -At <Repo=commit[,...]>  with -FromUpstream only: builds that repository at a commit reachable from its upstream
                   (already pushed, for example a release older than the head) instead of the upstream head.
  -ModelDependencies <folder>  verified bundle of the dependencies pinned by Model (build/dependencies.props, Model 4):
                   default .dependencies of the Model checkout in -Repos. See step 2.

Sources of the build:
- -FromUpstream (the mode for lib\Checker, which AGENTS.md wants from pushed commits). For each repository the commit of
  the remote-tracking branch of its checked-out branch (@{u}, as of the last git fetch: fetch first when needed) is
  checked out with "git worktree add --detach" into <TEMP>\gpc-snapshot\<Repo>. The four worktrees are siblings like the
  repositories, so the HintPaths and ProjectReferences between them resolve. The checkouts in -Repos are only read: their
  working tree, their HEAD and their local commits do not matter. The manifest records commit = upstream commit, branch =
  upstream branch, pushed = true. At the end, also after a failure, the script removes only the worktrees it created
  (git worktree remove --force, then git worktree prune) and the root; a root left by an interrupted run is recognised
  by its marker file and removed at the start. The root is fixed on purpose: each DLL contains the full path of its PDB
  (<project>\obj\Release\netstandard2.0\<name>.pdb), so the same commit built in another folder gives another SHA-256;
  with the fixed root two runs from the same commits give identical DLLs. The root must stay short (Geometry and
  Utilities version paths of up to 142 characters, Windows refuses paths of more than 260). Run one -FromUpstream at a
  time.
- Default: each repository is built in place at its local HEAD. The trees must be clean; the commit, branch and push
  state of each one are recorded (pushed = no local commit missing from the upstream). Nothing may be committed or merged
  in those repositories during the run: the commit is read before the build.

Steps:
1. Sources: see above; -RequirePushed refuses unpushed local commits (implicit with -FromUpstream).
2. Utilities and Geometry version their bin folders: their committed binaries are used as they are, never rebuilt
   (SourceLink writes the commit SHA into the PDB and, through the PDB id, into the DLL, so a committed binary can
   never equal a rebuild made after its own commit); the build must not change any of their versioned files (with
   -FromUpstream no worktree may differ from its commit after the build, Model and Checker included). Model and
   Checker are built from the commit, Release, with the SDK pinned by each global.json, into their (ignored) bin
   folders; they reference the committed Geometry and Utilities binaries through their HintPaths.
   Model 4 builds only from the bundle pinned in its build/dependencies.props (SHA-256 checked by its
   Directory.Build.targets) in the ignored Model\.dependencies, which a fresh worktree lacks: the Prepare-Dependencies.ps1
   of the commit fills it from -ModelDependencies, validating every file. The Geometry and Utilities DLLs pinned there
   must be the committed binaries of this snapshot, or GPCModel would be built against other DLLs than the installed ones.
3. The 8 DLLs go to the staging folder with manifest.json (file, version, SHA-256, repository, branch, commit, push, SDK;
   with -FromUpstream also fromUpstream = true and the buildRoot needed to reproduce the SHA-256) and manifest.props.
   A DLL whose SHA-256 differs from the one in -Lib must have a higher assembly version.
   GPCChecker.Geotechnics.xml (XML documentation, versioned in lib\Checker, outside the manifest and the hash check) is
   copied too when the build produces it.
4. -Install copies staging (DLLs, xml, manifest.json, manifest.props) into -Lib.

After installing: build\ci.ps1 -Profile full, the baseline comparison and the library tests (see AGENTS.md).
#>
[CmdletBinding()]
param([string] $Staging, [switch] $Install, [switch] $RequirePushed, [switch] $FromUpstream, [string] $Repos, [string] $Lib,
    [string[]] $At, [string] $ModelDependencies)
$ErrorActionPreference = 'Stop'
function Full([string] $path) {
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path (Get-Location).ProviderPath $path }
    [IO.Path]::GetFullPath($path).TrimEnd('\')
}
$Anthea = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$Repos = if ($Repos) { Full $Repos } else { Split-Path $Anthea -Parent }
$Lib = if ($Lib) { Full $Lib } else { Join-Path $Anthea 'lib\Checker' }
$atCommits = @{}
foreach ($item in @($At | ForEach-Object { $_ -split ',' } | Where-Object { $_ })) {
    if ($item -notmatch '^\s*(\w+)\s*=\s*([0-9a-fA-F]{7,40})\s*$') { throw "-At: atteso Repo=commit, non '$item'." }
    $atCommits[$Matches[1]] = $Matches[2]
}
if ($atCommits.Count -and -not $FromUpstream) { throw '-At vale solo con -FromUpstream (senza, si compila lo HEAD di ogni checkout).' }
$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $git) { $git = 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe' }
if (-not $Staging) { $Staging = Join-Path $Anthea ('supporto\artefatti\lib-staging\' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Staging = Full $Staging
New-Item -ItemType Directory -Force -Path $Staging | Out-Null
$log = Join-Path $Staging 'build.log'

$repoNames = 'Utilities', 'Geometry', 'Model', 'Checker'
$libraries = @(
    @{ File = 'GPCUtilities.dll'; Repo = 'Utilities'; Project = 'GPCUtilities\GPCUtilities.csproj'; Output = 'GPCUtilities\bin\Release\netstandard2.0'; TrackedBin = $true; Build = $false },
    @{ File = 'GPCGeometry.dll'; Repo = 'Geometry'; Project = 'GPCGeometry\GPCGeometry.csproj'; Output = 'GPCGeometry\bin\Release\netstandard2.0'; TrackedBin = $true; Build = $false },
    @{ File = 'DelaunayMesh.dll'; Repo = 'Geometry'; Project = 'DelaunayMesh\DelaunayMesh.csproj'; Output = 'DelaunayMesh\bin\Release\netstandard2.0'; TrackedBin = $true; Build = $false },
    @{ File = 'GPCModel.dll'; Repo = 'Model'; Project = 'Model\GPCModel.csproj'; Output = 'Model\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true },
    @{ File = 'GPCModelData.dll'; Repo = 'Model'; Project = 'ModelData\GPCModelData.csproj'; Output = 'ModelData\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true },
    @{ File = 'GPCChecker.Concrete.dll'; Repo = 'Checker'; Project = 'GPCChecker.Concrete\GPCChecker.Concrete.csproj'; Output = 'GPCChecker.Concrete\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true },
    @{ File = 'GPCChecker.Geotechnics.dll'; Repo = 'Checker'; Project = 'GPCChecker.Geotechnics\GPCChecker.Geotechnics.csproj'; Output = 'GPCChecker.Geotechnics\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true; Doc = 'GPCChecker.Geotechnics.xml' },
    @{ File = 'GPCChecker.CompositeBridge.dll'; Repo = 'Checker'; Project = 'GPCChecker.CompositeBridge\GPCChecker.CompositeBridge.csproj'; Output = 'GPCChecker.CompositeBridge\bin\Release\netstandard2.0'; TrackedBin = $false; Build = $true }
)
foreach ($r in $repoNames) {
    if (-not (Test-Path -LiteralPath (Join-Path (Join-Path $Repos $r) '.git'))) { throw "Repository $r non trovato in $Repos (opzione -Repos)." }
}

function RepoState([string] $repo) {
    $dir = Join-Path $Repos $repo
    $status = @(& $git -C $dir status --porcelain=v1)
    $branch = (& $git -C $dir rev-parse --abbrev-ref HEAD).Trim()
    $commit = (& $git -C $dir rev-parse HEAD).Trim()
    $ahead = (& $git -C $dir rev-list --count '@{u}..HEAD' 2>$null)
    [pscustomobject]@{ Repo = $repo; Branch = $branch; Commit = $commit; Dirty = $status.Count; Unpushed = $(if ($ahead) { [int] $ahead } else { -1 }); Sdk = '' }
}

# git with its stderr (progress, "Preparing worktree") kept away from the terminating errors of $ErrorActionPreference.
function GitValue([string] $dir) {
    $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $out = @(& $git -C $dir @args 2>$null); $code = $LASTEXITCODE } finally { $ErrorActionPreference = $eap }
    if ($code -ne 0 -or $out.Count -eq 0) { throw "git -C $dir $($args -join ' '): nessun risultato (uscita $code)." }
    ([string] $out[0]).Trim()
}
function GitLogged([string] $dir) {
    $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $out = @(& $git -C $dir @args 2>&1 | ForEach-Object { "$_" }); $code = $LASTEXITCODE } finally { $ErrorActionPreference = $eap }
    (@("> git -C $dir $($args -join ' ')") + $out) | Out-File -LiteralPath $log -Append
    if ($code -ne 0) { throw "git -C $dir $($args -join ' ') (uscita $code): $($out -join ' | ')" }
    $out
}

# -FromUpstream: fixed root of the temporary worktrees, outside the repositories (see the header).
$buildRepos = $Repos
if ($FromUpstream) {
    $buildRepos = Join-Path (Get-Item -LiteralPath ([IO.Path]::GetTempPath())).FullName 'gpc-snapshot'
    $marker = Join-Path $buildRepos '.update-snapshot-root'
    # MSBuild imports Directory.Build.props/targets from the parent folders: none may sit above the root.
    for ($d = Split-Path $buildRepos -Parent; $d; $d = Split-Path $d -Parent) {
        foreach ($f in 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props') {
            if (Test-Path -LiteralPath (Join-Path $d $f)) { throw "$(Join-Path $d $f) verrebbe importato dalla build in $buildRepos." }
        }
    }
}
# Removes the worktrees of this script (only <root>\<Repo> of the four repositories), the stale registrations and the root.
function Remove-WorkRoot {
    if ((Test-Path -LiteralPath $buildRepos) -and -not (Test-Path -LiteralPath $marker)) {
        throw "$buildRepos esiste senza il file $(Split-Path $marker -Leaf): non e' una radice di Update-Snapshot, rimuoverla a mano."
    }
    foreach ($r in $repoNames) {
        $main = Join-Path $Repos $r
        $wt = Join-Path $buildRepos $r
        $registered = @(GitLogged $main worktree list --porcelain) | Where-Object { $_ -like 'worktree *' } | ForEach-Object { Full $_.Substring(9) }
        if (($registered -contains $wt) -and (Test-Path -LiteralPath $wt)) { GitLogged $main worktree remove --force $wt | Out-Null }
        GitLogged $main worktree prune | Out-Null
    }
    if (Test-Path -LiteralPath $buildRepos) { Remove-Item -LiteralPath $buildRepos -Recurse -Force }
}

# 1. Sources
$states = @{}
foreach ($r in $repoNames) {
    $dir = Join-Path $Repos $r
    if ($FromUpstream) {
        $upstream = GitValue $dir rev-parse --abbrev-ref --symbolic-full-name '@{u}'
        $commit = GitValue $dir rev-parse --verify '@{u}^{commit}'
        if ($atCommits.ContainsKey($r)) {
            $wanted = GitValue $dir rev-parse --verify "$($atCommits[$r])^{commit}"
            $base = GitValue $dir merge-base $wanted $commit
            if ($base -ne $wanted) { throw "-At $r=$($atCommits[$r]): il commit non e' raggiungibile da $upstream (non pushato)." }
            $commit = $wanted
        }
        $s = [pscustomobject]@{ Repo = $r; Branch = ($upstream -replace '^[^/]+/', ''); Commit = $commit; Dirty = 0; Unpushed = 0; Sdk = ''; Upstream = $upstream }
    } else {
        $s = RepoState $r
        if ($s.Dirty -gt 0) { throw "$r ha modifiche non committate: lo snapshot deve nascere da commit." }
        if ($RequirePushed -and $s.Unpushed -ne 0) { throw "$r ha commit non pushati ($($s.Unpushed))." }
    }
    $states[$r] = $s
}

$docs = @()
$cleanUp = $false
try {
    if ($FromUpstream) {
        Remove-WorkRoot
        $cleanUp = $true
        New-Item -ItemType Directory -Force -Path $buildRepos | Out-Null
        Set-Content -LiteralPath $marker -Value "Radice temporanea di tools/libs/Update-Snapshot.ps1 -FromUpstream, $(Get-Date -Format s)" -Encoding UTF8
        foreach ($r in $repoNames) {
            $s = $states[$r]
            GitLogged (Join-Path $Repos $r) worktree add --quiet --detach (Join-Path $buildRepos $r) $s.Commit | Out-Null
            Write-Output ("{0,-10} {1} = {2}" -f $r, $s.Upstream, $s.Commit)
        }
    }
    foreach ($r in $repoNames) {
        Push-Location (Join-Path $buildRepos $r); $states[$r].Sdk = (& dotnet --version).Trim(); Pop-Location
    }

    # 2. Build. Model 4: pinned bundle in Model\.dependencies, coherent with the Geometry and Utilities of the snapshot.
    $prepare = Join-Path $buildRepos 'Model\build\Prepare-Dependencies.ps1'
    if (Test-Path -LiteralPath $prepare) {
        [xml] $pins = Get-Content -LiteralPath (Join-Path $buildRepos 'Model\build\dependencies.props') -Raw
        foreach ($l in $libraries | Where-Object { $_.TrackedBin }) {
            $pin = @($pins.Project.ItemGroup.GpcDependency | Where-Object { $_.Include -eq $l.File })
            $shipped = (Get-FileHash -LiteralPath (Join-Path (Join-Path (Join-Path $buildRepos $l.Repo) $l.Output) $l.File) -Algorithm SHA256).Hash
            if ($pin.Count -and $pin[0].Sha256 -ne $shipped) {
                throw "Model fissa $($l.File) $($pin[0].AssemblyVersion) ($($pin[0].Sha256.Substring(0, 12))), $($l.Repo) $($states[$l.Repo].Commit.Substring(0, 8)) ne fornisce un altro ($($shipped.Substring(0, 12))): scegliere commit coerenti (-At)."
            }
        }
        $bundle = if ($ModelDependencies) { Full $ModelDependencies } else { Join-Path $Repos 'Model\.dependencies' }
        & $prepare -SourceBundle $bundle *>> $log
        Write-Output "Model: dipendenze fissate preparate da $bundle"
    }
    foreach ($l in $libraries | Where-Object { $_.Build }) {
        $proj = Join-Path (Join-Path $buildRepos $l.Repo) $l.Project
        Push-Location (Join-Path $buildRepos $l.Repo)
        & dotnet build $proj -c Release -nologo -v:minimal *>> $log
        $code = $LASTEXITCODE
        Pop-Location
        if ($code -ne 0) { throw "Build fallita: $($l.Project) (vedi $log)" }
    }
    foreach ($r in 'Utilities', 'Geometry') {
        $after = @(& $git -C (Join-Path $buildRepos $r) status --porcelain=v1)
        if ($after.Count -gt 0) { throw "$r : la build ha cambiato file versionati (binari committati diversi da quelli prodotti dal commit): $($after -join '; ')" }
    }
    # The worktrees of Model and Checker must also still equal their commits (a checkout cut short by a path too long
    # for Windows, or a build that writes versioned files, would go unnoticed otherwise).
    if ($FromUpstream) {
        foreach ($r in 'Model', 'Checker') {
            $after = @(& $git -C (Join-Path $buildRepos $r) status --porcelain=v1)
            if ($after.Count -gt 0) { throw "$r : dopo la build il worktree differisce dal commit $($states[$r].Commit): $($after -join '; ')" }
        }
    }

    # 3. Staging and manifest
    $installed = @{}
    if (Test-Path -LiteralPath (Join-Path $Lib 'manifest.json')) {
        foreach ($a in (Get-Content -LiteralPath (Join-Path $Lib 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json).assemblies) { $installed[$a.file] = $a }
    }
    $assemblies = foreach ($l in $libraries) {
        $out = Join-Path (Join-Path $buildRepos $l.Repo) $l.Output
        $src = Join-Path $out $l.File
        Copy-Item -LiteralPath $src -Destination $Staging -Force
        if ($l.Doc -and (Test-Path -LiteralPath (Join-Path $out $l.Doc))) {
            Copy-Item -LiteralPath (Join-Path $out $l.Doc) -Destination $Staging -Force
            $docs += $l.Doc
        }
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
    $manifest = [ordered]@{ snapshotDate = (Get-Date -Format 'yyyy-MM-dd'); generator = 'tools/libs/Update-Snapshot.ps1' }
    if ($FromUpstream) { $manifest.fromUpstream = $true; $manifest.buildRoot = $buildRepos }
    if ($atCommits.Count) { $manifest.at = @($atCommits.Keys | Sort-Object | ForEach-Object { "$_=$($atCommits[$_])" }) }
    $manifest.targetFramework = 'netstandard2.0'
    $manifest.nuget = @(@{ name = 'MathNet.Numerics'; version = '5.0.0' })
    $manifest.assemblies = @($assemblies)
    $json = $manifest | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $Staging 'manifest.json'), $json, (New-Object Text.UTF8Encoding($false)))
    & (Join-Path $PSScriptRoot 'Write-ManifestProps.ps1') -LibDir $Staging | Out-Null
} finally {
    if ($cleanUp) {
        try { Remove-WorkRoot } catch { Write-Warning "Pulizia di $buildRepos incompleta: $_" }
    }
}
Write-Output "Snapshot in $Staging"
$assemblies | ForEach-Object { "{0,-32} {1,-10} {2} {3}@{4}" -f $_.file, $_.assemblyVersion, $_.sha256.Substring(0, 12), $_.repository, $_.commit.Substring(0, 8) }

# 4. Install
if ($Install) {
    New-Item -ItemType Directory -Force -Path $Lib | Out-Null
    foreach ($a in $assemblies) { Copy-Item -LiteralPath (Join-Path $Staging $a.file) -Destination $Lib -Force }
    foreach ($d in $docs) { Copy-Item -LiteralPath (Join-Path $Staging $d) -Destination $Lib -Force }
    Copy-Item -LiteralPath (Join-Path $Staging 'manifest.json') -Destination $Lib -Force
    Copy-Item -LiteralPath (Join-Path $Staging 'manifest.props') -Destination $Lib -Force
    Write-Output "Installato in $Lib. Aggiornare lib\Checker\README.md e lanciare build\ci.ps1 -Profile full."
}
