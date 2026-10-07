param([Parameter(Mandatory=$true)][string]$Source, [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
$sourceFile = (Resolve-Path -LiteralPath $Source).Path
$targetFile = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path (Split-Path $targetFile) | Out-Null
$reportWord = $null
$reportDoc = $null
try {
    $reportWord = New-Object -ComObject Word.Application
    $reportWord.Visible = $false
    $reportWord.DisplayAlerts = 0
    $reportWord.AutomationSecurity = 3
    $reportDoc = $reportWord.Documents.Open($sourceFile, $false, $true)
    $reportDoc.Repaginate()
    $reportDoc.ExportAsFixedFormat($targetFile, 17)
    Write-Output ('Pagine: ' + $reportDoc.ComputeStatistics(2))
} finally {
    if ($null -ne $reportDoc) { $reportDoc.Close(0); [Runtime.InteropServices.Marshal]::ReleaseComObject($reportDoc) | Out-Null }
    if ($null -ne $reportWord) { $reportWord.Quit(); [Runtime.InteropServices.Marshal]::ReleaseComObject($reportWord) | Out-Null }
}
