[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Model,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$modelPath = (Resolve-Path -LiteralPath $Model).Path
if ([IO.Path]::GetExtension($modelPath) -ne '.antheamodel') { throw 'Serve un pacchetto .antheamodel.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot ('supporto\artefatti\model-viewer\preview-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $destination) -and (Get-ChildItem -LiteralPath $destination -Force | Select-Object -First 1)) {
    throw 'La cartella di destinazione deve essere nuova o vuota; nessun pacchetto esistente viene sostituito.'
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$applicationDirectory = Join-Path $destination 'app'
& dotnet publish (Join-Path $repositoryRoot 'tests\ModelViewer.UiChecks\ModelViewer.UiChecks.csproj') -c Release --no-restore -o $applicationDirectory -p:UseAppHost=true -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Pubblicazione della preview non riuscita.' }
$forbidden = @(Get-ChildItem -LiteralPath $applicationDirectory -File -Recurse | Where-Object { $_.Name -match '^(gmsh|GMsh\.Net|UnsafeEx|GMesh)' })
if ($forbidden.Count -gt 0) { throw 'Il pacchetto contiene dipendenze di mesh ammesse soltanto negli unit test.' }
$dataDirectory = Join-Path $destination 'modello'
New-Item -ItemType Directory -Path $dataDirectory | Out-Null
Copy-Item -LiteralPath $modelPath -Destination (Join-Path $dataDirectory 'spalla.antheamodel')
$launcher = @'
[CmdletBinding()]
param(
    [ValidateSet('Contouring','Deformata','Aste')][string]$Vista = 'Contouring',
    [ValidateSet('Chiaro','Scuro','MoltoScuro')][string]$Tema = 'MoltoScuro'
)
$ErrorActionPreference = 'Stop'
try {
    $executable = Join-Path $PSScriptRoot 'app\ModelViewer.UiChecks.exe'
    $snapshot = Join-Path $PSScriptRoot 'modello\spalla.antheamodel'
    if (-not (Test-Path -LiteralPath $executable) -or -not (Test-Path -LiteralPath $snapshot)) { throw 'Pacchetto incompleto: conserva app e modello insieme agli avviatori.' }
    $session = Join-Path $PSScriptRoot ('prove\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
    New-Item -ItemType Directory -Path $session -Force | Out-Null
    $appearance = @{Chiaro='Light';Scuro='Dark';MoltoScuro='VeryDark'}[$Tema]
    $preferences = Join-Path $PSScriptRoot 'preferenze-viste'
    $arguments = @('"'+$session+'"','"'+$snapshot+'"','--viewer-only','--direct-model','--interactive','--appearance',$appearance,'--averaged','--extruded','--preferences','"'+$preferences+'"')
    if ($Vista -eq 'Deformata') { $arguments += '--deformed' }
    if ($Vista -eq 'Aste') { $arguments += '--beam-results' }
    Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $session 'avvio.log') -RedirectStandardError (Join-Path $session 'errore.log')
} catch {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($_.Exception.Message,'ANTHEA - Modelli') | Out-Null
    exit 1
}
'@
[IO.File]::WriteAllText((Join-Path $destination 'Avvia-Modelli.ps1'), $launcher, [Text.UTF8Encoding]::new($true))
foreach ($view in @('Contouring','Deformata','Aste')) {
    $command = '@echo off' + "`r`n" + 'powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Avvia-Modelli.ps1" -Vista ' + $view + "`r`n"
    [IO.File]::WriteAllText((Join-Path $destination ('Apri-' + $view + '.cmd')), $command, [Text.Encoding]::ASCII)
}
$commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$dirty = [bool](& git -C $repositoryRoot status --porcelain)
$files = @(Get-ChildItem -LiteralPath $destination -File -Recurse | ForEach-Object {
    [ordered]@{Path=$_.FullName.Substring($destination.Length+1);Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
[ordered]@{CreatedUtc=[DateTime]::UtcNow.ToString('o');Commit=$commit;UncommittedChanges=$dirty;Runtime='.NET Desktop Runtime 8 x64';Offline=$true;Files=$files} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'manifest.json') -Encoding UTF8
Write-Output $destination
