# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks/XianPal') -Filter '*.cs' -File | ForEach-Object FullName)
$testExe = Join-Path $projectRoot 'build/ReminderPerformanceTests.exe'
& $compiler /nologo /target:exe /optimize+ /main:ReminderPerformanceTests /out:$testExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $projectRoot 'tools/BuildReminderPreviews.cs') (Join-Path $PSScriptRoot 'ReminderPerformanceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Reminder performance tests did not compile' }
& $testExe (Join-Path $projectRoot 'build/payload/assets')
if ($LASTEXITCODE -ne 0) { throw 'Reminder performance tests failed' }
