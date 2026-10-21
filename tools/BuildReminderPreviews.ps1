# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks/XianPal') -Filter '*.cs' -File | ForEach-Object FullName)
$tool = Join-Path $projectRoot 'build/BuildReminderPreviews.exe'
& $compiler /nologo /target:exe /optimize+ /main:BuildReminderPreviews /out:$tool /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'BuildReminderPreviews.cs')
if ($LASTEXITCODE -ne 0) { throw 'Reminder preview compiler failed' }
& $tool (Join-Path $projectRoot 'build/payload/assets') (Join-Path $projectRoot 'build/previews')
if ($LASTEXITCODE -ne 0) { throw 'Reminder preview renderer failed' }
