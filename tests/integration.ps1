param([ValidateSet('local','online','catalog','catalog-install','catalog-packages','platform-packages','xbox-packages','oct28-packages','site-packages','download-fix')][string]$Mode = 'online')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$testOutput = Join-Path $repoRoot 'test-output'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
Copy-Item (Join-Path $repoRoot 'dist/Nexus/Nexus.exe') $testOutput -Force
Copy-Item (Join-Path $repoRoot 'dist/Nexus/Nexus.exe.config') $testOutput -Force
Copy-Item (Join-Path $repoRoot 'dist/Nexus/Nexus.exe.config') (Join-Path $testOutput 'Integration.exe.config') -Force
& (Join-Path $framework 'csc.exe') /nologo /target:exe ("/out:" + (Join-Path $testOutput 'Integration.exe')) ("/reference:" + (Join-Path $testOutput 'Nexus.exe')) ("/reference:" + (Join-Path $framework 'System.Net.Http.dll')) (Join-Path $PSScriptRoot 'Integration.cs')
if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed' }
$packageFolder = switch ($Mode) { 'download-fix' {'work'} 'site-packages' {'work/site-packages'} 'oct28-packages' {'work/oct28-packages'} 'xbox-packages' {'work/xbox-packages'} default {'work/platform-packages'} }
& (Join-Path $testOutput 'Integration.exe') (Join-Path $testOutput "data-$Mode") $Mode (Join-Path (Split-Path $repoRoot -Parent | Split-Path -Parent) $packageFolder)
if ($LASTEXITCODE -ne 0) { throw 'Integration test failed' }
