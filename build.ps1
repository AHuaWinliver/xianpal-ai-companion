param([switch]$SkipInstaller)
# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$publicVersion = [IO.File]::ReadAllText((Join-Path $root 'VERSION')).Trim()
if ($publicVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain a three-part public version.' }
foreach ($versionSource in @('src/DesktopTasks/XianPal/Program.cs','src/DesktopTasks/XianPal.Installer/Program.cs')) {
    $versionText = [IO.File]::ReadAllText((Join-Path $root $versionSource))
    foreach ($attribute in @('AssemblyVersion','AssemblyFileVersion')) {
        $declared = [regex]::Match($versionText, ($attribute + '\("([0-9.]+)"\)')).Groups[1].Value
        if ($declared -ne ($publicVersion + '.0')) { throw ($attribute + ' disagrees with VERSION in ' + $versionSource) }
    }
}
$installerText = [IO.File]::ReadAllText((Join-Path $root 'src/DesktopTasks/XianPal.Installer/Program.cs'))
if ([regex]::Match($installerText, 'const string Version = "([0-9.]+)"').Groups[1].Value -ne $publicVersion) { throw 'Installer version disagrees with VERSION.' }
if ($env:OS -ne 'Windows_NT') { throw 'XianPal requires Windows (WinForms and System.Drawing).' }
$build = Join-Path $root 'build'
$payload = Join-Path $build 'payload'
$release = Join-Path $root 'dist'
$candidates = @((Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'), (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'))
$csc = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $csc) { throw 'Install .NET Framework 4.x before building. No NuGet restore is required.' }
New-Item -ItemType Directory -Force -Path $build,$release | Out-Null
# Only a known generated payload may be removed. Never operate on user assets.
if (Test-Path -LiteralPath $payload) {
    $resolved = [IO.Path]::GetFullPath($payload)
    if ($resolved -ne ([IO.Path]::GetFullPath($build).TrimEnd('\') + '\payload')) { throw 'Unsafe build output path.' }
    if ((Get-Item -LiteralPath $payload).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Build payload cannot be a junction/symlink.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
New-Item -ItemType Directory -Path $payload | Out-Null
$profileSource = Join-Path $root 'src\DesktopTasks\XianPal\PetCharacter.cs'
$generator = Join-Path $build 'BuildOriginalCharacters.exe'
& $csc /nologo /target:exe /optimize+ /out:$generator /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $root 'tools\BuildOriginalCharacters.cs') $profileSource (Join-Path $root 'src\DesktopTasks\XianPal\CompanionMotion.cs')
if ($LASTEXITCODE -ne 0) { throw 'Original character generator failed to compile.' }
& $generator (Join-Path $payload 'assets') (Join-Path $build 'previews') (Join-Path $root 'art\rigs')
if ($LASTEXITCODE -ne 0) { throw 'Original character generation failed.' }
$icon = Join-Path $build 'orbitpet.ico'
$iconBuilder = Join-Path $build 'BuildAppIcon.exe'
& $csc /nologo /target:exe /out:$iconBuilder /reference:System.dll /reference:System.Drawing.dll (Join-Path $root 'tools\BuildAppIcon.cs') (Join-Path $root 'src\DesktopTasks\XianPal\AppIcon.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon tool failed to compile.' }
& $iconBuilder (Join-Path $payload 'assets\idle\00.png') $icon (Join-Path $build 'icon-preview')
if ($LASTEXITCODE -ne 0) { throw 'Icon creation failed.' }
$program = Join-Path $root 'src\DesktopTasks\XianPal\Program.cs'
$version = [regex]::Match([IO.File]::ReadAllText($program),'AssemblyFileVersion\("([0-9.]+)"\)').Groups[1].Value
if (-not $version) { throw 'Version metadata missing.' }
$shortVersion = ([Version]$version).ToString(3)
Copy-Item -LiteralPath $icon -Destination (Join-Path $payload ('OrbitPet-' + $shortVersion + '.ico'))
$sources = (Get-ChildItem -LiteralPath (Join-Path $root 'src\DesktopTasks\XianPal') -Filter '*.cs' -File).FullName
$pet = Join-Path $payload 'OrbitPet.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:$icon /out:$pet /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
foreach ($notice in @('LICENSE','NOTICE','ASSET-LICENSE.md')) { Copy-Item -LiteralPath (Join-Path $root $notice) -Destination $payload }
& (Join-Path $root 'tools/BuildReminderPreviews.ps1')
& (Join-Path $root 'tools/BuildReadmeMedia.ps1')
if (-not $SkipInstaller) {
    $payloadZip = Join-Path $build 'XianPalPayload.zip'
    if (Test-Path -LiteralPath $payloadZip) { Remove-Item -LiteralPath $payloadZip }
    Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $payloadZip -CompressionLevel Optimal
    $installer = Join-Path $release ('XianPal-' + $shortVersion + '-Setup.exe')
    & $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:$icon /out:$installer /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$payloadZip,XianPalPayload.zip" (Join-Path $root 'src\DesktopTasks\XianPal.Installer\Program.cs') (Join-Path $root 'src\DesktopTasks\XianPal\StartupRegistration.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'),($hash + '  ' + [IO.Path]::GetFileName($installer) + [Environment]::NewLine),[Text.UTF8Encoding]::new($false))
    Write-Output ('Built installer: ' + $installer)
}
Write-Output ('Built XianPal ' + $version + ' using approved cultivation art and deterministic layered animation.')
