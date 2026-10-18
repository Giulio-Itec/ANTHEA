param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Sites,
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Output) { throw 'Gli attesi esistenti non si sovrascrivono.' }
$excel = New-Object -ComObject Excel.Application
$workbook = $null
function Matrix($sheet, [string]$address) {
    $values = $sheet.Range($address).Value2
    $rows = @()
    for ($row = 1; $row -le $values.GetLength(0); $row++) {
        $line = @()
        for ($column = 1; $column -le $values.GetLength(1); $column++) { $line += ,$values[$row, $column] }
        $rows += ,$line
    }
    return ,$rows
}
try {
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $excel.AutomationSecurity = 3
    $excel.EnableEvents = $false
    $sourcePath = [IO.Path]::GetFullPath($Source)
    $workbook = $excel.Workbooks.Open($sourcePath, 0, $true)
    $excel.Calculation = -4135
    $phase = $workbook.Worksheets.Item('Fase1')
    $grid = $workbook.Worksheets.Item('D1b')
    $periods = $workbook.Worksheets.Item('D2a')
    $cases = @()
    foreach ($site in (Get-Content -LiteralPath $Sites -Raw | ConvertFrom-Json)) {
        foreach ($method in 1, 2) {
            $grid.Range('B2').Value2 = 2
            $grid.Range('AF18').Value2 = [double]$method
            $phase.Range('F5').Value2 = [double]$site.longitude
            $phase.Range('H5').Value2 = [double]$site.latitude
            $excel.CalculateFull()
            $cases += [ordered]@{
                name = $site.name; longitude = $site.longitude; latitude = $site.latitude; method = $method
                status = $grid.Range('AG79').Value2; kind = $grid.Range('AC51').Value2
                selected = (Matrix $grid 'AI16:FD16'); periods = (Matrix $periods 'A2:D10')
            }
        }
    }
    [ordered]@{ source = $sourcePath; sha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash; cases = $cases } |
        ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Output -Encoding utf8
} finally {
    if ($workbook) { $workbook.Close($false); [Runtime.InteropServices.Marshal]::ReleaseComObject($workbook) | Out-Null }
    $excel.Quit()
    [Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
