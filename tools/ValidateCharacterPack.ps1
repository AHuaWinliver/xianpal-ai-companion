param([Parameter(Mandatory=$true)][string]$PackDirectory)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$compiler=@((Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'),(Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe')) | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
if(-not $compiler){throw '.NET Framework compiler is required.'}
$validator=Join-Path ([IO.Path]::GetTempPath()) ('xianpal-pack-validator-'+[guid]::NewGuid().ToString('N')+'.exe')
try {
    & $compiler /nologo /target:exe /out:$validator /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'CharacterPackTool.cs') (Join-Path $projectRoot 'src/DesktopTasks/XianPal/CharacterPackCatalog.cs') (Join-Path $projectRoot 'src/DesktopTasks/XianPal/PetCharacter.cs')
    if($LASTEXITCODE -ne 0){throw 'Character pack validator failed to compile.'}
    & $validator ([IO.Path]::GetFullPath($PackDirectory))
    if($LASTEXITCODE -ne 0){throw 'Character pack validation failed.'}
} finally { if(Test-Path -LiteralPath $validator){Remove-Item -LiteralPath $validator -Force} }
