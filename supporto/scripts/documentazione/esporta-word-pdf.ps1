param([Parameter(Mandatory=$true)][string]$Manifest)
$ErrorActionPreference = 'Stop'
$taskJobs = Get-Content -Raw -LiteralPath $Manifest | ConvertFrom-Json
$taskWord = $null
try {
    $taskWord = New-Object -ComObject Word.Application
    $taskWord.Visible = $false
    $taskWord.DisplayAlerts = 0
    foreach ($taskJob in $taskJobs) {
        $taskSource = (Resolve-Path -LiteralPath $taskJob.source).Path
        $taskOutput = [IO.Path]::GetFullPath($taskJob.output)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskOutput)) | Out-Null
        $taskDoc = $null
        try {
            $taskDoc = $taskWord.Documents.Open($taskSource, $false, $true)
            $taskDoc.Repaginate()
            $taskDoc.ExportAsFixedFormat($taskOutput, 17)
            Write-Output ($taskOutput + ' | ' + $taskDoc.ComputeStatistics(2) + ' pagine')
        } finally { if ($null -ne $taskDoc) { $taskDoc.Close(0) } }
    }
} finally { if ($null -ne $taskWord) { $taskWord.Quit() } }
