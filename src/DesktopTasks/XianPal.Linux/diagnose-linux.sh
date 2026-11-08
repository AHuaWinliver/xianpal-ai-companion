#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Read-only deployment inventory. No model requests or package installation.
set -u
printf 'System\n'
if [ -r /etc/os-release ]; then
    sed -n '/^NAME=/p; /^VERSION=/p; /^ID=/p; /^VERSION_ID=/p' /etc/os-release
elif [ -r /etc/centos-release ]; then
    cat /etc/centos-release
fi
printf '\nArchitecture and kernel\n'
uname -m
uname -r
printf '\nC library\n'
getconf GNU_LIBC_VERSION 2>/dev/null || ldd --version 2>&1 | head -n 1
printf '\nDesktop session\n'
printf 'SESSION=%s\nDESKTOP=%s\nDISPLAY=%s\n' "${XDG_SESSION_TYPE:-unknown}" "${XDG_CURRENT_DESKTOP:-unknown}" "${DISPLAY:-unset}"
printf 'WAYLAND_DISPLAY=%s\nLANG=%s\nLC_ALL=%s\n' "${WAYLAND_DISPLAY:-unset}" "${LANG:-unset}" "${LC_ALL:-unset}"
if [ -z "${DISPLAY:-}" ]; then
    printf 'X11 display is unavailable. Run inside the graphical session; Wayland needs XWayland for the current build.\n'
fi
if command -v xrandr >/dev/null 2>&1 && [ -n "${DISPLAY:-}" ]; then
    xrandr --listmonitors 2>&1 || true
fi
printf '\nNative library inventory\n'
linker_cache=$(command -v ldconfig 2>/dev/null || true)
if [ -z "$linker_cache" ] && [ -x /sbin/ldconfig ]; then linker_cache=/sbin/ldconfig; fi
if [ -n "$linker_cache" ]; then
    "$linker_cache" -p 2>/dev/null | awk '/lib(X11|Xext|Xrender|Xfixes|Xss|ICE|SM|fontconfig|GL|EGL|icuuc|ssl|crypto|stdc\+\+|gcc_s)/ {print}' || true
else
    printf 'ldconfig is unavailable; inspect dependencies in the prepared package.\n'
fi
printf '\nTerminal commands\n'
for terminal in x-terminal-emulator gnome-terminal konsole xfce4-terminal mate-terminal lxterminal xterm; do
    command -v "$terminal" 2>/dev/null || true
done
printf '\nInstalled .NET runtimes\n'
if command -v dotnet >/dev/null 2>&1; then dotnet --list-runtimes; else printf 'not found\n'; fi
printf '\nExisting CLI runtimes (not required by the self-contained desktop application)\n'
if command -v node >/dev/null 2>&1; then node --version; fi
if command -v python3 >/dev/null 2>&1; then python3 --version; fi
printf '\nClaude Code\n'
claude_cli=$(command -v claude 2>/dev/null || true)
if [ -z "$claude_cli" ]; then
    for candidate in "$HOME/APP/claude" "$HOME/APP/claude/claude" "$HOME/APP/claude/bin/claude" "$HOME/.local/bin/claude"; do
        if [ -f "$candidate" ] && [ -x "$candidate" ]; then claude_cli=$candidate; break; fi
    done
fi
if [ -n "$claude_cli" ]; then printf 'command=%s\n' "$claude_cli"; "$claude_cli" --version; else printf 'not found\n'; fi
