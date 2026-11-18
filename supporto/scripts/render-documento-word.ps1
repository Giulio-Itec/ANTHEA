param(
    [Parameter(Mandatory=$true)][string]$InputDocx,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$RefreshFields
)
$ErrorActionPreference = 'Stop'
$inputPath = (Resolve-Path -LiteralPath $InputDocx).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = (Resolve-Path -LiteralPath $OutputDirectory).Path
$wordApp = $null
$wordDoc = $null
try {
    $wordApp = New-Object -ComObject Word.Application
    $wordApp.Visible = $false
    $wordApp.DisplayAlerts = 0
    $wordDoc = $wordApp.Documents.Open($inputPath, $false, (-not $RefreshFields.IsPresent), $false)
    if ($RefreshFields) {
        $wordDoc.Fields.Update() | Out-Null
        foreach ($toc in $wordDoc.TablesOfContents) { $toc.Update() }
        foreach ($section in $wordDoc.Sections) {
            foreach ($header in $section.Headers) { $header.Range.Fields.Update() | Out-Null }
            foreach ($footer in $section.Footers) { $footer.Range.Fields.Update() | Out-Null }
        }
        $wordDoc.Repaginate()
        foreach ($toc in $wordDoc.TablesOfContents) { $toc.UpdatePageNumbers() }
        $wordDoc.Save()
    }
    $wordDoc.Repaginate()
    $pdfPath = Join-Path $outputPath ([IO.Path]::GetFileNameWithoutExtension($inputPath) + '.pdf')
    $wordDoc.ExportAsFixedFormat($pdfPath, 17)
    $pages = $wordDoc.ComputeStatistics(2)
    $sectionInfo = @()
    foreach ($section in $wordDoc.Sections) {
        $sectionInfo += [pscustomobject]@{
            index = $section.Index
            start = $section.Range.Start
            end = $section.Range.End
            firstPage = $section.Range.Information(3)
        }
    }
    [pscustomobject]@{pdf=$pdfPath; pages=$pages; sections=$sectionInfo} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputPath 'render-word.json') -Encoding utf8
    Write-Output "PDF: $pdfPath"
    Write-Output "Pagine: $pages"
} finally {
    if ($wordDoc) { $wordDoc.Close(0); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($wordDoc) }
    if ($wordApp) { $wordApp.Quit(0); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($wordApp) }
}
