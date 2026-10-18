param([Parameter(Mandatory)][string]$Source,[Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop'
if (Test-Path -LiteralPath $Output) { throw 'Gli attesi esistenti non si sovrascrivono.' }
$excel=New-Object -ComObject Excel.Application
$wb=$null
function Matrix($sheet,[string]$address) {
    $v=$sheet.Range($address).Value2; $rows=@()
    for($r=1;$r -le $v.GetLength(0);$r++) { $line=@(); for($c=1;$c -le $v.GetLength(1);$c++) {$line+=,$v[$r,$c]}; $rows+=,$line }
    return ,$rows
}
try {
    $excel.Visible=$false; $excel.DisplayAlerts=$false; $excel.AutomationSecurity=3; $excel.EnableEvents=$false
    $wb=$excel.Workbooks.Open([IO.Path]::GetFullPath($Source),0,$true)
    $excel.Calculation=-4135
    $f1=$wb.Worksheets.Item('Fase1');$f2=$wb.Worksheets.Item('Fase2');$f3=$wb.Worksheets.Item('Fase3')
    $d1=$wb.Worksheets.Item('D1b');$d2=$wb.Worksheets.Item('D2a');$d3=$wb.Worksheets.Item('D3a')
    $sites=@(@('Aglie',7.768,45.368),@('Roma',12.4818,41.9109),@('LAquila',13.398,42.349),@('Reggio',15.65,38.11),@('Cagliari',9.11,39.22),@('Palermo',13.36,38.12),@('Trieste',13.77,45.65),@('Milano',9.19,45.46),@('Nodo',7.729,45.349),@('Fuori',2.35,48.85))
    $results=@();$n=0
    foreach($site in $sites) { foreach($method in 1,2) {
        $n++; $vn=@(50,10,100,200,1000)[$n%5];$cu=@(1,1.5,2)[$n%3];$soil=1+($n%5);$topo=1+($n%4)
        $d1.Range('B2').Value2=2;$d1.Range('AF18').Value2=[double]$method;$f1.Range('F5').Value2=[double]$site[1];$f1.Range('H5').Value2=[double]$site[2]
        $f2.Range('H5').Value2=[double]$vn;$f2.Range('H7').Value2=[double]$cu
        $d3.Range('B1').Value2=[double]$soil;$d3.Range('C1').Value2=[double]$topo
        $mode=1+($n%2);$regular=1+($n%2);$height=@(0,.5,1)[$n%3];$damping=@(5,10,30)[$n%3];$q0=@(1,3,6)[$n%3];$qv=@(1,1.5)[$n%2]
        $d3.Range('B8').Value2=[double]$mode;$d3.Range('D8').Value2=[double]$regular
        $f3.Range('H12').Value2=[double]$height;$f3.Range('H16').Value2=[double]$damping;$f3.Range('H18').Value2=[double]$q0;$f3.Range('H22').Value2=[double]$qv
        $excel.CalculateFull()
        $entry=[ordered]@{name=$site[0];longitude=$site[1];latitude=$site[2];method=$method;nominal=$vn;use=$cu;soil=$soil;topography=$topo;height=$height;damping=$damping;mode=$mode;regular=$regular;q0=$q0;qv=$qv;status=$d1.Range('AG79').Value2;kind=$d1.Range('AC51').Value2;periods=(Matrix $d2 'A2:D10');states=@()}
        if($entry.status -ne 0) { foreach($state in 1..4) {
            $d3.Range('A1').Value2=[double]$state; $excel.Calculate()
            $entry.states+=,[ordered]@{state=$state;parameters=(Matrix $d3 'G3:N5');horizontal=(Matrix $d3 'G8:H52');reference=(Matrix $d3 'I8:J52');vertical=(Matrix $d3 'K8:L52');tr=$d2.Cells.Item(1,4+$state).Value2}
        } }
        $results+=,$entry; Write-Output "Acquisito $($site[0]), metodo $method, stato reticolo $($entry.status)"
    } }
    $results|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $Output -Encoding utf8
} finally { if($wb){$wb.Close($false);[Runtime.InteropServices.Marshal]::ReleaseComObject($wb)|Out-Null};$excel.Quit();[Runtime.InteropServices.Marshal]::ReleaseComObject($excel)|Out-Null }
