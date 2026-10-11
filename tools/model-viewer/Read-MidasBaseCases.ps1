param([Parameter(Mandatory)][uri]$BaseUri, [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$ApiKey, [ValidateRange(1,10)][int]$CasesPerRequest=3)
$ErrorActionPreference='Stop'
if ($BaseUri.Scheme -ne 'https' -or $BaseUri.UserInfo -or $BaseUri.Query) { throw 'An HTTPS service URL without credentials is required.' }
$elements = (Get-Content -LiteralPath (Join-Path $Directory 'ELEM.json') -Raw | ConvertFrom-Json).ELEM
$cases = @((Get-Content -LiteralPath (Join-Path $Directory 'STLD.json') -Raw | ConvertFrom-Json).STLD.psobject.Properties.Value.NAME)
$report = [Collections.Generic.List[object]]::new()
$total = [Diagnostics.Stopwatch]::StartNew()
foreach ($family in @('PLATE','BEAM')) {
    $ids = @($elements.psobject.Properties | Where-Object {$_.Value.TYPE -eq $family} | ForEach-Object {[int]$_.Name})
    for ($start=0; $start -lt $cases.Count; $start+=$CasesPerRequest) {
        $chunk = @($cases[$start..([Math]::Min($start+$CasesPerRequest-1,$cases.Count-1))])
        $file = Join-Path $Directory ($family.ToLowerInvariant()+'-results-base-'+[int]($start/$CasesPerRequest)+'.json')
        if (Test-Path -LiteralPath $file) { throw 'Use a fresh acquisition directory; existing evidence is not overwritten.' }
        $argument = @{TABLE_NAME='GPC Results';TABLE_TYPE=$(if($family -eq 'PLATE'){'PLATEFORCEUL'}else{'BEAMFORCE'});
            UNIT=@{FORCE='N';DIST='mm'};STYLES=@{FORMAT='Scientific';PLACE=12};NODE_ELEMS=@{KEYS=$ids};LOAD_CASE_NAMES=@($chunk|ForEach-Object{$_+'(ST)'})}
        if ($family -eq 'PLATE') {
            $argument.AVERAGE_NODAL_RESULT=$false; $argument.NODE_FLAG=@{CENTER=$false;NODES=$true}
            $argument.COMPONENTS=@('Elem','Load','Node','Mxx','Myy','Mxy','Fxx','Fyy','Fxy','Vxx','Vyy')
        }
        $watch=[Diagnostics.Stopwatch]::StartNew()
        $reply=Invoke-WebRequest -Uri ($BaseUri.AbsoluteUri.TrimEnd('/')+'/post/TABLE') -Method Post -Headers @{'MAPI-Key'=$ApiKey} -Body (@{Argument=$argument}|ConvertTo-Json -Depth 8 -Compress) -ContentType 'application/json' -TimeoutSec 180 -MaximumRedirection 0
        $httpSeconds=$watch.Elapsed.TotalSeconds
        $result=$reply.Content|ConvertFrom-Json
        $table=$result.'GPC Results'
        if (!$table.HEAD -or !$table.DATA) { throw "No $family results returned." }
        $loadIndex=[Array]::IndexOf($table.HEAD,'Load')
        $returned=@($table.DATA|ForEach-Object {$_[$loadIndex]}|Sort-Object -Unique)
        if (Compare-Object $chunk $returned) { throw 'Returned case names do not match the requested static cases.' }
        [IO.File]::WriteAllText($file,$reply.Content)
        $record=[pscustomobject]@{Family=$family;Cases=$chunk;Elements=$ids.Count;Rows=$table.DATA.Count;Bytes=(Get-Item -LiteralPath $file).Length;HttpSeconds=$httpSeconds;TotalSeconds=$watch.Elapsed.TotalSeconds;File=[IO.Path]::GetFileName($file);Sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
        $report.Add($record)
        $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Directory 'base-acquisition-progress.json') -Encoding utf8
        Write-Output ("{0} cases {1}/{2}: {3} rows, HTTP {4:N2} s, total {5:N2} s" -f $family,[Math]::Min($start+$CasesPerRequest,$cases.Count),$cases.Count,$table.DATA.Count,$httpSeconds,$watch.Elapsed.TotalSeconds)
    }
}
foreach ($table in @('UNIT','NODE','ELEM','SECT','THIK','MATL','CONS','STLD','LCOM-CONC')) {
    $reply=Invoke-WebRequest -Uri ($BaseUri.AbsoluteUri.TrimEnd('/')+'/db/'+$table) -Headers @{'MAPI-Key'=$ApiKey} -TimeoutSec 30 -MaximumRedirection 0
    if ($reply.Content -ne [IO.File]::ReadAllText((Join-Path $Directory ($table+'.json')))) { throw "Input changed while reading results: $table." }
}
[pscustomobject]@{Complete=$true;StaticCases=$cases.Count;WallSeconds=$total.Elapsed.TotalSeconds;Requests=$report} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Directory 'base-acquisition.json') -Encoding utf8
