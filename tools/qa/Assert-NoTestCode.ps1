<#
Checks that shipped assemblies contain no test code (refactoring F1.2).

  powershell -NoProfile -ExecutionPolicy Bypass -File tools\qa\Assert-NoTestCode.ps1 -Path X.Desktop\bin\Release\net8.0-windows
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\qa\Assert-NoTestCode.ps1 -Path <publish>\ANTHEA.dll,<publish>\Materiali.dll

-Path accepts assemblies or folders; in a folder the ANTHEA assemblies (ANTHEA.dll, ANTHEA.Core.dll,
ANTHEA.Calculations.dll, Materiali.dll) are checked. The script reads the ECMA-335 metadata of each assembly and
searches only two heaps, so that embedded resources (Wiki guides, figures) can never give false positives:
  #Strings  names of types, members and namespaces (UTF-8): identifiers of the WPF checks;
  #US       string literals (UTF-16): command line flags and files of the test harness.
Exit code 0 when every assembly is clean, 1 when a marker is found or a file is missing or not a .NET assembly.
The UiTests build (X.Desktop\bin\UiTests\...) must fail: it is the counter-check of the markers.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string[]] $Path,
    # Regular expressions on the names of the #Strings heap (case sensitive).
    [string[]] $Identifier = @(
        'Smoke', 'WikiChecks', 'GlobalGuidanceChecks', 'WallAdvancedChecks', 'WallMaterialChecks', 'CheckAppearance', 'CheckWikiIntegration',
        'RunTestHarness', 'CheckErrorLog', 'VerifyChs', 'VerifyHorizontal', 'WaitForAutomatic', 'BoxInputsVisible', 'TorsionResultsVisible',
        'TorqueColumnVisible', 'RevealTorsion', 'CheckBond', 'CheckAutomaticMix', 'CheckExposureSelector', 'DurabilityReferenceChecks',
        'CheckDurability', 'CheckNtcCover'),
    # Names that contain a marker but belong to the framework (System.Windows.Media.Brushes.WhiteSmoke).
    [string[]] $AllowedIdentifier = @('WhiteSmoke', 'get_WhiteSmoke'),
    # Regular expressions on the string literals of the #US heap.
    [string[]] $Literal = @('^--smoke', '^--check', 'esito-smoke-completo')
)
$ErrorActionPreference = 'Stop'
$DefaultAssemblies = 'ANTHEA.dll', 'ANTHEA.Core.dll', 'ANTHEA.Calculations.dll', 'Materiali.dll'

function Read-Heaps([string] $file) {
    $b = [IO.File]::ReadAllBytes($file)
    if ($b.Length -lt 0x40 -or $b[0] -ne 0x4D -or $b[1] -ne 0x5A) { throw "non è un file PE" }
    $pe = [BitConverter]::ToInt32($b, 0x3C)
    if ([BitConverter]::ToUInt32($b, $pe) -ne 0x4550) { throw "firma PE assente" }
    $sectionCount = [BitConverter]::ToUInt16($b, $pe + 6)
    $optionalSize = [BitConverter]::ToUInt16($b, $pe + 20)
    $optional = $pe + 24
    $directories = if ([BitConverter]::ToUInt16($b, $optional) -eq 0x20B) { $optional + 112 } else { $optional + 96 }
    $sections = $optional + $optionalSize
    $toOffset = {
        param([long] $rva)
        for ($i = 0; $i -lt $sectionCount; $i++) {
            $s = $sections + 40 * $i
            [long] $va = [BitConverter]::ToUInt32($b, $s + 12)
            [long] $size = [Math]::Max([long][BitConverter]::ToUInt32($b, $s + 8), [long][BitConverter]::ToUInt32($b, $s + 16))
            if ($rva -ge $va -and $rva -lt $va + $size) { return [int]($rva - $va + [BitConverter]::ToUInt32($b, $s + 20)) }
        }
        throw "RVA $rva fuori dalle sezioni"
    }
    [long] $cliRva = [BitConverter]::ToUInt32($b, $directories + 14 * 8)
    if ($cliRva -eq 0) { throw "non è un assembly .NET (intestazione CLI assente)" }
    $cli = & $toOffset $cliRva
    $root = & $toOffset ([long][BitConverter]::ToUInt32($b, $cli + 8))
    if ([BitConverter]::ToUInt32($b, $root) -ne 0x424A5342) { throw "radice dei metadati non valida" }
    $versionLength = [BitConverter]::ToInt32($b, $root + 12)
    $streamCount = [BitConverter]::ToUInt16($b, $root + 16 + $versionLength + 2)
    $p = $root + 16 + $versionLength + 4
    $heaps = @{}
    for ($i = 0; $i -lt $streamCount; $i++) {
        $offset = [BitConverter]::ToInt32($b, $p); $size = [BitConverter]::ToInt32($b, $p + 4); $p += 8
        $end = $p; while ($b[$end] -ne 0) { $end++ }
        $name = [Text.Encoding]::ASCII.GetString($b, $p, $end - $p)
        $p += (($end - $p) + 4) -band -bnot 3  # name and terminator padded to 4 bytes
        $heaps[$name] = @{ Start = $root + $offset; Size = $size }
    }
    if (-not $heaps.ContainsKey('#Strings')) { throw "heap #Strings assente" }

    $names = New-Object System.Collections.Generic.List[string]
    $h = $heaps['#Strings']; $start = $h.Start; $last = $h.Start + $h.Size
    for ($i = $h.Start; $i -lt $last; $i++) {
        if ($b[$i] -eq 0) { if ($i -gt $start) { $names.Add([Text.Encoding]::UTF8.GetString($b, $start, $i - $start)) }; $start = $i + 1 }
    }
    $literals = New-Object System.Collections.Generic.List[string]
    if ($heaps.ContainsKey('#US')) {
        $h = $heaps['#US']; $i = $h.Start + 1; $last = $h.Start + $h.Size
        while ($i -lt $last) {
            $first = $b[$i]
            if (($first -band 0x80) -eq 0) { $length = $first; $i += 1 }
            elseif (($first -band 0xC0) -eq 0x80) { $length = (($first -band 0x3F) -shl 8) -bor $b[$i + 1]; $i += 2 }
            else { $length = (($first -band 0x1F) -shl 24) -bor ($b[$i + 1] -shl 16) -bor ($b[$i + 2] -shl 8) -bor $b[$i + 3]; $i += 4 }
            if ($length -eq 0) { continue }
            if ($length -gt 1) { $literals.Add([Text.Encoding]::Unicode.GetString($b, $i, $length - 1)) }
            $i += $length
        }
    }
    [pscustomobject]@{ Names = $names; Literals = $literals }
}

