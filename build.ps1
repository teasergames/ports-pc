param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'O compilador do .NET Framework do Windows não foi encontrado.' }
$destination = Join-Path $projectRoot 'dist\Nexus'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll','System.Drawing.dll','System.Windows.Forms.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output',('/out:' + (Join-Path $destination 'Nexus.exe')),('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')),('/win32icon:' + (Join-Path $projectRoot 'assets\nexus.ico')),('/resource:' + (Join-Path $projectRoot 'src\MainWindow.xaml') + ',Nexus.MainWindow.xaml'))
foreach ($reference in $references) { $compilerArgs += '/reference:' + (Join-Path $framework $reference) }
$compilerArgs += '/resource:' + (Join-Path $projectRoot 'assets\ports.json') + ',Nexus.ports.json'
$compilerArgs += '/resource:' + (Join-Path $projectRoot 'assets\releases-cache.json') + ',Nexus.releases-cache.json'
$compilerArgs += Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\Nexus.exe.config') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'CATALOG.md') -Destination $destination -Force
if ($Test) {
  $env:NEXUS_TEST_REPORT = Join-Path $destination 'test-results.txt'
  $testProcess = Start-Process -FilePath (Join-Path $destination 'Nexus.exe') -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
  Get-Content -LiteralPath $env:NEXUS_TEST_REPORT
  if ($testProcess.ExitCode -ne 0) { throw 'Falha nos testes.' }
  Remove-Item Env:NEXUS_TEST_REPORT
  Remove-Item -LiteralPath (Join-Path $destination 'test-results.txt')
}
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath (Join-Path $projectRoot 'dist\Nexus-Ports-PC-Windows-x64.zip') -Force
Write-Output ('Pronto: ' + (Join-Path $destination 'Nexus.exe'))
