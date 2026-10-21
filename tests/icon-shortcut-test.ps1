param([string]$TestRoot)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $projectRoot 'build\IconShortcutTests.exe'
$version=([Version][regex]::Match([IO.File]::ReadAllText((Join-Path $projectRoot 'src/DesktopTasks/XianPal/Program.cs')),'AssemblyFileVersion\("([0-9.]+)"\)').Groups[1].Value).ToString(3)
$installer = Join-Path $projectRoot ('dist\XianPal-'+$version+'-Setup.exe')
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Build the installer before running this test.' }
& $compiler /nologo /target:exe /out:$testExe /reference:System.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'IconShortcutTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon / shortcut tests failed to compile.' }
if (-not $TestRoot) {
    # Installer staging adds another directory level. Keep isolated checks out
    # of deeply nested source trees to respect .NET Framework path limits.
    $TestRoot = Join-Path ([IO.Path]::GetTempPath()) ('xianpal-desktop-package-test-' + [Guid]::NewGuid().ToString('N'))
}
$TestRoot = [IO.Path]::GetFullPath($TestRoot)
if ($TestRoot.IndexOf('desktop-package-test',[StringComparison]::Ordinal) -lt 0) { throw 'The isolated TestRoot must include desktop-package-test in its path.' }
New-Item -ItemType Directory -Force -Path $TestRoot | Out-Null
$previousEnvironment = @{}
foreach ($name in @('FUFU_TEST_MODE','FUFU_INSTALL_ROOT','FUFU_CLAUDE_CONFIG_DIR','FUFU_STATE_DIR')) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:FUFU_TEST_MODE = '1'
    $env:FUFU_INSTALL_ROOT = Join-Path $TestRoot ('app-'+$version)
    $env:FUFU_CLAUDE_CONFIG_DIR = Join-Path $TestRoot 'claude'
    $env:FUFU_STATE_DIR = Join-Path $TestRoot 'state'
    Write-Output ('Isolated installer test root: '+$TestRoot)
    foreach ($pass in @(1, 2)) {
        $process = Start-Process -FilePath $installer -ArgumentList '--silent' -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0) { throw "Isolated installation $pass failed: $($process.ExitCode)" }
        Write-Output "PASS isolated installation $pass"
    }
    $pet = Join-Path $env:FUFU_INSTALL_ROOT 'OrbitPet.exe'
    if ((Get-FileHash -LiteralPath $pet).Hash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'build\payload\OrbitPet.exe')).Hash) { throw 'Installed application does not match payload' }
    & $testExe $pet $installer
    if ($LASTEXITCODE -ne 0) { throw 'Icon / shortcut test failed.' }
} finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