$files = New-Object System.Collections.Generic.List[string]
foreach ($item in $Path) {
    # Relative to the PowerShell location, not to the process directory (the script may run in-process).
    $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($item)
    if (Test-Path -LiteralPath $full -PathType Container) {
        $found = @($DefaultAssemblies | ForEach-Object { Join-Path $full $_ } | Where-Object { Test-Path -LiteralPath $_ })
        if ($found.Count -eq 0) { Write-Output "FAIL: $full`: nessun assembly ANTHEA nella cartella"; $files.Add('') }
        foreach ($f in $found) { $files.Add($f) }
    } else { $files.Add($full) }
}

$here = (Get-Location).ProviderPath.TrimEnd('\') + '\'
function Show([string] $file) { if ($file.StartsWith($here, [StringComparison]::OrdinalIgnoreCase)) { $file.Substring($here.Length) } else { $file } }

$failed = 0
foreach ($fullName in $files) {
    if (-not $fullName) { $failed++; continue }
    $file = Show $fullName
    if (-not (Test-Path -LiteralPath $fullName -PathType Leaf)) { Write-Output "FAIL: $file`: file assente"; $failed++; continue }
    try { $heaps = Read-Heaps $fullName } catch { Write-Output "FAIL: $file`: $($_.Exception.Message)"; $failed++; continue }
    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($n in (New-Object 'System.Collections.Generic.SortedSet[string]' -ArgumentList @($heaps.Names, [StringComparer]::Ordinal))) {
        if ($AllowedIdentifier -ccontains $n) { continue }
        foreach ($pattern in $Identifier) { if ($n -cmatch $pattern) { $hits.Add("nome '$n'"); break } }
    }
    foreach ($l in (New-Object 'System.Collections.Generic.SortedSet[string]' -ArgumentList @($heaps.Literals, [StringComparer]::Ordinal))) {
        foreach ($pattern in $Literal) { if ($l -cmatch $pattern) { $hits.Add("testo '$l'"); break } }
    }
    if ($hits.Count -gt 0) {
        $failed++
        Write-Output ("FAIL: {0}: {1} marcatori di prova" -f $file, $hits.Count)
        $hits | Select-Object -First 25 | ForEach-Object { Write-Output "     $_" }
        if ($hits.Count -gt 25) { Write-Output "     ... altri $($hits.Count - 25)" }
    } else {
        Write-Output ("PASS: {0} senza marcatori di prova" -f $file)
        Write-Output ("     letti {0} nomi e {1} testi" -f $heaps.Names.Count, $heaps.Literals.Count)
    }
}
if ($failed -gt 0) { exit 1 }
exit 0
