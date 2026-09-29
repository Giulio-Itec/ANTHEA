param([Parameter(Mandatory=$true)][string]$Root, [Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path -LiteralPath $Root).Path
$taskOutput = [IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskWord = $null
try {
    $taskWord = New-Object -ComObject Word.Application
    $taskWord.Visible = $false
    $taskWord.DisplayAlerts = 0
    $taskFiles = @(Get-Item -LiteralPath (Join-Path $taskRoot 'Rapporto-controllo-stabilita-globale.docx')) + @(Get-ChildItem -Path (Join-Path $taskRoot 'esempi/*/relazione-anthea.docx'))
    foreach ($taskFile in $taskFiles) {
        $taskDoc = $null
        try {
            $taskName = if ($taskFile.Name -eq 'relazione-anthea.docx') { $taskFile.Directory.Name } else { $taskFile.BaseName }
            $taskDoc = $taskWord.Documents.Open($taskFile.FullName, $false, $true)
            $taskDoc.Repaginate()
            $taskDoc.ExportAsFixedFormat((Join-Path $taskOutput ($taskName + '.pdf')), 17)
            Write-Output ($taskName + ': ' + $taskDoc.ComputeStatistics(2) + ' pagine')
        } finally { if ($null -ne $taskDoc) { $taskDoc.Close(0) } }
    }
} finally { if ($null -ne $taskWord) { $taskWord.Quit() } }
