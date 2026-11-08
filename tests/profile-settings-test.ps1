# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $projectRoot 'build/ProfileSettingsTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks/XianPal') -Filter '*.cs' -File | ForEach-Object FullName)
& $compiler /nologo /target:exe /main:ProfileSettingsTests /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'ProfileSettingsTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Profile settings tests failed to compile.' }
$saved = @{}
foreach ($name in @('FUFU_TEST_MODE','FUFU_STATE_DIR','XIANPAL_TEST_ASSETS')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:FUFU_TEST_MODE = '1'
    $env:FUFU_STATE_DIR = Join-Path ([IO.Path]::GetTempPath()) ('xianpal-profile-test-' + [Guid]::NewGuid().ToString('N'))
    & $target (Join-Path $projectRoot 'build/payload/assets')
    if ($LASTEXITCODE -ne 0) { throw 'Profile settings tests failed.' }
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
