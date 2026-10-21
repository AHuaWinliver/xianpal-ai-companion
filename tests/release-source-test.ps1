# SPDX-License-Identifier: Apache-2.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid public VERSION' }
foreach ($path in @('src/DesktopTasks/XianPal/Program.cs','src/DesktopTasks/XianPal.Installer/Program.cs')) {
    $text = [IO.File]::ReadAllText((Join-Path $projectRoot $path))
    foreach ($name in @('AssemblyVersion','AssemblyFileVersion')) {
        if ([regex]::Match($text, ($name + '\("([0-9.]+)"\)')).Groups[1].Value -ne ($version + '.0')) { throw ('Version mismatch: '+$path+' '+$name) }
    }
}
$installer = [IO.File]::ReadAllText((Join-Path $projectRoot 'src/DesktopTasks/XianPal.Installer/Program.cs'))
if ([regex]::Match($installer, 'const string Version = "([0-9.]+)"').Groups[1].Value -ne $version) { throw 'Installer constant disagrees with VERSION' }
$ignored = [IO.File]::ReadAllText((Join-Path $projectRoot '.gitignore'))
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/DesktopTasks') -Recurse -Filter '*.cs' -File) {
    $source = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($source, '(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)')) {
        if ($match.Groups[1].Value -notmatch '^XianPal(?:\.[A-Za-z_][A-Za-z0-9_]*)*$') { throw ('Unexpected application namespace: '+$file.Name) }
    }
}
foreach ($script in @('BuildOriginalCharacters.cs','BuildAppIcon.cs','PublicFiles.ps1','TestRepository.ps1','ExportOpenSource.ps1','WalkingPreview.js','BuildReminderPreviews.cs','BuildReminderPreviews.ps1','BuildReadmeMedia.cs','BuildReadmeMedia.ps1','CharacterPackTool.cs','ValidateCharacterPack.ps1','ExportCharacterPack.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot ('tools/'+$script)))) { throw ('Required build file absent: '+$script) }
    if ($ignored -match '(?m)^/tools/\*\s*$' -and -not $ignored.Contains('!/tools/'+$script)) { throw ('Required tool ignored by Git: '+$script) }
}
foreach ($name in @('README.md','README.en.md')) {
    $path = Join-Path $projectRoot $name
    $text = [IO.File]::ReadAllText($path)
    if (-not $text.Contains($version) -or $text -match '尚未重新打包发布|[A-Za-z]:[\\/]AI[\\/]') { throw ('Stale/local-only README content: '+$name) }
    foreach ($match in [regex]::Matches($text, '(?:\]\(|src=")([^\s\)"<>]+)')) {
        $target = $match.Groups[1].Value
        if ($target -match '^(https?://|#|mailto:)') { continue }
        $target = ($target -split '[#?]',2)[0]
        if ($target -and -not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $path) $target))) { throw ('Broken local README link: '+$name+' -> '+$target) }
    }
    if (-not $text.Contains('github.com/AHuaWinliver/xianpal-ai-companion')) { throw ('Repository link missing: '+$name) }
}
foreach ($name in @('docs/CHARACTER-CUSTOMIZATION.md','docs/CHARACTER-PACKS.md','docs/templates/CHARACTER-SPEC.md','docs/DEVELOPMENT.md','docs/REMINDER-PERFORMANCES.md','CONTRIBUTING.md')) {
    $path = Join-Path $projectRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Missing character contributor documentation: '+$name) }
    $text = [IO.File]::ReadAllText($path)
    foreach ($match in [regex]::Matches($text, '(?:\]\(|src=")([^\s\)"<>]+)')) {
        $target = $match.Groups[1].Value
        if ($target -match '^(https?://|#|mailto:)') { continue }
        $target = ($target -split '[#?]',2)[0]
        if ($target -and -not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $path) $target))) { throw ('Broken contributor documentation link: '+$name+' -> '+$target) }
    }
}
foreach ($name in @('hero.png','characters.png','workbench.png','walking.gif','sword-flight.gif','social-preview.png','version.svg','windows.svg','license.svg')) {
    $file = Get-Item -LiteralPath (Join-Path $projectRoot ('docs/images/'+$name))
    if ($file.Length -le 0 -or $file.Length -gt 5MB) { throw ('README media empty or too large: '+$name) }
}
Add-Type -AssemblyName System.Drawing
foreach ($name in @('walking.gif','sword-flight.gif')) {
    $gif = [Drawing.Image]::FromFile((Join-Path $projectRoot ('docs/images/'+$name)))
    try { if ($gif.GetFrameCount([Drawing.Imaging.FrameDimension]::Time) -lt 24) { throw ('Missing animation frames: '+$name) } }
    finally { $gif.Dispose() }
}
Write-Output ('PASS public '+$version+': versions, required Git files, bilingual and character-documentation links, README media')
