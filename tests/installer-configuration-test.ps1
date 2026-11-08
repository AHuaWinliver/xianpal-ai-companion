# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
$installer = Join-Path $projectRoot ('dist/XianPal-' + $version + '-Setup.exe')
$target = Join-Path $projectRoot 'build/InstallerConfigurationTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'InstallerConfigurationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer configuration test compilation failed.' }
$saved = @{}
foreach ($name in @('FUFU_TEST_MODE','FUFU_STATE_DIR','FUFU_INSTALL_ROOT','FUFU_CLAUDE_CONFIG_DIR')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $fixture = Join-Path ([IO.Path]::GetTempPath()) ('xianpal-installer-ui-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixture | Out-Null
    $env:FUFU_TEST_MODE = '1'; $env:FUFU_STATE_DIR = $fixture; $env:FUFU_INSTALL_ROOT = Join-Path $fixture 'app'; $env:FUFU_CLAUDE_CONFIG_DIR = Join-Path $fixture 'claude'
    & $target $installer
    if ($LASTEXITCODE -ne 0) { throw 'Installer configuration checks failed.' }
    $actual = Join-Path $fixture '已选择 chosen folder'
    $process = Start-Process -FilePath $installer -ArgumentList @('--silent', '--install-dir', ('"' + $actual + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $actual 'OrbitPet.exe'))) { throw 'The silent installer did not use --install-dir.' }
    if (Test-Path -LiteralPath $env:FUFU_INSTALL_ROOT) { throw 'Installer unexpectedly wrote into the previous destination.' }
    Write-Output ('PASS real custom-directory installation and preview evidence: ' + $fixture)
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
