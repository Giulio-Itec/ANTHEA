param([Parameter(Mandatory=$true)][string]$Document, [Parameter(Mandatory=$true)][string]$OutputPdf)
$ErrorActionPreference = 'Stop'
$documentPath = (Resolve-Path -LiteralPath $Document).Path
$pdfPath = [IO.Path]::GetFullPath((Join-Path $PWD $OutputPdf))
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($pdfPath)) | Out-Null
$wordForReview = $null
$reviewDocument = $null
try {
    $wordForReview = New-Object -ComObject Word.Application
    $wordForReview.Visible = $false
    $wordForReview.DisplayAlerts = 0
    $wordForReview.AutomationSecurity = 3
    $reviewDocument = $wordForReview.Documents.Open($documentPath, $false, $true)
    $reviewDocument.Repaginate()
    Write-Output "Pagine: $($reviewDocument.ComputeStatistics(2))"
    $reviewDocument.ExportAsFixedFormat($pdfPath, 17)
    Write-Output "PDF: $pdfPath"
} finally {
    if ($null -ne $reviewDocument) { $reviewDocument.Close(0); [Runtime.InteropServices.Marshal]::FinalReleaseComObject($reviewDocument) | Out-Null }
    if ($null -ne $wordForReview) { $wordForReview.Quit(); [Runtime.InteropServices.Marshal]::FinalReleaseComObject($wordForReview) | Out-Null }
}
