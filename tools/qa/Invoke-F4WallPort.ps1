[CmdletBinding()]
param([Parameter(Mandatory)][string]$Output, [Parameter(Mandatory)][string]$CandidateConcrete)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$candidate = [IO.Path]::GetFullPath($CandidateConcrete)
if (!(Test-Path -LiteralPath $candidate -PathType Leaf)) { throw 'Candidate Concrete assembly missing' }
$bin = Join-Path $Output 'candidate-test-bin'
if (Test-Path -LiteralPath $bin) { throw 'Candidate test output already exists; use a fresh output directory' }
New-Item -ItemType Directory -Path $bin -Force | Out-Null
# Diagnostic copy only. No changes to lib/Checker or to any production build output.
Copy-Item -Path (Join-Path $root 'tests/ConcreteLibraryAdapter.Checks/bin/Release/net8.0/*') -Destination $bin -Recurse
Copy-Item -LiteralPath $candidate -Destination (Join-Path $bin 'GPCChecker.Concrete.dll') -Force
$hasher = [Security.Cryptography.SHA256]::Create()
$stream = [IO.File]::OpenRead($candidate)
try { $hash = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '') }
finally { $stream.Dispose(); $hasher.Dispose() }
@{ Path = $candidate; SHA256 = $hash } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'candidate-sha256.json') -Encoding UTF8
& dotnet (Join-Path $bin 'ConcreteLibraryAdapter.Checks.dll') --f4-wall-port $root (Join-Path $root 'supporto/artefatti/refactoring/f4/before-port-1.7.1') $Output
if ($LASTEXITCODE) { throw "Wall candidate comparison failed: $LASTEXITCODE" }
