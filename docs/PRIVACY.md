# Privacy and local configuration

[简体中文](PRIVACY.zh-CN.md)

XianPal stores companion selection, position, scheduled-task prompts and directories, and run history locally. These records can contain private information. Historical Windows installations may retain compatibility state names; do not upload the state directory with source code. Linux uses the user's XDG directories.

- External character packs contain local metadata and PNGs. Loading a pack does not download updates or execute plugins. Its personality text is used in AI prompts; import packs from sources you trust.
- Claude credentials, models, service endpoints and permissions remain in your existing CLI configuration. XianPal does not bundle API keys.
- The Windows installer adds owned hooks, preserves other handlers and creates a backup. Silent installation does this too. Install only on accounts where you intend to enable the integration.
- Monitoring unrelated local Claude sessions is off by default and requires explicit opt-in. Owned scheduled runs are identified so their output is not duplicated into ordinary chat.
- Proactive messages, AI routes and history introductions call the configured CLI. They may contact a network service and consume usage.
- Dropping a file stages paths and a request. Sending allows the CLI to process those paths according to its existing permissions; the animation itself deletes nothing.
- XianPal has no separate analytics server. Your CLI, MCP tools and configured services can have their own network and retention behavior.
- Windows uninstall removes owned hooks, startup entries and shortcuts. It does not recursively delete the installation directory or stop unrelated Claude jobs. Review retained files before cleaning them up.

Redact usernames, intranet endpoints, paths, prompts and model output before publishing screenshots or logs. Report sensitive vulnerabilities through the [security policy](../SECURITY.md).
