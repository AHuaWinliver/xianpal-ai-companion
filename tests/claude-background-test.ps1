$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testExe = Join-Path $projectRoot 'build\payload\ClaudeBackgroundTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\DesktopTasks\XianPal') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /main:ClaudeBackgroundTests /out:$testExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'ClaudeBackgroundTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Claude Background tests compilation failed.' }
& $testExe
$testResult = $LASTEXITCODE
# Read-only mock CLI queries can finish just after the UI supervisor is
# disposed. Wait for these exact test binaries; never kill real Claude jobs.
foreach ($owned in @(Get-Process -Name ClaudeBackgroundTests -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $testExe })) {
    if (-not $owned.WaitForExit(20000)) { throw 'An owned mock CLI did not finish cleanup.' }
}
if ($testResult -ne 0) { throw 'Claude Background tests failed.' }
Remove-Item -LiteralPath $testExe
