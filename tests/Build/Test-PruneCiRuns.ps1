param([string]$Output)
# Checks of build\Prune-CiRuns.ps1 on a fixture of runs OUTSIDE the repository (AGENTS.md: the junction fixture too).
# The junction is removed with rmdir in finally, then the fixture with rmdir /s, which does not follow links.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $Output) { $Output = Join-Path $repo ('supporto\artefatti\prune-ci-runs\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$prune = Join-Path $repo 'build\Prune-CiRuns.ps1'
$fixture = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Temp\ANTHEA-PruneTest-' + [guid]::NewGuid().ToString('N'))
$ci = Join-Path $fixture 'ci'
$external = Join-Path $fixture 'external'
$checks = [Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $checks.Add($Message)
    Write-Host "PASS $Message"
}
function Write-Bytes([string]$Path, [int]$Length) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Path) | Out-Null
    [IO.File]::WriteAllBytes($Path, (New-Object byte[] $Length))
}
function Names([string[]]$List) { (@($List) | Sort-Object) -join ',' }
# A run as ci.ps1 writes it: what the slimming keeps (summaries, logs, esito, small files) and what it removes
# (a large JSON capture, a PNG figure, a PDF report: $Large bytes).
$Large = 70KB + 70KB + 80KB
function New-Run([string]$Name, [switch]$Incomplete) {
    $run = Join-Path $ci $Name
    if (-not $Incomplete) { Write-Bytes "$run\summary.json" 2KB }
    Write-Bytes "$run\summary.txt" 1KB
    Write-Bytes "$run\suite_a\dettaglio.txt" 1KB
    Write-Bytes "$run\suite_a\run.out.log" 10KB
    Write-Bytes "$run\suite_a\esito.txt" 100
    Write-Bytes "$run\suite_a\piccolo.json" 1KB
    Write-Bytes "$run\suite_a\cattura.json" 70KB
    Write-Bytes "$run\suite_a\figure\pagina.png" 70KB
    Write-Bytes "$run\suite_b\report.pdf" 80KB
    return $run
}
function Kept([string]$Run) { Names @(Get-ChildItem -LiteralPath $Run -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($Run.Length + 1) }) }
$small = @('summary.txt', 'suite_a\dettaglio.txt', 'suite_a\run.out.log', 'suite_a\esito.txt', 'suite_a\piccolo.json')
$whole = $small + @('summary.json', 'suite_a\cattura.json', 'suite_a\figure\pagina.png', 'suite_b\report.pdf')
$slim = $small + @('summary.json', 'SNELLITA.txt')

