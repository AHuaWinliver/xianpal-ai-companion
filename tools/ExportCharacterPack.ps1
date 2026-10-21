param(
    [Parameter(Mandatory=$true)][string]$FramesDirectory,
    [Parameter(Mandatory=$true)][string]$ProfilePath,
    [Parameter(Mandatory=$true)][string]$OutputRoot
)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference='Stop'
$characterProfile=[IO.File]::ReadAllText([IO.Path]::GetFullPath($ProfilePath)) | ConvertFrom-Json
if($characterProfile.id -cnotmatch '^[a-z][a-z0-9-]{2,47}$'){throw 'Invalid character id.'}
$inputRoot=[IO.Path]::GetFullPath($FramesDirectory)
$outputParent=[IO.Path]::GetFullPath($OutputRoot)
if(-not(Test-Path -LiteralPath $inputRoot -PathType Container)){throw 'Rendered frames directory not found.'}
New-Item -ItemType Directory -Force -Path $outputParent | Out-Null
if((Get-Item -LiteralPath $outputParent).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked output directory is not allowed.'}
$destination=Join-Path $outputParent $characterProfile.id
if(Test-Path -LiteralPath $destination){throw 'Destination exists; export to a fresh directory and review before updating the installed pack.'}
$stagingParent=Join-Path $outputParent ('.xianpal-pack-'+[guid]::NewGuid().ToString('N'))
$staging=Join-Path $stagingParent $characterProfile.id
New-Item -ItemType Directory -Path $staging -Force | Out-Null
try {
    $hashes=[ordered]@{}
    foreach($state in @('idle','waiting','failed','running','running-left','running-right','waving','review','jumping','punching','eating','wiping-mouth')){
        $count=if($state -in @('idle','waiting','failed')){8}else{24}
        $folder=Join-Path $staging $state
        New-Item -ItemType Directory -Path $folder | Out-Null
        for($frame=0;$frame -lt $count;$frame++){
            $relative=$state+'/'+$frame.ToString('00')+'.png'
            $source=Join-Path $inputRoot $relative
            $target=Join-Path $staging $relative
            if((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked frame is not allowed.'}
            Copy-Item -LiteralPath $source -Destination $target
            $hashes[$relative]=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        }
    }
    $characterProfile | Add-Member -MemberType NoteProperty -Name files -Value $hashes -Force
    [IO.File]::WriteAllText((Join-Path $staging 'character.json'),($characterProfile | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    & (Join-Path $PSScriptRoot 'ValidateCharacterPack.ps1') -PackDirectory $staging
    [IO.Directory]::Move($staging,$destination) # Atomic publish; unlike Move-Item, refuses an existing destination.
    Remove-Item -LiteralPath $stagingParent
    Write-Output ('Exported data-only character pack: '+$destination)
} catch { throw ('Export failed. Unpublished draft retained at '+$stagingParent+': '+$_.Exception.Message) }
