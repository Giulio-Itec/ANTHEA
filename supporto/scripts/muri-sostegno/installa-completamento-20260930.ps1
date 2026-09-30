$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$release = [IO.Path]::GetFullPath((Join-Path $repo 'supporto/artefatti/muri-completamento-20260930/rilascio'))
$destination = [IO.Path]::GetFullPath((Join-Path $repo 'app'))
if (-not $release.StartsWith($repo + [IO.Path]::DirectorySeparatorChar) -or -not $destination.StartsWith($repo + [IO.Path]::DirectorySeparatorChar)) { throw 'Percorso di rilascio non valido.' }
if (-not (Test-Path -LiteralPath (Join-Path $release 'ANTHEA.exe'))) { throw 'Rilascio non trovato.' }
$running = Get-Process -Name ANTHEA -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $destination 'ANTHEA.exe') }
if ($running) { throw 'Salvare il documento e chiudere ANTHEA prima di installare. Nessuna applicazione è stata chiusa.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $release | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force }
Write-Output 'Rilascio muri installato in app. I documenti utente non sono stati modificati.'
