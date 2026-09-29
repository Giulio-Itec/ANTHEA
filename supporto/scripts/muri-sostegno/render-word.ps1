param([Parameter(Mandatory=$true)][string]$Source, [Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
$taskSourcePath = (Resolve-Path -LiteralPath $Source).Path
$taskOutputPath = [IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskOutputPath)) | Out-Null
$taskWord = $null
$taskDocument = $null
try {
    $taskWord = New-Object -ComObject Word.Application
    $taskWord.Visible = $false
    $taskWord.DisplayAlerts = 0
    $taskDocument = $taskWord.Documents.Open($taskSourcePath, $false, $true)
    $taskDocument.Repaginate()
    $taskDocument.ExportAsFixedFormat($taskOutputPath, 17)
    Write-Output ('Pages: ' + $taskDocument.ComputeStatistics(2))
} finally {
    if ($null -ne $taskDocument) { $taskDocument.Close(0) }
    if ($null -ne $taskWord) { $taskWord.Quit() }
}
