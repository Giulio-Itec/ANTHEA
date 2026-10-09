param([Parameter(Mandatory)][uri]$BaseUri, [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$ApiKey, [string]$Case = 'SLU_Q1_1(CBC:max)')
$ErrorActionPreference='Stop'
if ($BaseUri.Scheme -ne 'https' -or $BaseUri.UserInfo -or $BaseUri.Query) { throw 'An HTTPS service URL without credentials is required.' }
if (Get-ChildItem -LiteralPath $Directory -Filter '*-results-*.json') { throw 'Results already exist. Use a fresh acquisition directory to preserve prior evidence.' }
$elements = (Get-Content -LiteralPath (Join-Path $Directory 'ELEM.json') -Raw | ConvertFrom-Json).ELEM
foreach ($family in @('PLATE','BEAM')) {
    $ids = @($elements.psobject.Properties | Where-Object {$_.Value.TYPE -eq $family} | ForEach-Object {[int]$_.Name})
    for ($start=0; $start -lt $ids.Count; $start+=500) {
        $chunk=@($ids[$start..([Math]::Min($start+499,$ids.Count-1))])
        $argument=@{TABLE_NAME='GPC Results'; TABLE_TYPE=$(if($family -eq 'PLATE'){'PLATEFORCEUL'}else{'BEAMFORCE'});
            UNIT=@{FORCE='N'; DIST='mm'}; STYLES=@{FORMAT='Scientific'; PLACE=12}; NODE_ELEMS=@{KEYS=$chunk}; LOAD_CASE_NAMES=@($Case)}
        if ($family -eq 'PLATE') {
            $argument.AVERAGE_NODAL_RESULT=$false; $argument.NODE_FLAG=@{CENTER=$false;NODES=$true}
            $argument.COMPONENTS=@('Elem','Load','Node','Mxx','Myy','Mxy','Fxx','Fyy','Fxy','Vxx','Vyy','Mmax','Mmin','Fmax','Fmin')
        }
        $body=@{Argument=$argument} | ConvertTo-Json -Depth 8 -Compress
        # POST /post/TABLE reads a result table; it does not edit the model or run analysis.
        $reply=Invoke-WebRequest -Uri ($BaseUri.AbsoluteUri.TrimEnd('/')+'/post/TABLE') -Method Post -Headers @{'MAPI-Key'=$ApiKey} -Body $body -ContentType 'application/json' -TimeoutSec 180 -MaximumRedirection 0
        $result=$reply.Content | ConvertFrom-Json
        if (!$result.'GPC Results'.HEAD) { throw "MIDAS did not return the requested $family result table." }
        $file=Join-Path $Directory ($family.ToLowerInvariant()+'-results-'+[int]($start/500)+'.json')
        [IO.File]::WriteAllText($file,$reply.Content)
        Write-Output ($family+': '+$chunk.Count+' elements requested, '+$result.'GPC Results'.DATA.Count+' result rows acquired.')
    }
}
