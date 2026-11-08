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
printf '\nInstalled .NET runtimes\n'
if command -v dotnet >/dev/null 2>&1; then dotnet --list-runtimes; else printf 'not found\n'; fi
printf '\nClaude Code\n'
claude_cli=$(command -v claude 2>/dev/null || true)
if [ -z "$claude_cli" ]; then
    for candidate in "$HOME/APP/claude" "$HOME/APP/claude/claude" "$HOME/APP/claude/bin/claude" "$HOME/.local/bin/claude"; do
        if [ -f "$candidate" ] && [ -x "$candidate" ]; then claude_cli=$candidate; break; fi
    done
fi
if [ -n "$claude_cli" ]; then printf 'command=%s\n' "$claude_cli"; "$claude_cli" --version; else printf 'not found\n'; fi
