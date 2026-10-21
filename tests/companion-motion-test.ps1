# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testExe=Join-Path $projectRoot 'build/CompanionMotionTests.exe'
& $compiler /nologo /target:exe /out:$testExe /reference:System.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'CompanionMotionTests.cs') (Join-Path $projectRoot 'src/DesktopTasks/XianPal/CompanionMotion.cs')
if($LASTEXITCODE -ne 0){throw 'Motion test compile failed'}
& $testExe
if($LASTEXITCODE -ne 0){throw 'Motion test failed'}
