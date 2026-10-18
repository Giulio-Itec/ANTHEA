param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$outputPath = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$excel = New-Object -ComObject Excel.Application
$workbook = $null
try {
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $excel.AutomationSecurity = 3 # Disable macros; never run code in a reference workbook.
    $excel.EnableEvents = $false
    $workbook = $excel.Workbooks.Open($sourcePath, 0, $true)
    $metadata = @()
    foreach ($sheet in $workbook.Worksheets) {
        $metadata += [pscustomobject]@{ Name=$sheet.Name; Range=$sheet.UsedRange.Address(); Visible=$sheet.Visible }
    }
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'sheets.json') -Encoding utf8
    $metadata | Format-Table | Out-String | Write-Output
    $workbook.SaveAs((Join-Path $outputPath 'reference.xlsx'), 51)
} finally {
    if ($null -ne $workbook) { $workbook.Close($false); [Runtime.InteropServices.Marshal]::ReleaseComObject($workbook) | Out-Null }
    $excel.Quit()
    [Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
