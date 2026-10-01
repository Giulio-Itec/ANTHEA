param([switch]$VerifyFinal)
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$wordForBridgeGuides = $null
$guideDoc = $null
try {
    $wordForBridgeGuides = New-Object -ComObject Word.Application
    $wordForBridgeGuides.Visible = $false
    $wordForBridgeGuides.DisplayAlerts = 0
    $wordForBridgeGuides.AutomationSecurity = 3
    foreach ($kind in @('pratica', 'teorica')) {
        $label = if ($kind -eq 'pratica') { 'Pratica' } else { 'Teoria' }
        $sourcePath = Join-Path $repoPath "supporto/documentazione/Bridge_Design/ANTHEA_Bridge_Design_${label}_ITEC_Rev01.docx"
        $artifactPath = Join-Path $repoPath "supporto/artefatti/bridge-design-guide-20260930/$kind"
        $guideDoc = $wordForBridgeGuides.Documents.Open([string]$sourcePath, $false, $false)
        $guideDoc.Fields.Update() | Out-Null
        foreach ($toc in $guideDoc.TablesOfContents) { $toc.Update() }
        $guideDoc.Repaginate()
        foreach ($toc in $guideDoc.TablesOfContents) { $toc.UpdatePageNumbers() }
        if (-not $VerifyFinal) { $guideDoc.SaveAs2([string](Join-Path $artifactPath 'word_updated.docx'), 16) }
        $pdfName = if ($VerifyFinal) { 'final_verified.pdf' } else { 'fields_updated.pdf' }
        $guideDoc.ExportAsFixedFormat([string](Join-Path $artifactPath $pdfName), 17)
        if ($VerifyFinal) { Copy-Item -LiteralPath (Join-Path $artifactPath $pdfName) -Destination ([IO.Path]::ChangeExtension($sourcePath, '.pdf')) -Force }
        Write-Output ("${kind}: " + $guideDoc.ComputeStatistics(2) + ' pagine')
        $guideDoc.Close(0)
        [Runtime.InteropServices.Marshal]::ReleaseComObject($guideDoc) | Out-Null
        $guideDoc = $null
    }
} finally {
    if ($null -ne $guideDoc) { $guideDoc.Close(0) }
    if ($null -ne $wordForBridgeGuides) { $wordForBridgeGuides.Quit(); [Runtime.InteropServices.Marshal]::ReleaseComObject($wordForBridgeGuides) | Out-Null }
}
