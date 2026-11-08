# Linux desktop port and compatibility

[中文](LINUX.md) · [Project overview](../README.en.md)

The `linux` branch contains the Linux desktop port. It shares character packs,
Claude configuration, scheduling, reminders and agent dependency execution with
the Windows application. Full migration and release acceptance are still in progress.

## Build families

| Build | Intended use | Current verification |
| --- | --- | --- |
| Modern x64 / ARM64 | Recent glibc-based distributions; .NET 10 and Avalonia 12 | Native Ubuntu 22.04 and 24.04 runners, X11 windows, shared services and glyph coverage |
| Compatibility x64 | Older system libraries; .NET 8 and Avalonia 11 with private C++ libraries | CentOS 7.9 userspace with glibc 2.17, X11 windows, shared services and glyph coverage |

Compatibility tests are application tests, not a change to upstream OS or runtime
support policies. Prefer the modern build when the system permits it. The legacy
build requires a separate runtime maintenance plan before release.

All 16 system/architecture checks across 12 distribution versions have passed:

| Systems | Architecture | Test environment |
| --- | --- | --- |
| Ubuntu 22.04 / 24.04 | x64, ARM64 | Native runners |
| CentOS 7.9, glibc 2.17 | x64 | Pinned userspace image, compatibility build |
| Debian 11 / 12 / 13; Ubuntu 20.04 | x64 | Pinned userspace images, modern build |
| Rocky Linux 8 / 9; AlmaLinux 8 / 9 | x64 | Pinned userspace images, modern build |
| openSUSE Leap 16 | x64 | Pinned userspace image, modern build |
| Debian 12 / 13 | ARM64 | Pinned images on native ARM64 hosts, modern build |

The tests cover four character profiles, Unicode streaming chat, independent task
records, reminder and fullscreen windows, four-agent dependency handoffs and Chinese
and color emoji glyphs. CentOS and the expanded distribution matrix explicitly use
CPU rendering; native Ubuntu jobs test the default rendering configuration. Debian
11 test dependencies come from the official archive with package signature checks.

Containers validate userspace libraries and share the host kernel. They do not
validate a distribution's original kernel, every desktop environment or the target
TurboVNC session.

