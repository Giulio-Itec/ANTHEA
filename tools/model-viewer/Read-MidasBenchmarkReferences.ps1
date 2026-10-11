param([Parameter(Mandatory)][uri]$BaseUri,[Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$Destination,[Parameter(Mandatory)][string]$ApiKey)
$ErrorActionPreference='Stop'
if ($BaseUri.Scheme -ne 'https' -or $BaseUri.UserInfo -or $BaseUri.Query) { throw 'An HTTPS service URL without credentials is required.' }
if ((Test-Path -LiteralPath $Destination) -and (Get-ChildItem -LiteralPath $Destination -Force | Select-Object -First 1)) {throw 'Use a fresh reference directory.'}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$elements=(Get-Content -LiteralPath (Join-Path $SourceDirectory 'ELEM.json') -Raw|ConvertFrom-Json).ELEM
$cases=@('SLU_Q1_1(CBC:max)','ENV_SLU(CBC:max)','ENV_SLU(CBC:min)')
$report=@()
foreach($family in @('PLATE','BEAM')) {
    $ids=@($elements.psobject.Properties|Where-Object {$_.Value.TYPE -eq $family}|ForEach-Object {[int]$_.Name})
    for($index=0;$index -lt $cases.Count;$index++) {
        $argument=@{TABLE_NAME='GPC Results';TABLE_TYPE=$(if($family -eq 'PLATE'){'PLATEFORCEUL'}else{'BEAMFORCE'});UNIT=@{FORCE='N';DIST='mm'};STYLES=@{FORMAT='Scientific';PLACE=12};NODE_ELEMS=@{KEYS=$ids};LOAD_CASE_NAMES=@($cases[$index])}
        if($family -eq 'PLATE') {$argument.AVERAGE_NODAL_RESULT=$false;$argument.NODE_FLAG=@{CENTER=$false;NODES=$true};$argument.COMPONENTS=@('Elem','Load','Node','Mxx','Myy','Mxy','Fxx','Fyy','Fxy','Vxx','Vyy')}
        $watch=[Diagnostics.Stopwatch]::StartNew()
        $reply=Invoke-WebRequest -Uri ($BaseUri.AbsoluteUri.TrimEnd('/')+'/post/TABLE') -Method Post -Headers @{'MAPI-Key'=$ApiKey} -Body (@{Argument=$argument}|ConvertTo-Json -Depth 8 -Compress) -ContentType 'application/json' -TimeoutSec 180 -MaximumRedirection 0
        $seconds=$watch.Elapsed.TotalSeconds;$result=$reply.Content|ConvertFrom-Json
        if(!$result.'GPC Results'.HEAD -or !$result.'GPC Results'.DATA){throw 'Missing reference result table.'}
        $path=Join-Path $Destination ($family.ToLowerInvariant()+'-reference-'+$index+'.json');[IO.File]::WriteAllText($path,$reply.Content)
        $report += [pscustomobject]@{Family=$family;Case=$cases[$index];Seconds=$seconds;File=[IO.Path]::GetFileName($path);Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
        $report|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $Destination 'reference-acquisition-progress.json') -Encoding utf8
        Write-Output ("Reference {0} {1}: {2:N2} s" -f $family,$cases[$index],$seconds)
    }
}
foreach($table in @('UNIT','NODE','ELEM','SECT','THIK','MATL','CONS','STLD','LCOM-CONC')) {
    $reply=Invoke-WebRequest -Uri ($BaseUri.AbsoluteUri.TrimEnd('/')+'/db/'+$table) -Headers @{'MAPI-Key'=$ApiKey} -TimeoutSec 30 -MaximumRedirection 0
    if($reply.Content -ne [IO.File]::ReadAllText((Join-Path $SourceDirectory ($table+'.json')))){throw "Reference input changed: $table"}
}
$report|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $Destination 'reference-acquisition.json') -Encoding utf8
