param([Parameter(Mandatory)][uri]$BaseUri, [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$ApiKey, [string[]]$Cases = @())
$ErrorActionPreference = 'Stop'
if ($BaseUri.Scheme -ne 'https' -or $BaseUri.UserInfo -or $BaseUri.Query) { throw 'An HTTPS service URL without credentials is required.' }
if (Get-ChildItem -LiteralPath $Directory -Filter 'displacements-global-*.json') { throw 'Displacements already exist. Use a fresh acquisition directory.' }
$nodes = (Get-Content -LiteralPath (Join-Path $Directory 'NODE.json') -Raw | ConvertFrom-Json).NODE
$ids = @($nodes.psobject.Properties | ForEach-Object { [int]$_.Name })
if (!$ids.Count) { throw 'No source nodes were acquired.' }
if (!$Cases.Count) {
    $loads = (Get-Content -LiteralPath (Join-Path $Directory 'STLD.json') -Raw | ConvertFrom-Json).STLD
    $Cases = @($loads.psobject.Properties | ForEach-Object { $_.Value.NAME + '(ST)' })
}
$measurements = [Collections.Generic.List[object]]::new()
for ($index = 0; $index -lt $Cases.Count; $index++) {
    $argument = @{ TABLE_NAME='ANTHEADisplacements'; TABLE_TYPE='DISPLACEMENTG';
        UNIT=@{FORCE='N';DIST='mm'}; STYLES=@{FORMAT='Scientific';PLACE=12};
        COMPONENTS=@('Node','Load','DX','DY','DZ'); NODE_ELEMS=@{KEYS=$ids}; LOAD_CASE_NAMES=@($Cases[$index]) }
    $body = @{Argument=$argument} | ConvertTo-Json -Depth 6 -Compress
    $watch = [Diagnostics.Stopwatch]::StartNew()
    # Read-only result-table API, documented by MIDAS. No export path, model mutation or analysis run.
    $reply = Invoke-WebRequest -Uri ($BaseUri.AbsoluteUri.TrimEnd('/')+'/post/TABLE') -Method Post -Headers @{'MAPI-Key'=$ApiKey} -Body $body -ContentType 'application/json' -TimeoutSec 60 -MaximumRedirection 0
    $watch.Stop()
    $table = ($reply.Content | ConvertFrom-Json).ANTHEADisplacements
    if (!$table.HEAD -or @($table.DATA).Count -ne $ids.Count) { throw ('Incomplete displacement response for ' + $Cases[$index] + '; prior captures remain available.') }
    $file = Join-Path $Directory ('displacements-global-{0:D3}.json' -f $index)
    [IO.File]::WriteAllText($file,$reply.Content)
    $measurements.Add([pscustomobject]@{Case=$Cases[$index];Nodes=$ids.Count;Rows=@($table.DATA).Count;Seconds=$watch.Elapsed.TotalSeconds;File=[IO.Path]::GetFileName($file)})
    [IO.File]::WriteAllText((Join-Path $Directory 'displacements-acquisition.json'),($measurements | ConvertTo-Json -Depth 4))
    Write-Output ('{0}/{1} {2}: {3} nodi, {4:N2} s' -f ($index+1),$Cases.Count,$Cases[$index],$ids.Count,$watch.Elapsed.TotalSeconds)
}
