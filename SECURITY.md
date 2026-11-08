# Security policy

[简体中文](SECURITY.zh-CN.md)

## Reporting a vulnerability

Please do not disclose exploit details, credentials, private paths or sensitive logs
in public issues. Use the repository's **Security → Report a vulnerability** option
when private reporting is available. Otherwise, ask a maintainer for a private
reporting channel before sharing sensitive details.

Include the affected release or commit, operating system, reproduction steps,
expected impact and a minimal redacted example. Security fixes are maintained on
a best-effort basis for the latest public release; the project does not offer an SLA.

## Security boundaries

XianPal integrates with a locally installed Claude Code CLI. Claude's configuration,
the operating-system user and the service selected by that user determine its
permissions and data processing. XianPal is not an execution sandbox and must not
bypass Claude Code's workspace-trust or permission prompts.

External character packs contain configuration and PNG frames; they do not load
DLLs, scripts or executable plugins. Packs are checked for dimensions, frame counts,
size limits and per-frame hashes. Checksums detect corruption and incomplete copies;
they are not signatures and do not establish the identity or rights of an author.
Review a character's persona before importing it because it can become model input.

Tests isolate state, settings and installation directories. A passing automated test
does not grant permission to execute tasks against untrusted files or prompts.
