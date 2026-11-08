# SPDX-License-Identifier: Apache-2.0
function Get-PublicProjectFiles([string]$ProjectRoot) {
    $names = @('README.md','README.en.md','README.zh-CN.md','VERSION','LICENSE','NOTICE','ASSET-LICENSE.md','CONTRIBUTING.md','CONTRIBUTING.zh-CN.md','SECURITY.md','SECURITY.zh-CN.md','CHANGELOG.md','CHANGELOG.zh-CN.md','.gitignore','.gitattributes','.editorconfig','build.ps1')
    $names += @('tools/BuildOriginalCharacters.cs','tools/BuildAppIcon.cs','tools/PublicFiles.ps1','tools/TestRepository.ps1','tools/ExportOpenSource.ps1')
    $names += @('tests/smoke-test.ps1','tests/integration-task-test.ps1','tests/IntegrationTaskTests.cs','tests/desktop-visibility-test.ps1','tests/DesktopVisibilityTests.cs','tests/icon-shortcut-test.ps1','tests/IconShortcutTests.cs','tests/claude-background-test.ps1','tests/ClaudeBackgroundTests.cs')
    $names += @('tests/original-assets-test.ps1','tests/companion-motion-test.ps1','tests/CompanionMotionTests.cs','tools/WalkingPreview.js')
    $names += @('tests/reminder-performance-test.ps1','tests/ReminderPerformanceTests.cs','tools/BuildReminderPreviews.cs','tools/BuildReminderPreviews.ps1')
    $names += @('tools/BuildReadmeMedia.cs','tools/BuildReadmeMedia.ps1','tests/release-source-test.ps1')
    $names += @('tests/startup-registration-test.ps1','tests/StartupRegistrationTests.cs')
    $names += @('tests/profile-settings-test.ps1','tests/ProfileSettingsTests.cs')
    $names += @('tests/installer-configuration-test.ps1','tests/InstallerConfigurationTests.cs')
    $names += @('tests/workshop-test.ps1','tests/WorkshopTests.cs')
    $names += @('tests/chat-bubble-test.ps1','tests/ChatBubbleTests.cs')
    $names += @('tests/chat-drag-test.ps1','tests/ChatDragTests.cs')
    $names += @('tests/hall-scene-install-test.ps1','tests/HallSceneTests.cs')
    $names += @('tests/hall-layout-test.ps1','tests/HallLayoutTests.cs')
    $names += @('tests/artifact-travel-test.ps1','tests/ArtifactTravelTests.cs')
    $names += @('tests/drag-gait-test.ps1','tests/DragGaitTests.cs')
    $names += @('tests/linux-package-test.py')
    $names += @('tools/CharacterPackTool.cs','tools/ValidateCharacterPack.ps1','tools/ExportCharacterPack.ps1','tests/character-pack-test.ps1','tests/CharacterPackTests.cs')
    foreach ($name in $names) {
        $file = Join-Path $ProjectRoot $name
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw ('Required public file missing: '+$name) }
        Get-Item -LiteralPath $file -Force
    }
    foreach ($folder in @('src/DesktopTasks','docs','.github','art')) {
        Get-ChildItem -LiteralPath (Join-Path $ProjectRoot $folder) -Recurse -File -Force |
            Where-Object { $_.FullName.Substring($ProjectRoot.Length + 1) -notmatch '(?:^|[\\/])(?:bin|obj)(?:[\\/]|$)' }
    }
}
