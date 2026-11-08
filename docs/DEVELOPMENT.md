# Development and rendering

[简体中文](DEVELOPMENT.zh-CN.md)

For artwork, joint constraints and the full review checklist, start with [character customization](CHARACTER-CUSTOMIZATION.md). For installable data-only additions, use [character packs](CHARACTER-PACKS.md).

## Application structure

Windows uses C# and .NET Framework / WinForms in `src/DesktopTasks/XianPal`; the installer is in `XianPal.Installer`. Keep shared/Windows files compatible with the existing C# compiler. Linux has a separate Avalonia application in `XianPal.Linux`, linking compatible shared services with `XIANPAL_LINUX`.

| Module | Responsibility |
| --- | --- |
| `PetForm` / `PetWindow` | Render state, position, visibility and input |
| `PetCharacter`, `PetBrain` | Identity, personality, quotes and AI request protocol |
| `CompanionMotion`, `DragGaitClock` | Attached joints and velocity-driven gait |
| `CompanionRoutes`, `ArtifactTravelClock`, `WalkingExpressionClock` | Shared route geometry, travel timing and independent expressions |
| `ClaudeCliRunner`, `NativeTaskHooks` | CLI invocation, streaming and session ownership |
| `IntegrationPreferences`, `ClaudeSettingsFile` | Per-companion executable and settings inheritance |
| `ClaudeTaskService`, task windows | Schedule, owner, overlap policy and run history |
| `BackgroundTaskSupervisor` | Native Background dispatch, polling and recovery |
| `ReminderService`, `ReminderPerformanceTimeline` | Reminder recurrence and shared performance timing |
| `SectWorkshop` | Team dependency graph, handoffs and persistence |

## Artwork pipeline

`art/approved-design.png` is the identity baseline. `art/rigs` contains normal transparent parts, `art/side-rigs` side-view parts, and `art/side-torsos` corrected human torsos. Crops are reviewed source-pixel rectangles, not an assumed sprite grid. The build imports these local inputs without generation services or network calls.

`tools/BuildOriginalCharacters.cs` renders 384 × 416 RGBA at 2× supersampling. The four built-ins produce 960 state frames, 48 expression patches and two legacy-name compatibility images. Walking uses genuine side-view parts while interactions retain approved normal art. Mesh deformation preserves shoulder/neck/hip attachment and complete rigid boots; front limbs are drawn last.

Head inertia and eye/mouth patches use the same attachment transform as the underlying pose. Expression timing is independent of the 24-phase gait. Never compensate for a disconnected head by moving its patch separately.

## Long-distance movement

Travel length includes every route segment, including a return leg. `ArtifactTravelClock` mounts when total distance exceeds 40% of the active work area's diagonal, with a 480 px minimum. Short movement retains walking. No additional AI call is needed to choose a vessel.

Takeoff uses a 550 ms smoothstep from 110 to 360 px/s; arrival stops translation and lands over 450 ms. Capped movement deltas prevent a delayed UI tick from teleporting the pet. Cancellation, dragging, hiding, switching character and reduced motion clear the same travel state.

The vessel uses the real opaque sole row, approved standing art, proportional scale and one shared facing transform. Human art and turtle art have different native directions. Available styles are sword, talisman scroll, leaf boat and ink-jade disc. A new shape needs its own sole/facing review; do not distort approved art to fit a prop.

Windows uses `ArtifactTravelArt`; Linux uses `NativeArtifactView`. Route, envelope and expression clocks are shared. Linux's optional native trail requires an empty X11 input region and working transparency; otherwise it is disabled to preserve desktop interaction.

## Tests and exports

Run the commands in the main README and relevant focused tests. Motion tests inspect actual imported layers, neck skin overlap, complete boots, soles, turtle sockets, elbow curves, opposite limbs and loop continuity. Desktop/CLI tests require a real Windows desktop session and isolated state. CI uses fake CLIs rather than paid model requests.

Linux builds, font staging, architecture checks and target limitations are documented in [Linux compatibility](LINUX.en.md). Native X11 validation captures real windows, task history, locale switching, tray recovery, installation and motion evidence.

`tools/PublicFiles.ps1` defines the public source set. `TestRepository.ps1` checks known secret signatures and accidental private paths; it is not a comprehensive security or rights audit. `ExportOpenSource.ps1` creates a standalone archive and checksums. Rebuild the export in a short clean directory before release.
