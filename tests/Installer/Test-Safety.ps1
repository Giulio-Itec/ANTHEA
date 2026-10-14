param(
    [string]$Output,
    [string]$MakeNsis = "${env:ProgramFiles(x86)}\NSIS\makensis.exe"
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $Output) { $Output = Join-Path $repo ('supporto\artefatti\installer-safety\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $Output | Out-Null
# Junction fixtures are OUTSIDE the repository (AGENTS.md). Retain the small
# fixtures for inspection; remove only the two links explicitly in finally.
$fixture = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Temp\ANTHEA-InstallerTest-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$checks = [Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $checks.Add($Message)
    Write-Host "PASS $Message"
}
function Compile([string]$Source, [string]$Destination, [string[]]$Defines = @()) {
    & $MakeNsis /V2 /INPUTCHARSET UTF8 "/DSETUP=$Destination" @Defines $Source > "$Destination.build.log" 2>&1
    if ($LASTEXITCODE -ne 0) { throw (Get-Content "$Destination.build.log" -Raw) }
}
function Run([string]$Setup, [string]$Destination) {
    $process = Start-Process -FilePath $Setup -ArgumentList "/S /D=$Destination" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(20000)) { $process.Kill(); throw 'Isolated installer exceeded 20 seconds' }
    $process.Refresh()
    return $process.ExitCode
}
function Seed([string]$Name) {
    $path = Join-Path $fixture "$Name\ANTHEA"
    New-Item -ItemType Directory -Force -Path "$path\old\nested" | Out-Null
    [IO.File]::WriteAllText("$path\old\nested\obsolete.dll", 'old dll')
    [IO.File]::WriteAllText("$path\user-added.txt", 'previous file')
    return $path
}
$setup = Join-Path $Output 'safety.exe'
$validate = Join-Path $Output 'validate.exe'
$trace = Join-Path $fixture 'previous-uninstaller.txt'
$defines = @("/DSAFETY=$repo\supporto\installer\InstallSafety.nsh", "/DTESTROOT=$fixture", "/DTRACE=$trace")
Compile "$PSScriptRoot\SafetyHarness.nsi" $setup $defines
Compile "$PSScriptRoot\SafetyHarness.nsi" $validate ($defines + '/DVALIDATE_ONLY')
Check $true 'The real NSIS safety implementation compiles'

$source = Get-Content "$repo\supporto\installer\ANTHEA.nsi" -Raw -Encoding UTF8
Check ($source -match '(?s)Section "\$\{APP\}" SecApp\s+SectionIn RO\s+Call CheckNotRunning\s+Call ValidateInstallDirectory\s+Call RemovePreviousVersion\s+Call CleanInstallDirectory\s+;[^\r\n]*\s+SetOutPath') 'Real setup guards precede old uninstaller, cleanup and file copy'
Check ($source -match '(?s)Function .onInit.*?Call CheckNotRunning.*?FunctionEnd') 'Real setup blocks a running application at startup'

$outside = Join-Path $fixture 'outside.txt'
[IO.File]::WriteAllText($outside, 'preserve outside')
$target = Seed 'cleanup with spaces'
$hidden = "$target\hidden.dll"
[IO.File]::WriteAllText($hidden, 'hidden')
[IO.File]::SetAttributes($hidden, [IO.FileAttributes]::Hidden)
Check ((Run $setup $target) -eq 0) 'Upgrade succeeds after cleaning the application folder'
Check (@(Get-ChildItem -LiteralPath $target -Force -Recurse).Count -eq 1) 'All previous files, nested folders and hidden files are removed'
Check ([IO.File]::ReadAllText("$target\new-payload.txt") -eq '1.8.0') 'New payload is copied after successful cleanup'
Check ([IO.File]::ReadAllText($outside) -eq 'preserve outside') 'Sibling files outside the application folder are preserved'
Check ((Run $setup (Join-Path $fixture 'fresh\ANTHEA')) -eq 0) 'Fresh installation into a missing directory succeeds'

$unrelated = Join-Path $fixture 'unrelated'
New-Item -ItemType Directory $unrelated | Out-Null
[IO.File]::WriteAllText("$unrelated\keep.txt", 'preserve')
Remove-Item -LiteralPath $trace
Check ((Run $setup $unrelated) -eq 11) 'Populated unrelated directory is rejected'
Check ((Test-Path "$unrelated\keep.txt") -and -not (Test-Path $trace)) 'Invalid destination is rejected before the previous uninstaller'
foreach ($protected in @([IO.Path]::GetPathRoot($fixture), $env:SystemRoot, $env:USERPROFILE, ${env:ProgramFiles})) {
    Check ((Run $validate $protected) -eq 11) "Protected path rejected without mutation: $protected"
}

$custom = Join-Path $fixture 'previous custom path'
New-Item -ItemType Directory $custom | Out-Null
[IO.File]::WriteAllText("$custom\ANTHEA.exe", 'old executable fixture')
[IO.File]::WriteAllText("$custom\ANTHEA.runtimeconfig.json", '{}')
Check ((Run $setup $custom) -eq 0) 'A recognized previous custom installation remains supported'

$locked = Seed 'locked'
$stream = [IO.File]::Open("$locked\old\nested\obsolete.dll", [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
try {
    Check ((Run $setup $locked) -eq 12) 'Locked old file prevents installation'
    Check (-not (Test-Path "$locked\new-payload.txt")) 'No new payload is copied after incomplete cleanup'
} finally { $stream.Dispose() }

# A real process image named ANTHEA.exe, from an unrelated directory. Never
# start/close the user's application or kill a process not created by this test.
$probeSource = Join-Path $Output 'probe.nsi'
@'
Unicode true
RequestExecutionLevel user
Name "Installer process fixture"
SilentInstall silent
OutFile "${SETUP}"
Section
  Sleep 30000
SectionEnd
'@ | Set-Content -LiteralPath $probeSource -Encoding UTF8
$probeExe = Join-Path $Output 'ANTHEA.exe'
Compile $probeSource $probeExe
$running = Seed 'running'
$before = Get-FileHash "$running\user-added.txt"
if (Test-Path $trace) { Remove-Item -LiteralPath $trace }
$probe = Start-Process -FilePath $probeExe -WindowStyle Hidden -PassThru
try {
    Check (-not $probe.HasExited) 'Owned ANTHEA process fixture is running'
    Check ((Run $setup $running) -eq 10) 'ANTHEA running in another folder blocks silent installation'
    Check (-not (Test-Path $trace) -and -not (Test-Path "$running\new-payload.txt")) 'Running application blocks before uninstallation or copying'
    Check ((Get-FileHash "$running\user-added.txt").Hash -eq $before.Hash) 'Existing application files are untouched when ANTHEA is running'
} finally { if (-not $probe.HasExited) { $probe.Kill(); $probe.WaitForExit() }; $probe.Dispose() }
Check ((Run $setup $running) -eq 0) 'Installation succeeds after the owned application process closes'

$linked = Seed 'linked'
$external = Join-Path $fixture 'external'
New-Item -ItemType Directory $external | Out-Null
[IO.File]::WriteAllText("$external\keep.txt", 'outside link target')
$links = @("$linked\linked-data", (Join-Path $fixture 'parent-link'))
try {
    New-Item -ItemType Junction -Path $links[0] -Target $external | Out-Null
    New-Item -ItemType Junction -Path $links[1] -Target (Split-Path $linked) | Out-Null
    if (Test-Path $trace) { Remove-Item -LiteralPath $trace }
    Check ((Run $setup $linked) -eq 11) 'Junction inside the application folder blocks cleanup'
    Check ((Run $setup (Join-Path $links[1] 'ANTHEA')) -eq 11) 'Junction in an ancestor blocks cleanup'
    Check (-not (Test-Path $trace) -and (Test-Path "$linked\user-added.txt")) 'Links are rejected before touching the previous installation'
    Check ([IO.File]::ReadAllText("$external\keep.txt") -eq 'outside link target') 'Junction target outside the application is untouched'
} finally {
    foreach ($link in $links) {
        # Exact, verified link only; no recursive deletion and no target traversal.
        $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
        if ($item -and $item.LinkType -eq 'Junction' -and $item.FullName.StartsWith($fixture + '\', [StringComparison]::OrdinalIgnoreCase)) {
            & $env:ComSpec /d /c "rmdir `"$($item.FullName)`""
            if ($LASTEXITCODE -ne 0) { throw "Cannot remove fixture junction: $link" }
        }
    }
}
[IO.File]::WriteAllText((Join-Path $Output 'esito.txt'), "PASS $($checks.Count) installer checks`r`n" + ($checks -join "`r`n"))
@{ checks = $checks.Count; fixture = $fixture; completedUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content (Join-Path $Output 'result.json') -Encoding UTF8
