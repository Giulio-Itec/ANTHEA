param([switch]$VerifyFinal)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$art = Join-Path $repo 'supporto/artefatti/validazione_ca_2026_09_25'
$source = Join-Path $repo 'supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Calcestruzzo_Armato_Rev01.docx'
$word = $null
$doc = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $doc = $word.Documents.Open($source, $false, $false)
    $doc.Fields.Update() | Out-Null
    foreach ($toc in $doc.TablesOfContents) { $toc.Update() }
    $doc.Repaginate()
    if (-not $VerifyFinal) { $doc.SaveAs2((Join-Path $art 'word_updated.docx'), 16) }
    $pdfName = if ($VerifyFinal) { 'final_verified.pdf' } else { 'final.pdf' }
    $doc.ExportAsFixedFormat((Join-Path $art $pdfName), 17)
    Write-Output ('Pages: ' + $doc.ComputeStatistics(2))
} finally {
    if ($null -ne $doc) { $doc.Close(0) }
    if ($null -ne $word) { $word.Quit() }
}
