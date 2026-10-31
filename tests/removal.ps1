$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $repoRoot ('test-output\removal-links-' + [guid]::NewGuid().ToString('N'))
$portFolder = Join-Path $fixtureRoot 'dedicated-port'
$outside = Join-Path $fixtureRoot 'outside-copy'
New-Item -ItemType Directory -Path $portFolder,$outside -Force | Out-Null
Set-Content -LiteralPath (Join-Path $portFolder 'Game.exe') -Value 'owned fixture'
[IO.File]::WriteAllText((Join-Path $outside 'personal.dat'),'preserve')
New-Item -ItemType Junction -Path (Join-Path $portFolder 'linked-data') -Target $outside | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'dist\Nexus\Nexus.exe') -Destination $fixtureRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 ("/reference:"+(Join-Path $fixtureRoot 'Nexus.exe')) ("/out:"+(Join-Path $fixtureRoot 'RemovalLinks.exe')) (Join-Path $PSScriptRoot 'RemovalLinks.cs')
if($LASTEXITCODE -ne 0){throw 'Removal integration compile failed'}
& (Join-Path $fixtureRoot 'RemovalLinks.exe') $fixtureRoot
if($LASTEXITCODE -ne 0){throw 'Removal link safety failed'}
