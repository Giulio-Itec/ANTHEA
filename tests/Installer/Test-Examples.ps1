param([string]$Output, [string]$MakeNsis = "${env:ProgramFiles(x86)}\NSIS\makensis.exe")
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null
. (Join-Path $root 'supporto/installer/Esempi.ps1')
$checks = 0
function Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
function Reject([scriptblock]$action, [string]$message) {
    $failed = $false
    try { & $action | Out-Null } catch { $failed = $true }
    Check $failed $message
}
$files = @(Get-InstallerExamples $root)
Check ($files.Count -ge 31) 'Catalogo insufficiente'
$stage = Join-Path $Output 'stage'
Write-InstallerExamples $files $stage
$source = Get-Content (Join-Path $root 'supporto/installer/ANTHEA.nsi') -Raw -Encoding UTF8
$section = [regex]::Match($source, '(?ms)^Section "Esempi per tutti i moduli" SecExamples\r?\n.*?^SectionEnd').Value
Check ($section.Length -gt 0 -and $section -notmatch 'SectionIn RO|Section /o') 'Componente selezionabile e selezionato di default'
$removal = Get-Content (Join-Path $stage 'uninstall-examples.nsh') -Raw
Check ($removal -notmatch '(?i)RMDir\s+/r|Delete\s+"[^"\r\n]*[?*]') 'Rimozione non limitata agli esempi'
foreach ($name in 'example-shortcuts.nsh','uninstall-example-shortcuts.nsh') {
    $path = Join-Path $stage $name
    [IO.File]::WriteAllText($path, (Get-Content $path -Raw).Replace('$SMPROGRAMS\${APP}', '$INSTDIR\Start'), (New-Object Text.UTF8Encoding $true))
}
$harness = @'
Unicode true
RequestExecutionLevel user
Name "ANTHEA - prova esempi"
OutFile "${SETUP}"
SilentInstall silent
SilentUnInstall silent
!include LogicLib.nsh
'@ + "`r`n" + $section + @'

Section "-TestUninstaller"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd
Section "Uninstall"
  !include "${STAGE}\uninstall-examples.nsh"
  !include "${STAGE}\uninstall-example-shortcuts.nsh"
  RMDir "$INSTDIR\Start"
  FileOpen $0 "${TRACE}" w
  FileWrite $0 "completed"
  FileClose $0
SectionEnd
'@
$script = Join-Path $Output 'examples.nsi'
[IO.File]::WriteAllText($script, $harness, (New-Object Text.UTF8Encoding $true))
$setup = Join-Path $Output 'examples.exe'
& $MakeNsis /V3 /INPUTCHARSET UTF8 "/DSTAGE=$stage" "/DSETUP=$setup" "/DTRACE=$(Join-Path $Output 'uninstall.txt')" $script > (Join-Path $Output 'makensis.log')
Check ($LASTEXITCODE -eq 0) 'Compilazione sezione NSIS'
$installed = Join-Path ([IO.Path]::GetTempPath()) ('ANTHEA-Examples-' + [guid]::NewGuid().ToString('N'))
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$installed" -WindowStyle Hidden -PassThru -Wait
Check ($process.ExitCode -eq 0) 'Installazione esempi'
foreach ($file in $files) {
    Check ((Get-FileHash -LiteralPath $file.Source).Hash -eq (Get-FileHash -LiteralPath (Join-Path $installed ('Esempi\' + $file.Target))).Hash) "File installato: $($file.Target)"
}
$shell = New-Object -ComObject WScript.Shell
try { Check ($shell.CreateShortcut((Join-Path $installed 'Start\Esempi.lnk')).TargetPath -eq (Join-Path $installed 'Esempi')) 'Collegamento Esempi' }
finally { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
$personal = Join-Path $installed 'Esempi\personale.anthea'
[IO.File]::WriteAllText($personal, 'conservare')
$process = Start-Process -FilePath (Join-Path $installed 'Uninstall.exe') -ArgumentList "/S _?=$installed" -WindowStyle Hidden -PassThru -Wait
Check ($process.ExitCode -eq 0 -and (Test-Path (Join-Path $Output 'uninstall.txt'))) 'Disinstallazione completata'
foreach ($file in $files) { Check (-not (Test-Path -LiteralPath (Join-Path $installed ('Esempi\' + $file.Target)))) "File rimosso: $($file.Target)" }
Check ((Get-Content -LiteralPath $personal -Raw) -eq 'conservare') 'File personale conservato'
Check (-not (Test-Path (Join-Path $installed 'Start'))) 'Collegamenti rimossi'

# Validate corrupt input in an isolated fixture, never change the shipped catalog.
$fixture = Join-Path $Output 'fixture'
$folder = Join-Path $fixture 'supporto/esempi/distribuzione'
New-Item -ItemType Directory -Force $folder | Out-Null
$original = Get-Content -LiteralPath $files[0].Source -Raw -Encoding UTF8 | ConvertFrom-Json
$first = $original.entries[0]
New-Item -ItemType Directory -Force (Join-Path $folder $first.module) | Out-Null
$example = Join-Path $folder $first.path
Copy-Item -LiteralPath $files[1].Source -Destination $example
function Catalog([scriptblock]$edit) {
    $copy = $original | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $copy.entries = @($copy.entries[0])
    & $edit $copy
    $copy | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $folder 'catalogo.json') -Encoding UTF8
}
Catalog { param($j) $j.entries[0].path = '../outside.anthea' }
Reject { Get-InstallerExamples $fixture } 'Percorso esterno rifiutato'
Catalog { param($j) $j.entries = @($j.entries[0], $j.entries[0]) }
Reject { Get-InstallerExamples $fixture } 'Duplicato rifiutato'
Catalog { param($j) $j.entries[0].path = $j.entries[0].module + '/missing.anthea' }
Reject { Get-InstallerExamples $fixture } 'File assente rifiutato'
Catalog { param($j) $j.version = 2 }
Reject { Get-InstallerExamples $fixture } 'Versione rifiutata'
Catalog { }
[IO.File]::AppendAllText($example, ' ')
Reject { Get-InstallerExamples $fixture } 'Hash alterato rifiutato'
[IO.File]::WriteAllText((Join-Path $Output 'esito.txt'), "PASS $checks controlli installer esempi`n")
Write-Host "PASS $checks"
