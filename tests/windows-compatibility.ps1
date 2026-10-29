# Run on the actual target Windows 10/11 machine; Windows Server CI is not a
# substitute for this compatibility check. No third-party games are launched.
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Execute este teste em Windows 10/11 x64.' }
if (![Environment]::Is64BitOperatingSystem) { throw 'NEXUS exige Windows x64.' }
$release = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release -ErrorAction Stop).Release
if ($release -lt 528040) { throw 'Instale o .NET Framework 4.8 antes de continuar.' }
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & ./build.ps1 -Test
    $output = Join-Path $root 'test-output/windows-compatibility'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $data = Join-Path $output ('data-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $data | Out-Null
    foreach ($mode in @('library', '--catalog-smoke', '--updates-smoke', '--package-smoke', '--content-smoke')) {
        $png = Join-Path $output (($mode -replace '^--','') + '.png')
        if (Test-Path $png) { Remove-Item -LiteralPath $png }
        $arguments = @('--data-dir', ('"' + $data + '"'), '--smoke-test', ('"' + $png + '"'))
        if ($mode -ne 'library') { $arguments += $mode }
        $process = Start-Process ./dist/Nexus/Nexus.exe -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -ne 0 -or !(Test-Path $png) -or (Get-Item $png).Length -eq 0) { throw "Falha no smoke test: $mode" }
    }
    $os = Get-CimInstance Win32_OperatingSystem
    @("OS: $($os.Caption)", "Version: $($os.Version)", "Build: $($os.BuildNumber)", "Architecture: $($os.OSArchitecture)", ".NET Framework Release: $release", 'PASS build, self-tests and five rendering smoke tests') | Set-Content (Join-Path $output 'results.txt') -Encoding UTF8
    Get-Content (Join-Path $output 'results.txt')
} finally { Pop-Location }
