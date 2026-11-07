# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$payload=Join-Path $projectRoot 'build/payload'
if(-not(Test-Path -LiteralPath (Join-Path $payload 'OrbitPet.exe'))){throw 'Build first'}
$target=Join-Path $payload 'ChatDragTests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=(Get-ChildItem (Join-Path $projectRoot 'src/DesktopTasks/XianPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:ChatDragTests /out:$target /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'ChatDragTests.cs')
if($LASTEXITCODE -ne 0){throw 'Drag tests compile failed'}
$savedMode=$env:FUFU_TEST_MODE;$savedState=$env:FUFU_STATE_DIR
try{
    $env:FUFU_TEST_MODE='1';$env:FUFU_STATE_DIR=Join-Path ([IO.Path]::GetTempPath()) ('xianpal-chat-drag-'+[Guid]::NewGuid().ToString('N'))
    & $target
    if($LASTEXITCODE -ne 0){throw 'Drag tests failed'}
    Write-Output ('PASS isolated drag evidence: '+$env:FUFU_STATE_DIR)
}finally{$env:FUFU_TEST_MODE=$savedMode;$env:FUFU_STATE_DIR=$savedState;if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}}