[Complete compatibility matrix results](https://github.com/AHuaWinliver/xianpal-ai-companion/actions/runs/42484766977)

![Native Linux workshop using isolated demonstration data](images/linux-workshop.png)

## Build

```sh
python3 src/DesktopTasks/XianPal.Linux/fetch-fonts.py build/linux-resources/fonts
dotnet publish src/DesktopTasks/XianPal.Linux -r linux-arm64 --self-contained true -c Release -o build/linux-app
build/linux-app/xianpal --self-test
```

Character assets are generated with the existing Windows artwork tools. CI transfers
the approved `assets/`, `scenes/` and `performances/` resources into the Linux output.
The pinned Noto CJK and Color Emoji fonts are embedded in both builds; their OFL 1.1
licenses and source checksums accompany the resources in `fonts/`. Only the build
machine downloads fonts. The installed application makes no font download requests.

For the legacy x64 candidate:

```sh
dotnet publish src/DesktopTasks/XianPal.Linux -p:LinuxCompat=true -r linux-x64 --self-contained true -c Release -o build/linux-compat-x64
python3 src/DesktopTasks/XianPal.Linux/bundle-runtime.py build/linux-compat-x64
build/linux-compat-x64/launch.sh --self-test
```

`bundle-runtime.py` verifies fixed SHA256 hashes, stages application-private C++
libraries in `runtime/`, and includes the GCC license, runtime exception and source
recipes. `launch.sh` loads them for this application. No system library is replaced.

## Intranet deployment

The current target is CentOS 7 x86_64, kernel 3.10.0-1160.el7, glibc 2.17 and an
X11 desktop through TurboVNC. Claude is under `~/APP/claude`. This differs from the
Windows build machine and modern CI hosts.

Prepare the complete package and its architecture-specific native dependencies on
a connected machine. The self-contained application does not need an installed
.NET SDK, Node.js or Python at runtime. The user's independent Claude installation
may need its own runtime. Native ICU, OpenSSL, Fontconfig and X11 libraries remain
system dependencies; copy approved offline packages for the exact target OS rather
than running a network installer on the intranet machine.

Read-only inventory:

```sh
sh src/DesktopTasks/XianPal.Linux/diagnose-linux.sh
```

Use the current graphical session and its `DISPLAY`. The current build uses X11;
Wayland sessions need XWayland. For a VNC session with unusable GLX, select CPU
rasterization before launch:

```sh
XIANPAL_RENDERER=software ./launch.sh
```

An unset renderer uses GLX with software fallback. Chinese and emoji fonts are
bundled, so replacing system fonts is unnecessary. State uses the user's XDG state
directory; external character packs use the XDG data directory's `xianpal/Characters/`.

## Installation and desktop integration

The self-contained Linux directory includes `install.sh`. Run `sh install.sh` in
your graphical session to select a destination, switch between English and Chinese,
and preview the original character animations. For unattended installation, use:

```sh
sh install.sh --silent --install-dir "$HOME/.local/opt/xianpal"
```

Installation checks the payload and verifies each copied file. Updates retain the
complete previous directory beside the new installation. An unrelated nonempty
folder is rejected. Personal state and external character packs remain in the XDG
directories. Quit the companion before installing; the installer does not stop
independent Claude jobs. It creates a current-user desktop menu launcher.

**Default settings** includes user autostart, per-companion CLI executable and JSON
settings, and docked opacity. A tray menu can show or hide the companion. Tray display
depends on the desktop's StatusNotifierItem / AppIndicator support; launching XianPal
again also restores a hidden window. [Avalonia tray integration](https://docs.avaloniaui.net/controls/navigation/trayicon/)

Native terminal tasks keep Claude's interactive interface and terminal input. Each
scheduled task has an explicit companion owner. Claude Background dispatch, status,
logs and history are isolated from normal conversations. The native test suite uses
fixture CLIs and real `xterm` windows; it does not submit model requests.

## Remaining acceptance

Native X11 input tests cover all four companions: dragging from the expanded hit
area in both directions, faster gait at faster pointer speeds, a held-drag pause
returning to standing, release cleanup, first reply placement, emoji-safe truncation
and right-click dismissal. Input injection requires both the private test mode and
`XIANPAL_CI_INPUT=1`; it is disabled during ordinary desktop use. Character-pack
validation runs off the render thread and defers replacement during interactions.
Tests cover valid replacement, malformed-update retention, selection races and
removed-pack fallback in isolated XDG directories.

### Offline candidate archives

After native checks pass, CI builds self-contained `.tar.gz` candidates with
executable permissions, SHA256 manifests and English/Chinese first-run instructions.
`VALIDATION.json` identifies the source and CI run and keeps actual target acceptance
marked pending. Downloaded ZIP artifacts must be unpacked to retrieve the TAR;
extract the TAR on Linux to preserve permissions.

```sh
python3 tests/linux-package-test.py
python3 src/DesktopTasks/XianPal.Linux/package-linux.py build build/linux-compat-x64 \
  --compat --output dist/XianPal-Linux-Compat-x64-Candidate.tar.gz \
  --source-commit FULL_TESTED_COMMIT_SHA \
  --validation-url https://github.com/AHuaWinliver/xianpal-ai-companion/actions/runs/RUN_ID
python3 src/DesktopTasks/XianPal.Linux/package-linux.py verify dist/XianPal-Linux-Compat-x64-Candidate.tar.gz
```

Use the published folder with all character resources, fonts, runtime libraries and
licenses. Packaging is offline, rejects missing resources and personal state, and
never overwrites an existing archive. Tests reject damaged payloads, unsafe archive
paths, links, missing frames and mismatched runtime families. Python is a build-time
tool; it is not required to run the delivered desktop application.

The candidate includes shared 24-phase motion, spirit-artifact travel, batched AI
routes, proactive care, optional light trails and external-character performances.
Native tests cover directional gait progression, cancellation, opaque edge docking,
countdown arrival, a single fake-CLI request reused for 24 route plans, and all four
external performance styles. Trails require both native mouse-through support and
an active X11 compositor; otherwise the overlay is disabled.

The responsive hall now uses original frames, desks and recorded handoff connections.
Real desktop input, display scaling, multiple monitors, the target CLI and final
visual acceptance remain required. Fake-CLI success does not establish complete
Windows feature parity or target-environment acceptance.

The target's kernel 3.10, actual Claude installation and TurboVNC must be checked on
the intranet desktop before claiming that environment is fully supported. See the
[full acceptance checklist](LINUX.md#发布验收).
