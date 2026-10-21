param([string]$PetPath, [string]$ClaudeSettings)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $PetPath) { $PetPath = Join-Path $projectRoot 'build\payload\OrbitPet.exe' }
$PetPath = [IO.Path]::GetFullPath($PetPath)
if (-not (Test-Path -LiteralPath $PetPath -PathType Leaf)) { throw 'Build the application before running this test.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\DesktopTasks\XianPal') -Filter '*.cs').FullName
$testExe = Join-Path $projectRoot 'build\IntegrationTaskTests.exe'
& $compiler /nologo /target:exe /main:IntegrationTaskTests /out:$testExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'IntegrationTaskTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed to compile.' }
$previousEnvironment = @{}
foreach ($name in @('FUFU_TEST_MODE','FUFU_STATE_DIR','FUFU_PET_MANAGED_SESSION','FUFU_PET_INTERNAL_TASK_ID')) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:FUFU_TEST_MODE = '1'
    # Keep .NET Framework fixture paths below MAX_PATH even from long source folders.
    $env:FUFU_STATE_DIR = Join-Path ([IO.Path]::GetTempPath()) ('orbit-integration-task-test-' + [Guid]::NewGuid().ToString('N'))
    Remove-Item Env:FUFU_PET_MANAGED_SESSION,Env:FUFU_PET_INTERNAL_TASK_ID -ErrorAction SilentlyContinue
    if ($ClaudeSettings) { & $testExe $PetPath ([IO.Path]::GetFullPath($ClaudeSettings)) }
    else { & $testExe $PetPath }
    if ($LASTEXITCODE -ne 0) { throw 'Integration test failed; diagnostics retained in the isolated state folder.' }
    Write-Output "PASS integration tests; evidence: $env:FUFU_STATE_DIR"
} finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
