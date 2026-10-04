$ErrorActionPreference='Stop'
$rootPath=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$wordForPile=$null
$pileDoc=$null
try {
    $wordForPile=New-Object -ComObject Word.Application
    $wordForPile.Visible=$false
    $wordForPile.DisplayAlerts=0
    $wordForPile.AutomationSecurity=3
    foreach($relative in @('supporto/artefatti/palo-elastico/finale-rev13/esempio-viggiani-finale.docx','supporto/artefatti/palificata-ui/esempio-palificata.docx')) {
        $sourcePath=[string](Join-Path $rootPath $relative)
        $pileDoc=$wordForPile.Documents.Open($sourcePath,$false,$true)
        $pileDoc.ExportAsFixedFormat([string][IO.Path]::ChangeExtension($sourcePath,'.pdf'),17)
        Write-Output ($relative+': '+$pileDoc.ComputeStatistics(2)+' pagine')
        $pileDoc.Close(0)
        [Runtime.InteropServices.Marshal]::ReleaseComObject($pileDoc)|Out-Null
        $pileDoc=$null
    }
}finally{if($null -ne $pileDoc){$pileDoc.Close(0)};if($null -ne $wordForPile){$wordForPile.Quit();[Runtime.InteropServices.Marshal]::ReleaseComObject($wordForPile)|Out-Null}}
