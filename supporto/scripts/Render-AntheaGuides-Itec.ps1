param([switch]$VerifyFinal, [string]$Revision = '03')
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$wordForGuides = $null
$guideDoc = $null
try {
    $wordForGuides = New-Object -ComObject Word.Application
    $wordForGuides.Visible = $false
    $wordForGuides.DisplayAlerts = 0
    $wordForGuides.AutomationSecurity = 3
    foreach ($kind in @('pratica', 'teorica')) {
        $sourcePath = Join-Path $repoPath "supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_${kind}_ITEC_Rev${Revision}.docx"
        $artifactFolder = if ($Revision -eq '02') { 'guide_anthea_itec' } else { "guide_anthea_itec_rev${Revision}" }
        $artifactPath = Join-Path $repoPath "supporto/artefatti/$artifactFolder/$kind"
        $guideDoc = $wordForGuides.Documents.Open($sourcePath, $false, $false)
        $guideDoc.Fields.Update() | Out-Null
        foreach ($toc in $guideDoc.TablesOfContents) { $toc.Update() }
        $guideDoc.Repaginate()
        foreach ($toc in $guideDoc.TablesOfContents) { $toc.UpdatePageNumbers() }
        if (-not $VerifyFinal) {
            $guideDoc.SaveAs2((Join-Path $artifactPath 'word_updated.docx'), 16)
        }
        $pdfName = if ($VerifyFinal) { 'final_verified.pdf' } else { 'fields_updated.pdf' }
        $guideDoc.ExportAsFixedFormat((Join-Path $artifactPath $pdfName), 17)
        Write-Output ("${kind}: " + $guideDoc.ComputeStatistics(2) + ' pagine')
        $guideDoc.Close(0)
        [Runtime.InteropServices.Marshal]::ReleaseComObject($guideDoc) | Out-Null
        $guideDoc = $null
    }
} finally {
    if ($null -ne $guideDoc) { $guideDoc.Close(0) }
    if ($null -ne $wordForGuides) {
        $wordForGuides.Quit()
        [Runtime.InteropServices.Marshal]::ReleaseComObject($wordForGuides) | Out-Null
    }
}
