$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $repoRoot ('test-output\self-update-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$env:NEXUS_SELF_UPDATE_FIXTURE_ROOT = $fixtureRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixtureSource = @'
using System;using System.IO;using System.Threading;using System.Web.Script.Serialization;using System.Collections.Generic;using System.Reflection;
[assembly:AssemblyVersion("VERSION")]
class Fixture {static int Main(string[] args){if(Array.IndexOf(args,"--fixture-wait")>=0){Thread.Sleep(1500);return 0;}int i=Array.IndexOf(args,"--update-complete");if(i>=0){if(FAILURE)return 9;var d=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(args[i+1]));File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[i+1]),"started.txt"),(string)d["Token"]);}return 0;}}
'@
foreach ($case in @(@('Old','1.6.3.0','false'),@('New','1.6.4.0','false'),@('Fail','1.6.4.0','true'))) {
  $source = Join-Path $fixtureRoot ($case[0]+'.cs')
  [IO.File]::WriteAllText($source,$fixtureSource.Replace('VERSION',$case[1]).Replace('FAILURE',$case[2]))
  & $compiler /nologo /target:exe /platform:x64 /reference:System.Web.Extensions.dll ("/out:"+(Join-Path $fixtureRoot ($case[0]+'.exe'))) $source
  if($LASTEXITCODE -ne 0){throw 'Fixture compile failed'}
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'dist\Nexus\Nexus.exe') -Destination $fixtureRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'dist\Nexus\Nexus.exe.config') -Destination (Join-Path $fixtureRoot 'SelfUpdateIntegration.exe.config')
& $compiler /nologo /target:exe /platform:x64 ("/reference:"+(Join-Path $fixtureRoot 'Nexus.exe')) /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/out:"+(Join-Path $fixtureRoot 'SelfUpdateIntegration.exe')) (Join-Path $PSScriptRoot 'SelfUpdateIntegration.cs')
if($LASTEXITCODE -ne 0){throw 'Self update integration compile failed'}
try { & (Join-Path $fixtureRoot 'SelfUpdateIntegration.exe'); if($LASTEXITCODE -ne 0){throw 'Self update integration failed'} } finally { Remove-Item Env:NEXUS_SELF_UPDATE_FIXTURE_ROOT }
