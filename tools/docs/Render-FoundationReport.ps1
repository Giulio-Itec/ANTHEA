param([Parameter(Mandatory=$true)][string]$Document)
$ErrorActionPreference='Stop'
$documentPath=(Resolve-Path -LiteralPath $Document).Path
$pdfPath=[IO.Path]::ChangeExtension($documentPath,'.word-render.pdf')
$word=$null
$doc=$null
try {
    $word=New-Object -ComObject Word.Application
    $word.Visible=$false
    $word.DisplayAlerts=0
    $word.AutomationSecurity=3
    $doc=$word.Documents.Open([string]$documentPath,$false,$true)
    $doc.Repaginate()
    $doc.ExportAsFixedFormat([string]$pdfPath,17)
    Write-Output $pdfPath
} finally {
    if($null -ne $doc){$doc.Close(0);[Runtime.InteropServices.Marshal]::ReleaseComObject($doc)|Out-Null}
    if($null -ne $word){$word.Quit();[Runtime.InteropServices.Marshal]::ReleaseComObject($word)|Out-Null}
}
