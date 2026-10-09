param(
    [Parameter(Mandatory)] [uri] $BaseUri,
    [Parameter(Mandatory)] [string] $Destination,
    [Parameter(Mandatory)] [string] $ApiKey
)
$ErrorActionPreference = 'Stop'
if ($BaseUri.Scheme -ne 'https' -or $BaseUri.UserInfo -or $BaseUri.Query) { throw 'An HTTPS service URL without credentials is required.' }
Add-Type -AssemblyName System.Net.Http
$destinationPath = [IO.Path]::GetFullPath($Destination)
if ((Test-Path -LiteralPath $destinationPath) -and (Get-ChildItem -LiteralPath $destinationPath -Force | Select-Object -First 1)) { throw 'Use a new output directory to preserve the previous acquisition.' }
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromSeconds(25)
$client.DefaultRequestHeaders.Add('MAPI-Key', $ApiKey)
$records = @()
try {
    $tables = @('UNIT','NODE','ELEM','MATL','SECT','THIK','GRUP','SKEW','CONS','OFFS','FRLS','PRLS','ELNK','RIGD','MCON','NSPR','NLNK','NLLP','STAG','BNGR','LDGR','STLD','CNLD','BMLD','PRES','BODF','NBOF','LCOM-GEN','LCOM-CONC','LCOM-STEEL','LCOM-SRC','LCOM-STLCOMP','LCOM-SEISMIC')
    for ($start = 0; $start -lt $tables.Count; $start += 4) {
        $requests = @()
        foreach ($table in $tables[$start..([Math]::Min($start + 3, $tables.Count - 1))]) {
            $requests += [PSCustomObject]@{ Table = $table; Task = $client.GetAsync($BaseUri.AbsoluteUri.TrimEnd('/') + '/db/' + $table) }
        }
        foreach ($request in $requests) {
            try {
                $response = $request.Task.GetAwaiter().GetResult()
                try {
                    if (!$response.IsSuccessStatusCode) {
                        $records += [PSCustomObject]@{ Table=$request.Table; Status=[int]$response.StatusCode; Available=$false }
                        continue
                    }
                    $json = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    if ($json.Length -gt 32000000) { throw 'Table size exceeds the snapshot limit.' }
                    $null = $json | ConvertFrom-Json
                    [IO.File]::WriteAllText((Join-Path $destinationPath ($request.Table + '.json')), $json)
                    $records += [PSCustomObject]@{ Table=$request.Table; Status=200; Available=$true }
                } finally { $response.Dispose() }
            } catch {
                $records += [PSCustomObject]@{ Table=$request.Table; Status='transport-error'; Available=$false }
            }
        }
    }
    foreach ($table in @('UNIT','NODE','ELEM','SECT','THIK')) {
        $path = Join-Path $destinationPath ($table + '.json')
        if (!(Test-Path -LiteralPath $path)) { throw "Missing mandatory table: $table" }
        $response = $client.GetAsync($BaseUri.AbsoluteUri.TrimEnd('/') + '/db/' + $table).GetAwaiter().GetResult()
        try {
            $response.EnsureSuccessStatusCode() | Out-Null
            if ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult() -ne [IO.File]::ReadAllText($path)) { throw "Model changed during acquisition: $table" }
        } finally { $response.Dispose() }
    }
    $records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $destinationPath 'acquisition.json') -Encoding utf8
    $records | Format-Table -AutoSize
} finally { $client.Dispose() }
