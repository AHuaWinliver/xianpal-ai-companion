# XianPal @VERSION@ · Linux @ARCH@ candidate

This candidate includes the self-contained @RUNTIME@ application, original character resources and bundled Noto fonts. A compatibility package also includes application-private C++ libraries and their licenses. It does not include Claude Code, a model, credentials or system RPM/DEB packages.

Source: `@SOURCE_COMMIT@`

Automated validation: @VALIDATION_URL@

Container checks validate userspace libraries and share the CI host kernel. The actual intranet kernel, TurboVNC session and Claude service still require acceptance. This is not a stable Linux release.

## Verify and try

1. Verify the external `.sha256` file, then extract with `tar -xzf` to retain executable modes.
2. In the extracted directory, run `sha256sum -c SHA256SUMS.txt`.
3. In your graphical desktop terminal, run `sh diagnose-linux.sh`, `sh launch.sh --version` and `sh launch.sh --self-test`.
4. For the first TurboVNC trial, run `XIANPAL_RENDERER=software sh launch.sh`.
5. In Default settings, select your real Claude executable, working directory, language and optional per-companion JSON profiles. Under `~/APP/claude`, choose the file that supports `--version`, rather than its containing folder.

The application needs no installed .NET SDK, Node.js or Python. Your independent CLI may have its own runtime requirements. System ICU, OpenSSL, Fontconfig and X11 libraries must be available. Native terminal tasks need a supported terminal. Prepare approved offline dependencies for the exact target OS if diagnostics identify missing libraries; do not run connected-machine download scripts on the intranet target.

Diagnostics and self-tests do not install packages or submit model requests. The app's AI features use your configured service and may consume usage.

## Acceptance

- Four companions: left/right gait, drag-speed response, stopping, edge docking, hide/show, proportions and transparency.
- Chinese/emoji, initial reply placement, chat follow-up and file-drop request confirmation.
- Task owner, working directory, overlap policy, native terminal, independent history and Background where your CLI supports it.
- Current-monitor countdowns/reminders, reduced motion, dismissal and character-specific performances.
- Workshop desks, all statuses, run history, actual handoffs and window resizing.
- External-pack discovery, live valid updates, broken-update retention and removal fallback.

Trails require an active X11 compositor and native mouse-through support; otherwise they are disabled. Check the actual VNC desktop, not just an automated screenshot.

After the direct trial passes, quit the pet and run `sh install.sh`, or `sh install.sh --silent --install-dir "$HOME/.local/opt/xianpal"`. Updates retain the previous installation. State and character packs remain in XDG user directories. Independent Claude jobs are not stopped automatically.

Record passed, failed and untested cases. Redact private endpoints, credentials, paths and transcripts before sharing diagnostics.
