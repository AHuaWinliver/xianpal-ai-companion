# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$payload=Join-Path $projectRoot 'build/payload'
if(-not(Test-Path -LiteralPath (Join-Path $payload 'assets/idle/00.png'))){throw 'Run build.ps1 before testing live character packs.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testExe=Join-Path $payload 'CharacterPackTests.exe'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('xianpal-character-pack-test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$sources=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks/XianPal') -Filter '*.cs' -File | ForEach-Object FullName)
try {
    & $compiler /nologo /target:exe /main:CharacterPackTests /out:$testExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'CharacterPackTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Character pack tests failed to compile.'}
    & $testExe $fixture
    if($LASTEXITCODE -ne 0){throw ('Character pack tests failed; isolated diagnostics: '+$fixture)}
    & (Join-Path $projectRoot 'tools/ExportCharacterPack.ps1') -FramesDirectory (Join-Path $fixture 'export-frames/export-demo') -ProfilePath (Join-Path $projectRoot 'docs/templates/character-profile.json') -OutputRoot (Join-Path $fixture 'exported')
    & (Join-Path $projectRoot 'tools/ValidateCharacterPack.ps1') -PackDirectory (Join-Path $fixture 'exported/my-companion')
    Write-Output ('PASS pack export and standalone validation; isolated evidence: '+$fixture)
} finally { if(Test-Path -LiteralPath $testExe){Remove-Item -LiteralPath $testExe} }
