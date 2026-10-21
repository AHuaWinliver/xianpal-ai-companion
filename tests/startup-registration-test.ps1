# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = @(
    (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw '.NET Framework compiler is required.' }
$temporaryParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$testRoot = Join-Path $temporaryParent ('xianpal-startup-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $pet = Join-Path $testRoot 'OrbitPet.exe'
    $installer = Join-Path $testRoot 'Setup.exe'
    $test = Join-Path $testRoot 'StartupRegistrationTests.exe'
    $shared = Join-Path $projectRoot 'src/DesktopTasks/XianPal/StartupRegistration.cs'
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks/XianPal') -Filter '*.cs' -File | ForEach-Object FullName)
    & $compiler /nologo /target:winexe /out:$pet /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
    # Compile and inspect only: do not install, run UI or modify real startup entries.
    & $compiler /nologo /target:winexe /out:$installer /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $projectRoot 'src/DesktopTasks/XianPal.Installer/Program.cs') $shared
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    & $compiler /nologo /target:exe /out:$test /reference:System.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'StartupRegistrationTests.cs') $shared
    if ($LASTEXITCODE -ne 0) { throw 'Startup test compilation failed.' }
    $version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
    & $test $pet $installer $version
    if ($LASTEXITCODE -ne 0) { throw 'Startup registration tests failed.' }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $temporaryParent -or [IO.Path]::GetFileName($resolved) -notmatch '^xianpal-startup-test-[a-f0-9]{32}$') { throw 'Unsafe temporary cleanup path.' }
    if ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked temporary directory cannot be cleaned.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
