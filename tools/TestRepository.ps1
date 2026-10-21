# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'PublicFiles.ps1')
$files=@(Get-PublicProjectFiles $projectRoot | Sort-Object FullName -Unique)
$violations=New-Object 'Collections.Generic.List[string]'
foreach($file in $files) {
    $relative=$file.FullName.Substring($projectRoot.Length+1).Replace('\','/')
    if($file.Length -gt 5MB){$violations.Add('oversized public file: '+$relative)}
    if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){$violations.Add('linked public file: '+$relative)}
    if($file.Extension -in @('.exe','.zip','.dll','.log','.pem','.key','.pfx') -or $relative -match '^assets/|^outputs/|^work/|^build/|backup'){$violations.Add('private/generated path: '+$relative)}
    if($file.Extension -in @('.cs','.ps1','.md','.yml','.json') -or $file.Name -in @('NOTICE','LICENSE')) {
        $content=[IO.File]::ReadAllText($file.FullName)
        if($content -match 'sk-ant-[A-Za-z0-9_-]{20,}|sk-proj-[A-Za-z0-9_-]{20,}|ghp_[A-Za-z0-9]{30,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'){$violations.Add('possible secret: '+$relative)}
        if($relative.StartsWith('src/') -and $content -match 'Kantam|shinchan|masao|nanako|kuroiso|蜡笔|野原|春日部|娜娜子|小爱小姐'){$violations.Add('legacy identity: '+$relative)}
        if($content -match '[A-Za-z]:[\\/]+Users[\\/]+(winli|mamba)[\\/]'){$violations.Add('personal absolute path: '+$relative)}
    }
}
if($violations.Count -gt 0){throw ($violations -join [Environment]::NewLine)}
$license=[IO.File]::ReadAllText((Join-Path $projectRoot 'LICENSE'))
if($license -notmatch 'Version 2.0, January 2004' -or $license -notmatch 'Grant of Patent License') {throw 'Apache-2.0 license incomplete'}
Write-Output ('PASS '+$files.Count+' public files: allowlist, size, legacy identity and credential-signature checks')
Write-Output 'This scan is not a legal/provenance guarantee; review publication manually.'