try {
    # Twelve completed runs: with -KeepLast 10 the two oldest are slimmed.
    $ordinary = @(1..12 | ForEach-Object { New-Run ('20261001-{0:D2}0000-standard' -f $_) })
    Write-Bytes "$($ordinary[0])\suite_a\lungo.txt" (2MB + 1)
    $release = New-Run '20260901-000000-rilascio-9.9.9-full'
    $marked = New-Run '20260902-000000-indagine'; Write-Bytes "$marked\CONSERVA.txt" 10
    $reference = New-Run '20260903-000000-riferimento'
    $inner = New-Run '20260904-000000-cattura'
    $interrupted = New-Run '20260905-000000-interrotta' -Incomplete
    $running = New-Run ((Get-Date).AddHours(-1).ToString('yyyyMMdd-HHmmss') + '-in-corso') -Incomplete
    $done = New-Run '20260906-000000-gia-snellita'; Write-Bytes "$done\SNELLITA.txt" 10
    $manual = New-Run 'indagine-manuale'
    $light = Join-Path $ci '20260908-000000-leggera'
    Write-Bytes "$light\summary.json" 2KB; Write-Bytes "$light\suite_a\esito.txt" 100
    $linked = New-Run '20260907-000000-con-giunzione'
    Write-Bytes "$external\destinazione.bin" 100KB
    New-Item -ItemType Junction -Path "$linked\collegamento" -Target $external | Out-Null
    $exclude = @($reference, "$inner\suite_a\figure")
    $expected = Names @($ordinary[0], $ordinary[1], $interrupted | ForEach-Object { Split-Path $_ -Leaf })

    $dry = & $prune -CiRoot $ci -KeepLast 10 -Exclude $exclude -WhatIf
    Check ((Names $dry.slimmed) -eq $expected) 'Dry run lists the two oldest completed runs and the interrupted run'
    Check ($dry.bytes -eq (3 * $Large + 2MB + 1)) 'Dry run counts captures, PDF, large data and texts over 2 MB'
    Check ((Kept $ordinary[0]) -eq (Names ($whole + 'suite_a\lungo.txt'))) 'Dry run removes nothing'

    $real = & $prune -CiRoot $ci -KeepLast 10 -Exclude $exclude
    Check ((Names $real.slimmed) -eq $expected) 'Slimming takes the runs of the dry run'
    Check ($real.bytes -eq $dry.bytes) 'Slimming frees the bytes counted by the dry run'
    foreach ($run in $ordinary[0..1]) { Check ((Kept $run) -eq (Names $slim)) "Slimmed run keeps summaries, logs, esito and small files: $(Split-Path $run -Leaf)" }
    Check ((Kept $interrupted) -eq (Names ($small + 'SNELLITA.txt'))) 'Run without summary.json older than 24 hours is slimmed'
    Check (-not (Test-Path -LiteralPath "$($ordinary[0])\suite_b") -and -not (Test-Path -LiteralPath "$($ordinary[0])\suite_a\figure")) 'Folders left empty are removed'
    Check ((Get-Content -LiteralPath "$($ordinary[0])\SNELLITA.txt" -Raw) -match 'Prune-CiRuns') 'SNELLITA.txt records the rule'
    foreach ($run in $ordinary[2..11]) { Check ((Kept $run) -eq (Names $whole)) "Recent run stays whole: $(Split-Path $run -Leaf)" }
    Check ((Kept $release) -eq (Names $whole)) 'Release run stays whole'
    Check ((Kept $marked) -eq (Names ($whole + 'CONSERVA.txt'))) 'Run with CONSERVA.txt stays whole'
    Check ((Kept $reference) -eq (Names $whole)) 'Excluded run stays whole'
    Check ((Kept $inner) -eq (Names $whole)) 'Run holding an excluded path stays whole'
    Check ((Kept $running) -eq (Names ($whole | Where-Object { $_ -ne 'summary.json' }))) 'Run without summary.json younger than 24 hours stays whole'
    Check ((Kept $done) -eq (Names ($whole + 'SNELLITA.txt'))) 'Run already slimmed is not taken again'
    Check ((Kept $manual) -eq (Names $whole)) 'Folder without the name of a run is left alone'
    Check ((Kept $light) -eq (Names @('summary.json', 'suite_a\esito.txt', 'SNELLITA.txt'))) 'Old run with only small files is marked, not counted as slimmed'
    Check ((@($real.skipped) -join ';') -match '20260907-000000-con-giunzione') 'Run holding a junction is reported and skipped'
    Check ((Get-Item -LiteralPath "$linked\collegamento" -Force).LinkType -eq 'Junction') 'The junction stays'
    Check ((Get-Item -LiteralPath "$linked\suite_a\cattura.json").Length -eq 70KB) 'Nothing is removed in the run holding a junction'
    Check ((Get-Item -LiteralPath "$external\destinazione.bin").Length -eq 100KB) 'Junction target is untouched'

    $again = & $prune -CiRoot $ci -KeepLast 10 -Exclude $exclude
    Check (@($again.slimmed).Count -eq 0 -and $again.bytes -eq 0) 'A second pass slims nothing'
    $none = & $prune -CiRoot (Join-Path $fixture 'assente')
    Check (@($none.slimmed).Count -eq 0) 'Missing ci folder: nothing to do'
} finally {
    $link = Join-Path $fixture 'ci\20260907-000000-con-giunzione\collegamento'
    $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
    if ($item -and $item.LinkType -eq 'Junction' -and $item.FullName.StartsWith($fixture + '\', [StringComparison]::OrdinalIgnoreCase)) {
        & $env:ComSpec /d /c "rmdir `"$($item.FullName)`""
        if ($LASTEXITCODE -ne 0) { throw "Cannot remove fixture junction: $link" }
    }
    if ((Test-Path -LiteralPath $fixture) -and -not (Test-Path -LiteralPath $link)) { & $env:ComSpec /d /c "rmdir /s /q `"$fixture`"" }
}
[IO.File]::WriteAllText((Join-Path $Output 'esito.txt'), "PASS $($checks.Count) prune checks`r`n" + ($checks -join "`r`n"))
