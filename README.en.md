<div align="center">

<img src="docs/images/hero.png" alt="XianPal: four original cultivation-inspired AI desktop companions" width="100%">

# XianPal · AI Desktop Companion

Four original chibi companions for Windows, powered by your local Claude Code CLI

[![Version 0.6.11 · Preview](docs/images/version.svg)](CHANGELOG.md)
![Windows 10 / 11](docs/images/windows.svg)
[![Apache-2.0](docs/images/license.svg)](LICENSE)

[简体中文](README.md) · [What's new](#whats-new) · [Get started](#get-started) · [Create a companion](#custom-characters) · [Build from source](#build-from-source)

</div>

XianPal combines a lively Windows desktop companion with a practical Claude Code workflow. Chat beside your pet, hand it a file, schedule recurring work, and let a character-specific performance remind you when it is time to pause.

The desktop app runs locally. **AI features require a separately installed, configured and authorized Claude Code CLI.** No model or API key is included; local execution does not mean offline inference.

**0.6 is an open preview, built to evolve with contributors.** The current release is **0.6.11**, not a 1.0 stable release. Features, interfaces and asset formats may change. Issues, original characters and code PRs are welcome; back up configuration and try tasks in a test directory before using important data.

<a id="whats-new"></a>
## What's new in 0.6.11

- The workshop hall uses the same full-resolution animation frames as the desktop companions. Each character scales to its workstation with its original proportions.

### Features added in 0.6.10

- **Default settings (默认设置)** groups startup, window behavior, size, trails, roaming, proactive care and Claude defaults.
- **Docked opacity (贴边不透明度)** ranges from 20% to 100%. The companion returns to full opacity when undocked.
- **Claude settings files** can be set globally and per companion. Open **Default settings → Claude settings file → By companion (默认设置 → Claude 配置文件 → 按角色设置)** to select a JSON file. Companions without an override use the global file. When both are unset, Claude uses its own settings. Team members use their own companion profiles, and scheduled tasks use the companion active at launch. [Claude CLI settings flag](https://code.claude.com/docs/en/cli-reference)
- **Scheduled tasks** run in the background by default. Open **Claude Code scheduled tasks → Run history (Claude Code 定时任务 → 运行情况)** and double-click a run to follow its output. Choose a terminal mode when the task needs terminal interaction.

The companion's steps follow the cursor speed while dragging. **Default settings → Reduce motion (默认设置 → 减少动画)** turns off the drag animation.

[Full changelog in Chinese](CHANGELOG.md)

## Events and multi-agent teams

**Responsive hall (0.6.8):** the scene and workstations resize and reflow with the window. Characters and desks keep their proportions. Drag the divider above the details pane to adjust its share; resizing preserves that ratio and the selected reply. Vertical scrolling is used only when the members cannot fit at a readable size, rather than enforcing a permanent 680px scene.

**Artifact travel (0.6.7):** long routes automatically use a flying sword, talisman scroll, leaf boat or ink-jade disk, depending on the companion. Short routes keep walking. Mounting, acceleration and landing ease smoothly. The threshold is 40% of the current monitor working-area diagonal, with a 480px minimum, measured over the entire queued route. Both local and AI routes reuse this logic without additional model calls. Reduced motion still disables autonomous travel.

![Four companion-specific artifacts in both directions, drawn by the production renderer](docs/images/artifact-travel.png)

0.6.5 fixes the missing hall background after installation: the installer now deploys and validates the scene directory transactionally, while the executable embeds a fallback copy. Missing or corrupt external artwork falls back to the embedded scene. Exit the old app before upgrading; no manual image copying is required.

0.6.2 replaces the activity transcript with left/right chat bubbles, character avatars, selectable Unicode text and throttled streaming updates. User and assistant turns are recorded explicitly rather than inferred from transcript text. The session list is collapsed by default; use the header session button to switch conversations.

Drag the chat header, companion name or status label to move the conversation and visible companion together. Text selection and buttons do not start window dragging. The group is constrained to the target monitor's working area, with release-time reflow when moving to a smaller display.

In 0.6.6, double-clicking the reply preview opens the conversation centered directly below the companion. The window is shortened when necessary; if still needed the companion moves upward/inward instead of automatically flipping the dialog above it. Manual first-show placement avoids Windows selecting another initial location.

Version 0.6.1 adds editable role presets for Qingheng (planning/synthesis), Feijian (implementation), Cangzhi (independent review), and Xiaomo (verification). Create a research-first or development workflow with shared goals and acceptance criteria. Optional native Claude subagent definition templates are included but are not installed or enabled automatically. See [role boundaries and integration notes](docs/COMPANION-ROLES.md).

Right-click the companion to open the event and team workshop. Define members, roles, directories and task dependencies. Separate Claude CLI sessions exchange bounded upstream summaries. The hall displays execution states and recorded handoffs. Click a member to inspect visible output. Up to two independent directories can run concurrently. Overlapping directories run in sequence. Events notify you before execution. Claude CLI permissions apply and model usage may incur charges.

[Workshop guide in Chinese](docs/WORKSHOP.md)

![Four full-resolution companions at their workstations in a local workshop demonstration](docs/images/sect-hall.png)

## Four companions, four personalities

<img src="docs/images/characters.png" alt="Actual in-app artwork for Qingheng, Feijian, Cangzhi and Xiaomo" width="100%">

| Companion | Personality | Reminder performance |
| --- | --- | --- |
| Qingheng / 青衡 | A dependable, thoughtful cultivator | Summons a flying sword, takes an aerial circuit and lands with a reminder |
| Feijian / 绯笺 | A cheerful, meticulous talisman artisan | Dancing talismans, a small leap and rose-colored light |
| Cangzhi / 苍芷 | A quiet, considerate herbalist | Unfurling vines, drifting leaves and gentle firefly light |
| Xiaomo / 小墨 | A steady charcoal turtle with golden cloud patterns | A little hop followed by expanding ink-and-gold ripples |

<div align="center"><img src="docs/images/walking.gif" alt="Actual 24-phase walking cycles, including opposing arm/leg motion and quadruped turtle locomotion" width="720"></div>

The supplied transparent artwork is animated locally: 24-phase gait, articulated elbows, subtle neck motion and independently timed expressions. No image-generation service is called during build or installation.

## A companion that can help you work

<img src="docs/images/workbench.png" alt="Real UI controls rendered with isolated, preset demonstration data; not a live model execution" width="100%">

*UI examples use real controls and synthetic demonstration data, not private conversations or a live model run.*

- **Chat:** latest-reply bubbles, a persistent activity window and follow-up replies.
- **Files:** drag a file or folder onto the pet, then confirm the request and working directory. The eating animation **does not delete the original file**.
- **Tasks:** daily, weekly and interval schedules; explicit directories; block or parallel execution; run history and past conversations.
- **CLI modes:** background execution, visible PowerShell / Windows Terminal, native CLI interaction, and native Background on compatible CLI versions.
- **Desktop life:** hover interactions, drag-to-follow movement, roaming, edge docking, translucent light trails, desktop-1 / all-desktop display and tray hide/restore.
- **Character hot updates:** add or replace data-only character packs while the app runs, without reinstalling.
- **Reminders:** countdowns and daily reminders, character performances or a quiet popup, reduced motion, Esc dismissal and snooze.

<div align="center"><img src="docs/images/sword-flight.gif" alt="Qingheng rides a sword along a curved flight path, with a tapered translucent wake" width="640"></div>

## Get started

1. Download `XianPal-0.6.11-Setup.exe` and its SHA256 checksum. [GitHub Releases](https://github.com/AHuaWinliver/xianpal-ai-companion/releases)
2. Install on Windows 10 / 11. The installer adds a Start menu shortcut and the current user's integration hooks.
3. Right-click the pet to choose a character, configure reminders or open scheduled tasks. Set the Claude Code working directory and settings file under **Default settings**.
4. Before using AI features, make sure Claude Code works normally in your terminal with your own account/service configuration.

The release link becomes available after the maintainer publishes the release. Until then, build from source. The installer is unsigned; Windows may display an unknown-publisher warning.

| Action | Result |
| --- | --- |
| Right-click the companion | Open the menu for characters, settings, reminders and tasks |
| Drag the companion | Move it at your cursor's pace and dock it near a screen edge |
| Double-click a reply bubble | Open the conversation beneath the companion and reply |
| Drop a file or folder onto the companion | Prepare a file request and confirm its working directory |
| Right-click the tray icon | Show or hide the companion, restore the window or exit |

Open **Event planning / Team workshop (事件规划 / 门派协作…)** from the companion's menu to set up a team workflow.

## Build from source

Requirements: Windows 10 / 11, PowerShell and the system .NET Framework 4.x compiler. No Node.js, NuGet restore, image-generation account or build-time API key is required.

You can also download and extract the repository ZIP. Git is only needed for the clone step below, not for compilation.
Prefer a short checkout path such as `C:\src\xianpal`; deeply nested directories can hit .NET Framework's traditional path-length limit.

```powershell
git clone https://github.com/AHuaWinliver/xianpal-ai-companion.git
cd xianpal-ai-companion
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\build\payload\OrbitPet.exe
```

- Installer: `dist/XianPal-0.6.11-Setup.exe`
- App: `build/payload/OrbitPet.exe`
- Offline animation preview: `build/previews/index.html`
- Performance preview: `build/previews/performances.html`
- README media: `docs/images/`

Use `-SkipInstaller` for a development build. The application namespace is `XianPal`; the installer namespace is `XianPal.Installer`. The current public version is **0.6.11 (Preview)**.

The Windows startup entry and executable description use the software name, “仙伴 · AI桌面伴侣”. On normal launch, the updated app migrates an existing `OrbitPet` startup entry while preserving its command and Windows enabled/disabled state. It does not enable startup when no entry exists.

<a id="custom-characters"></a>
## Create your own companion

Give an original character its own appearance, personality and reminder performance. **0.5 supports external character packs without reinstalling or changing application source.** Right-click the pet → character menu → open the character resource folder, then copy in a complete pack. The app detects and validates changes; new characters appear in the menu and valid updates to the selected character apply live. Invalid updates keep the currently loaded artwork.

The default folder is `%LOCALAPPDATA%\XianPal\Characters`. A pack contains a profile, 240 transparent PNG frames across 12 states, and per-frame hashes—not a single-image automatic animator. See the [pack format and export tutorial](docs/CHARACTER-PACKS.md) (Chinese).

1. **Define the design:** copy the [character specification template](docs/templates/CHARACTER-SPEC.md). Record the character's identity, body type, personality, quotes and asset provenance. Approve normal and side views before animation; motion fixes must not redesign the face, clothes or proportions.
2. **Prepare transparent parts:** provide the head, torso, two complete arms/hands, two legs/boots and mouth/eye reference heads; quadrupeds use head/neck, body, four limbs and tail. Supply normal and walking views separately. Record reviewed pixel crops in `regions.json`, not an assumed regular grid.
3. **Attach and animate:** calibrate neck, shoulder, palm, hip and sole anchors. Use opposite arm/leg coordination for bipeds and diagonal alternation for quadrupeds. Walking needs at least 24 distinct phases, elbow bending and subtle neck motion. Both arms/forelimbs remain the topmost character layers.
4. **Add expression and performance:** preserve the head silhouette when changing eyes or mouth. Built-in rigs use local patches; external packs bake expressions into complete frames. Cover waving, reviewing, eating and mouth-wiping, then choose a preset performance while preserving reduced motion, Esc dismissal and single-monitor boundaries.
5. **Review before packaging:** inspect both walking directions, stopping, transitions, joint connections, transparency and clipping. Automated checks do not replace visual approval.

For resource-only characters, fill in the [profile template](docs/templates/character-profile.json), prepare the animation frames, and run [ExportCharacterPack.ps1](tools/ExportCharacterPack.ps1). The source entry points below are for extending the built-in renderer, rig or app capabilities:

| What to customize | Entry points |
| --- | --- |
| Name, personality, quotes and reminder style | [PetCharacter.cs](src/DesktopTasks/XianPal/PetCharacter.cs) |
| Layered art and crop regions | [art/rigs](art/rigs), [art/side-rigs](art/side-rigs), human walking torsos in [art/side-torsos](art/side-torsos) |
| Texture import, animation and expression generation | [BuildOriginalCharacters.cs](tools/BuildOriginalCharacters.cs) |
| Joint anchors, elbow curves, feet and gesture targets | [CompanionMotion.cs](src/DesktopTasks/XianPal/CompanionMotion.cs) |
| Asset loading, speed-adaptive playback and expressions | [PetForm.cs](src/DesktopTasks/XianPal/PetForm.cs), [PetForm.Expressions.cs](src/DesktopTasks/XianPal/PetForm.Expressions.cs) |
| Character-specific performance timeline | [ReminderPerformance.cs](src/DesktopTasks/XianPal/ReminderPerformance.cs) |

The IDs `orbit`, `spark`, `moss` and `pippin` are reserved for built-in characters. External packs use unique IDs and do not change built-in generation counts. Only new compiled-in rigs or rendering algorithms require geometry, preview and test extensions. See the [customization guide](docs/CHARACTER-CUSTOMIZATION.md) (Chinese) for rigging and artwork rules.

From the project root on Windows:

```powershell
# Generate assets, app and offline previews without an installer or model calls
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -SkipInstaller
.\tests\companion-motion-test.ps1
.\tests\original-assets-test.ps1
.\tests\reminder-performance-test.ps1
```

After building, open `build/previews/index.html` for animation review and `build/previews/performances.html` for performances. Keep original inputs in `art/`, `src/` and `tools/`; **never keep the only copy of handmade artwork inside the replaceable `build/payload` directory**.

Recipients only copy the exported pack; they need no compiler. External expressions are baked into complete frames, not overlaid with built-in head geometry. Reminder performances currently reuse four preset timelines with the pack's own images; executable content and custom timeline scripts are not loaded.

Use original art you have permission to license, and include actual prompts, reference permissions and edit records. Recoloring or renaming a known fictional character is not an original design. Read the [asset license scope](ASSET-LICENSE.md), [performance notes](docs/REMINDER-PERFORMANCES.md) and [contribution requirements](CONTRIBUTING.md) before submitting.

## Validate and contribute

### Help shape the next versions

Small, focused contributions are welcome: original characters and animation tools; multi-monitor, DPI and performance fixes; CLI/task compatibility and diagnostics; isolated tests, guides and translations.

Discuss the scope in [Issues](https://github.com/AHuaWinliver/xianpal-ai-companion/issues), then follow [CONTRIBUTING](CONTRIBUTING.md). Include reproduction steps and test results, plus previews for visual changes. Never upload credentials, private conversations or intranet data.

```powershell
.\tools\TestRepository.ps1
.\tests\release-source-test.ps1
.\tests\startup-registration-test.ps1
.\tests\character-pack-test.ps1
.\tests\smoke-test.ps1
.\tests\drag-gait-test.ps1
.\tests\companion-motion-test.ps1
.\tests\original-assets-test.ps1
.\tests\reminder-performance-test.ps1
```

Tests use mocks or isolated directories, not paid model calls. Window, virtual-desktop and installer checks need a Windows desktop session. See actual CI results in [Actions](https://github.com/AHuaWinliver/xianpal-ai-companion/actions); local success is not a compatibility guarantee for every Windows environment.

Read [CONTRIBUTING](CONTRIBUTING.md), then share reproducible bugs or ideas in [Issues](https://github.com/AHuaWinliver/xianpal-ai-companion/issues). Never submit credentials, private work records or unauthorized character artwork.

## Privacy, permissions and limits

- AI requests follow your CLI configuration and may use the network or paid quota.
- Installation appends this app's hooks to the current user's Claude settings and creates a backup, preserving other handlers. See [privacy notes](docs/PRIVACY.md).
- This is **not a security sandbox**. File access and task execution follow your user account and CLI permissions. Do not automatically trust unfamiliar directories or prompts.
- Proactive AI features can be disabled. Capturing other local Claude sessions is off by default; the app does not automatically terminate them.
- Background support, Terminal availability, fonts and virtual-desktop behavior depend on your environment. Voice conversation is not included.
- No affiliation with Anthropic, Microsoft or an existing fictional franchise is claimed.

## License and documentation

Code is licensed under [Apache-2.0](LICENSE). The bundled AI-assisted original character art has a separately explained [license scope](ASSET-LICENSE.md) and [provenance record](art/PROVENANCE.md). AI generation is not an absolute non-infringement guarantee. System fonts are not redistributed.

[Development](docs/DEVELOPMENT.md) · [Character customization](docs/CHARACTER-CUSTOMIZATION.md) · [Performances](docs/REMINDER-PERFORMANCES.md) · [Publishing guide](docs/GITHUB-PUBLISH.md) · [Changelog](CHANGELOG.md) · [Security](SECURITY.md)

---

**A little presence. A practical helping hand.** If XianPal makes your desktop feel more welcoming, a Star is appreciated.
