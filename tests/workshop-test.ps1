# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'build') | Out-Null
$target = Join-Path $projectRoot 'build/WorkshopTests.exe'
$sources = (Get-ChildItem (Join-Path $projectRoot 'src/DesktopTasks/XianPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:WorkshopTests /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'WorkshopTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Workshop tests compile failed' }
$savedMode = $env:FUFU_TEST_MODE
$savedDirectory = $env:FUFU_STATE_DIR
try {
    $env:FUFU_TEST_MODE = '1'
    $env:FUFU_STATE_DIR = Join-Path ([IO.Path]::GetTempPath()) ('xianpal-workshop-' + [Guid]::NewGuid().ToString('N'))
    & $target
    if ($LASTEXITCODE -ne 0) { throw 'Workshop tests failed' }
    Write-Output ('Evidence retained in isolated directory: ' + $env:FUFU_STATE_DIR)
} finally { $env:FUFU_TEST_MODE = $savedMode; $env:FUFU_STATE_DIR = $savedDirectory }
