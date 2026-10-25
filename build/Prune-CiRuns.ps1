<#
Slims the old runs of build\ci.ps1 in supporto\artefatti\ci (not versioned). ci.ps1 calls it at the end of every run
(decisioni.md, 25 ottobre); it also runs alone:

  powershell -NoProfile -ExecutionPolicy Bypass -File build\Prune-CiRuns.ps1 -WhatIf     # dry run: counts, removes nothing
  powershell -NoProfile -ExecutionPolicy Bypass -File build\Prune-CiRuns.ps1 -KeepLast 10

Runs are the folders <yyyyMMdd-HHmmss>-<tag> directly in -CiRoot (default: supporto\artefatti\ci of this checkout); any
other folder is left alone. A run stays whole when it is one of the -KeepLast most recent completed runs (summary.json),
when it is or contains a path of -Exclude (ci.ps1 passes its own run and its inputs), when its tag contains 'rilascio',
when it holds CONSERVA.txt (ci.ps1 -Keep), when it has no summary.json and is younger than 24 hours (running, or just
interrupted), or when it is already slimmed (SNELLITA.txt). Every other run is slimmed: summary*, dettaglio*,
riepilogo*, esito*, conserva*, snellita*, logs and texts (.log, .txt, .md) under 2 MB and any file under 64 KB stay;
captures, PDF, Word and large data go, then the folders left empty. SNELLITA.txt records the rule (a run with nothing
to remove only gets SNELLITA.txt and is not counted).
A run holding a junction or a symbolic link is skipped whole: nothing is followed or removed in it (AGENTS.md).
Runs are never deleted. The result is one object: slimmed (run names), bytes, skipped and errors (texts).
#>
param(
    [string] $CiRoot,
    [int] $KeepLast = 10,
    [string[]] $Exclude = @(),
    [switch] $WhatIf
)
$ErrorActionPreference = 'Stop'
if (-not $CiRoot) { $CiRoot = Join-Path $PSScriptRoot '..\supporto\artefatti\ci' }
$CiRoot = [IO.Path]::GetFullPath($CiRoot)
$result = [pscustomobject]@{ slimmed = @(); bytes = [int64]0; skipped = @(); errors = @() }
if (-not (Test-Path -LiteralPath $CiRoot -PathType Container)) { return $result }

$excluded = @($Exclude | Where-Object { $_ } | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\') })
$note = 'Corsa snellita il {0} da build/Prune-CiRuns.ps1: tenuti summary*, dettaglio*, riepilogo*, esito*, log e testi ' +
    'sotto 2 MB e ogni file sotto 64 KB; tolti catture, PDF, Word e dati grandi.'

function Test-Keep([IO.FileInfo] $file) {
    $low = $file.Name.ToLowerInvariant()
    foreach ($prefix in 'summary', 'dettaglio', 'riepilogo', 'esito', 'conserva', 'snellita') { if ($low.StartsWith($prefix)) { return $true } }
    if (($low.EndsWith('.log') -or $low.EndsWith('.txt') -or $low.EndsWith('.md')) -and $file.Length -lt 2MB) { return $true }
    return $file.Length -lt 64KB
}

# Files and folders of a run, without following links: $null when the run holds a junction or a symbolic link.
function Get-Tree([IO.DirectoryInfo] $run) {
    $files = New-Object System.Collections.ArrayList
    $dirs = New-Object System.Collections.ArrayList
    $stack = New-Object System.Collections.Stack
    $stack.Push($run)
    while ($stack.Count -gt 0) {
        $d = $stack.Pop()
        foreach ($s in $d.GetDirectories()) {
            if ($s.Attributes -band [IO.FileAttributes]::ReparsePoint) { return $null }
            [void] $dirs.Add($s); $stack.Push($s)
        }
        foreach ($f in $d.GetFiles()) {
            if ($f.Attributes -band [IO.FileAttributes]::ReparsePoint) { return $null }
            [void] $files.Add($f)
        }
    }
    [pscustomobject]@{ Files = $files; Dirs = $dirs }
}

function Test-Excluded([string] $path) {
    foreach ($x in $excluded) {
        if ($x -eq $path -or $x.StartsWith($path + '\', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

$pattern = '^(\d{8}-\d{6})-(.+)$'
$runs = @(Get-ChildItem -LiteralPath $CiRoot -Directory -Force | Where-Object { $_.Name -match $pattern } | Sort-Object Name -Descending)
$completed = @($runs | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'summary.json') })
$recent = @($completed | Select-Object -First ([Math]::Max(0, $KeepLast)) | ForEach-Object { $_.Name })
$now = Get-Date

foreach ($run in $runs) {
    [void] ($run.Name -match $pattern)
    $started = [datetime]::ParseExact($Matches[1], 'yyyyMMdd-HHmmss', [Globalization.CultureInfo]::InvariantCulture)
    $tag = $Matches[2]
    if ($run.Attributes -band [IO.FileAttributes]::ReparsePoint) { $result.skipped += "$($run.Name): collegamento"; continue }
    if ($recent -contains $run.Name) { continue }
    if (Test-Excluded $run.FullName.TrimEnd('\')) { continue }
    if ($tag -match 'rilascio') { continue }
    if (Test-Path -LiteralPath (Join-Path $run.FullName 'CONSERVA.txt')) { continue }
    if (Test-Path -LiteralPath (Join-Path $run.FullName 'SNELLITA.txt')) { continue }
    if (-not (Test-Path -LiteralPath (Join-Path $run.FullName 'summary.json')) -and ($now - $started).TotalHours -lt 24) { continue }

    $tree = Get-Tree $run
    if (-not $tree) { $result.skipped += "$($run.Name): contiene un collegamento, saltata"; continue }
    $drop = @($tree.Files | Where-Object { -not (Test-Keep $_) })
    if ($drop.Count -eq 0) {
        # Nothing to remove: SNELLITA.txt spares the walk to the next passes; the run is not counted as slimmed.
        if (-not $WhatIf) { [IO.File]::WriteAllText((Join-Path $run.FullName 'SNELLITA.txt'), ($note -f $now.ToString('yyyy-MM-dd')) + "`r`n") }
        continue
    }
    $freed = [int64]0; $failed = 0
    foreach ($f in $drop) {
        if ($WhatIf) { $freed += $f.Length; continue }
        try {
            if ($f.IsReadOnly) { $f.IsReadOnly = $false }
            $length = $f.Length
            $f.Delete(); $freed += $length
        } catch { $failed++ }
    }
    if (-not $WhatIf) {
        # Folders left empty, deepest first (no links: Get-Tree checked them).
        foreach ($d in @($tree.Dirs | Sort-Object { $_.FullName.Length } -Descending)) {
            if (@(Get-ChildItem -LiteralPath $d.FullName -Force).Count -eq 0) { [IO.Directory]::Delete($d.FullName, $false) }
        }
        # A run with files that could not be removed stays without SNELLITA.txt: the next pass tries again.
        if ($failed -gt 0) { $result.errors += "$($run.Name): $failed file non rimossi" }
        else { [IO.File]::WriteAllText((Join-Path $run.FullName 'SNELLITA.txt'), ($note -f $now.ToString('yyyy-MM-dd')) + "`r`n") }
    }
    $result.slimmed += $run.Name
    $result.bytes += $freed
}
$result
