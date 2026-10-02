param(
    [string]$MakeNsis = "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
    [switch]$SkipUninstall
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
. (Join-Path $root 'supporto\installer\Documentazione.ps1')
$work = Join-Path $root ('supporto\artefatti\installer-guide\test-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Force $work | Out-Null
$checks = New-Object System.Collections.Generic.List[string]
function Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $checks.Add("OK: $message")
    Write-Host "OK: $message"
}
function Expect-Failure([scriptblock]$action, [string]$message) {
    $failed = $false
    try { & $action | Out-Null } catch { $failed = $true }
    Check $failed $message
}
function Fixture-Pdf([string]$path) {
    [IO.File]::WriteAllText($path, '%PDF-1.4 fixture', [Text.Encoding]::ASCII)
}

$catalog = Get-InstallerGuides $root
Check ($catalog.Entries.Count -eq 3) 'Tre PDF: due guide globali complete e indice'
$stage = Join-Path $work 'stage'
Write-InstallerGuides $catalog $stage
foreach ($entry in $catalog.Entries) {
    Check ((Get-FileHash $entry.Source).Hash -eq (Get-FileHash (Join-Path $stage ('guide\' + $entry.Target))).Hash) "Copia esatta: $($entry.Target)"
}

# Independent fixture: no modifications to the repository's published documentation.
$fixture = Join-Path $work 'repository'
$revisions = Join-Path $fixture 'supporto\documentazione\Guide_ANTHEA'
$fixtureInstaller = Join-Path $fixture 'supporto\installer'
$fixtureDocs = Join-Path $fixture 'supporto\docs'
New-Item -ItemType Directory -Force $revisions, $fixtureInstaller, $fixtureDocs | Out-Null
foreach ($kind in 'pratica', 'teorica') {
    foreach ($rev in '9', '10') { Fixture-Pdf (Join-Path $revisions "ANTHEA_Guida_${kind}_ITEC_Rev$rev.pdf") }
}
$fixtureManifest = Join-Path $fixtureInstaller 'Guide.json'
'[{"source":"supporto/docs/modulo.pdf","target":"modulo.pdf","shortcut":"Modulo"}]' |
    Set-Content -LiteralPath $fixtureManifest -Encoding UTF8
$module = Join-Path $fixtureDocs 'modulo.pdf'
Fixture-Pdf $module
$selected = Get-InstallerGuides $fixture
Check ($selected.Revision -eq '10') 'Selezione numerica Rev10 dopo Rev9'
Fixture-Pdf (Join-Path $revisions 'ANTHEA_Guida_pratica_ITEC_Rev11.pdf')
Expect-Failure { Get-InstallerGuides $fixture } 'Revisioni generali disallineate bloccano la build'
Fixture-Pdf (Join-Path $revisions 'ANTHEA_Guida_teorica_ITEC_Rev11.pdf')
Remove-Item -LiteralPath $module
Expect-Failure { Get-InstallerGuides $fixture } 'PDF di modulo mancante blocca la build'
[IO.File]::WriteAllText($module, 'non e un PDF')
Expect-Failure { Get-InstallerGuides $fixture } 'File senza intestazione PDF blocca la build'
Fixture-Pdf $module
'[{"source":"supporto/docs/modulo.pdf","target":"Guida pratica ANTHEA.pdf","shortcut":"Modulo"}]' |
    Set-Content -LiteralPath $fixtureManifest -Encoding UTF8
Expect-Failure { Get-InstallerGuides $fixture } 'Destinazioni duplicate bloccano la build'

# Exercise the actual NSIS documentation section and generated removal lists.
# Redirect Start shortcuts into the test directory; do not touch registry, desktop or installed ANTHEA.
$source = Get-Content (Join-Path $root 'supporto\installer\ANTHEA.nsi') -Raw -Encoding UTF8
$section = [regex]::Match($source, '(?ms)^Section "Documentazione e guide in PDF" SecGuides\r?\n.*?^SectionEnd').Value
Check ($section -match 'SectionIn RO') 'Documentazione obbligatoria nel setup reale'
$removal = Get-Content (Join-Path $stage 'uninstall-guides.nsh') -Raw -Encoding UTF8
Check ($removal -notmatch '(?i)RMDir\s+/r|Delete\s+"[^"\r\n]*[?*]') 'Rimozione limitata ai file elencati, senza cancellazioni ricorsive o wildcard'
foreach ($entry in $catalog.Entries) {
    Check ($removal.Contains('Delete "$INSTDIR\Guide\' + $entry.Target + '"')) "Elenco disinstallazione: $($entry.Target)"
}
foreach ($name in 'guide-shortcuts.nsh', 'uninstall-guide-shortcuts.nsh') {
    $path = Join-Path $stage $name
    $text = (Get-Content $path -Raw -Encoding UTF8).Replace('$SMPROGRAMS\${APP}', '$INSTDIR\Start')
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding $true))
}
$harness = @'
Unicode true
RequestExecutionLevel user
Name "ANTHEA - prova documentazione"
OutFile "${SETUP}"
SilentInstall silent
SilentUnInstall silent
!include LogicLib.nsh
Function un.onInit
  FileOpen $0 "${TRACE}" w
  FileWrite $0 'Command: $CMDLINE$\r$\nDirectory: $INSTDIR$\r$\n'
  FileClose $0
FunctionEnd
'@ + "`r`n" + $section + @'

Section "-TestUninstaller"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd
Section "Uninstall"
  FileOpen $9 "${TRACE}" a
  FileSeek $9 0 END
  FileWrite $9 'Removal started: $INSTDIR$\r$\n'
  ClearErrors
  !include "${STAGE}\uninstall-guides.nsh"
  ${If} ${Errors}
    FileWrite $9 'Removal reported errors$\r$\n'
  ${EndIf}
  !include "${STAGE}\uninstall-guide-shortcuts.nsh"
  RMDir "$INSTDIR\Start"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  FileWrite $9 'Removal completed: $INSTDIR$\r$\n'
  FileClose $9
SectionEnd
'@
$harnessPath = Join-Path $work 'test-guide.nsi'
[IO.File]::WriteAllText($harnessPath, $harness, (New-Object Text.UTF8Encoding $true))
$setup = Join-Path $work 'test-guide.exe'
& $MakeNsis /V3 /INPUTCHARSET UTF8 "/DSTAGE=$stage" "/DSETUP=$setup" "/DTRACE=$(Join-Path $work 'uninstall.log')" $harnessPath > (Join-Path $work 'makensis.log')
Check ($LASTEXITCODE -eq 0) 'Compilazione NSIS della sezione documentazione'
$installed = Join-Path $work 'installazione con spazi'
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$installed" -WindowStyle Hidden -PassThru -Wait
Check ($process.ExitCode -eq 0) 'Installazione silenziosa della documentazione'
$shell = New-Object -ComObject WScript.Shell
try {
    foreach ($entry in $catalog.Entries) {
        $pdf = Join-Path $installed ('Guide\' + $entry.Target)
        Check ((Get-FileHash $entry.Source).Hash -eq (Get-FileHash $pdf).Hash) "PDF installato: $($entry.Target)"
        $link = Join-Path $installed ('Start\' + $entry.Shortcut + '.lnk')
        Check ((Test-Path $link) -and $shell.CreateShortcut($link).TargetPath -eq $pdf) "Collegamento valido: $($entry.Shortcut)"
    }
    $folderLink = Join-Path $installed 'Start\Documentazione.lnk'
    Check ($shell.CreateShortcut($folderLink).TargetPath -eq (Join-Path $installed 'Guide')) 'Collegamento alla cartella Guide'
}
finally { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
if ($SkipUninstall) {
    $checks | Set-Content -LiteralPath (Join-Path $work 'checks.log') -Encoding UTF8
    Write-Host "Esito: $($checks.Count) controlli superati; disinstallazione NON eseguita. Artefatti: $work"
    exit 0
}
$personal = Join-Path $installed 'Guide\note-personali.txt'
[IO.File]::WriteAllText($personal, 'Documento utente da conservare')
$process = Start-Process -FilePath (Join-Path $installed 'Uninstall.exe') -ArgumentList "/S _?=$installed" -WindowStyle Hidden -PassThru -Wait
Check ($process.ExitCode -eq 0) 'Disinstallazione silenziosa della documentazione'
Check ((Get-Content (Join-Path $work 'uninstall.log') -Raw) -match 'Removal completed:') 'Il disinstallatore raggiunge la fine della sezione di rimozione'
foreach ($entry in $catalog.Entries) {
    Check (-not (Test-Path (Join-Path $installed ('Guide\' + $entry.Target)))) "PDF rimosso: $($entry.Target)"
}
Check ((Get-Content $personal -Raw) -eq 'Documento utente da conservare') 'Conservazione dei documenti personali durante la disinstallazione'
Check (-not (Test-Path (Join-Path $installed 'Start'))) 'Rimozione dei soli collegamenti installati'
$checks | Set-Content -LiteralPath (Join-Path $work 'checks.log') -Encoding UTF8
Write-Host "Esito: $($checks.Count) controlli superati. Artefatti: $work"
