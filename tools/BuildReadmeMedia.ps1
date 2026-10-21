# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$tool = Join-Path $projectRoot 'build/BuildReadmeMedia.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks/XianPal') -Filter '*.cs' -File | ForEach-Object FullName)
& $compiler /nologo /target:exe /optimize+ /main:BuildReadmeMedia /out:$tool /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'BuildReminderPreviews.cs') (Join-Path $PSScriptRoot 'BuildReadmeMedia.cs')
if ($LASTEXITCODE -ne 0) { throw 'README media tool compilation failed' }
$previousEnvironment = @{}
foreach ($name in @('FUFU_TEST_MODE','FUFU_STATE_DIR','FUFU_CLAUDE_CONFIG_DIR','FUFU_INSTALL_ROOT')) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $demoRoot = Join-Path $projectRoot ('build/readme-demo/' + [Guid]::NewGuid().ToString('N'))
    $env:FUFU_TEST_MODE = '1'
    $env:FUFU_STATE_DIR = Join-Path $demoRoot 'state'
    $env:FUFU_CLAUDE_CONFIG_DIR = Join-Path $demoRoot 'claude'
    $env:FUFU_INSTALL_ROOT = Join-Path $demoRoot 'app'
    New-Item -ItemType Directory -Force -Path $env:FUFU_STATE_DIR | Out-Null
    & $tool (Join-Path $projectRoot 'build/payload/assets') (Join-Path $projectRoot 'docs/images') (Join-Path $projectRoot 'build/previews')
    if ($LASTEXITCODE -ne 0) { throw 'README media rendering failed' }
} finally {
    foreach ($name in $previousEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$previousEnvironment[$name],'Process') }
}
