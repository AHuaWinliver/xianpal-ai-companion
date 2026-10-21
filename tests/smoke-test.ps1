# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$app=Join-Path $projectRoot 'build/payload/OrbitPet.exe'
if(-not(Test-Path -LiteralPath $app)){throw 'Run build.ps1 first'}
$savedTestMode=$env:FUFU_TEST_MODE
$savedState=$env:FUFU_STATE_DIR
try {
    $env:FUFU_TEST_MODE='1'
    $env:FUFU_STATE_DIR=Join-Path $projectRoot ('build/smoke/'+[Guid]::NewGuid().ToString('N'))
    foreach($mode in @('--self-test','--encoding-self-test','--claude-task-self-test')) {
        $check=Start-Process -FilePath $app -ArgumentList $mode -WindowStyle Hidden -PassThru -Wait
        if($check.ExitCode -ne 0){throw ($mode+' failed: '+$check.ExitCode)}
        Write-Output ('PASS '+$mode)
    }
}finally{$env:FUFU_TEST_MODE=$savedTestMode;$env:FUFU_STATE_DIR=$savedState}
