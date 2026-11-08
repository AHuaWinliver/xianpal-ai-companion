# Publishing to GitHub

[简体中文](GITHUB-PUBLISH.zh-CN.md)

Repository: [AHuaWinliver/xianpal-ai-companion](https://github.com/AHuaWinliver/xianpal-ai-companion).

## Prepare a clean source tree

Review `.gitignore` and `git status`. Keep application state, transcripts, credentials, generated builds, archives and backups out of Git. Use `tools/ExportOpenSource.ps1` for an allowlisted source archive; it refuses to overwrite an existing archive.

```powershell
.\tools\TestRepository.ps1
.\tests\release-source-test.ps1
.\build.ps1
.\tests\smoke-test.ps1
.\tests\drag-gait-test.ps1
.\tools\ExportOpenSource.ps1
```

Build and test a clean, short-path copy before delivery. Refer to [release acceptance](RELEASE-CHECKLIST.md) for the remaining gates.

## Contribute through a pull request

Clone the existing repository, create a feature branch, commit only the intended files and open a pull request. Do not force-push over repository history. Use the contribution guide and include images/instructions for major visual features. Check actual Actions results rather than manually asserting that CI passes.

Use an English repository description and default README, with a visible link to Simplified Chinese. Keep feature descriptions evergreen; put version-specific changes in `CHANGELOG.md`, tags and release notes. The social cover is `docs/images/social-preview.png`.

## Publish a release

Choose a new version, update `VERSION`, application/installer assembly metadata and changelog together, then tag the tested commit as `v<version>`. Never replace an already published asset with different bytes under the same version.

Attach the installer or platform archive, clean source archive and SHA256 checksums to the release rather than committing binaries into Git. Preview builds should be marked as pre-releases. Release notes should lead with user-visible changes, then setup requirements, known limitations and validation evidence. Use English first with an optional Chinese section.

For Linux, require the architecture/distribution matrix and the actual target desktop acceptance. Userspace containers do not establish compatibility with an old kernel or a TurboVNC session. Do not advertise complete parity or target support before those checks pass.
