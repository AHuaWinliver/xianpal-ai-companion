# Release acceptance

[简体中文](RELEASE-CHECKLIST.zh-CN.md)

- [ ] `VERSION`, app/installer metadata, changelog, tag and asset names agree.
- [ ] The tag identifies the exact tested source commit; published assets are not silently replaced.
- [ ] English/Chinese README and guide links work; screenshots use sanitized demo data and English-first captions.
- [ ] Major features include images and setup instructions. Changes and versions are recorded in release notes rather than dominating the README.
- [ ] Required tools are included by the source exporter; `TestRepository` and `release-source-test` pass.
- [ ] Code and artwork provenance, licensing, `LICENSE` and `NOTICE` have been reviewed. Apache-2.0 does not license unrelated third-party characters.
- [ ] A clean short-path source export rebuilds without private settings, logs or cached dependencies.
- [ ] Relevant motion, transparency, chat/IME/emoji, CLI, schedule/history, workshop, installation and startup checks pass.
- [ ] Four companions move in both directions, vary cadence with drag speed, stop correctly and retain their approved proportions.
- [ ] Installation supports the chosen folder, upgrades preserve existing data, and recovery/cleanup behavior is checked.
- [ ] Real target CLI version, permissions, native Background, display scaling and multiple monitors are tested.
- [ ] Linux releases pass the system/architecture matrix plus real kernel/desktop acceptance; container evidence is not presented as a TurboVNC test.
- [ ] Attachments contain the platform package, source archive, checksums and accurate known limitations.
- [ ] Branch protection, repository permissions and public reports do not expose credentials or private data.
