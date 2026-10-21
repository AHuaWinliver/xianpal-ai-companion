$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testExe = Join-Path $projectRoot 'build\payload\DesktopVisibilityTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\DesktopTasks\XianPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:DesktopVisibilityTests /out:$testExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'DesktopVisibilityTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Desktop tests compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Desktop tests failed.' }
# This exact test-only output must not be included in a production installer.
Remove-Item -LiteralPath $testExe
