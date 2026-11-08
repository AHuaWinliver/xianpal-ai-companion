# Contributing to XianPal

[简体中文](CONTRIBUTING.zh-CN.md)

Contributions to code, tests, documentation, translations and original character
packs are welcome. For a substantial change, open an
[issue](https://github.com/AHuaWinliver/xianpal-ai-companion/issues) first to agree on
scope. Keep pull requests focused and describe the user-visible result.

## Development

Follow the [build and testing guide](docs/DEVELOPMENT.md). Run the build, smoke tests
and checks relevant to the changed modules. Include reproduction steps, test results,
compatibility implications and any unverified environments in your pull request.
Use isolated state, settings and installation directories in tests. Automated tests
must not call a paid model or modify a user's real Claude configuration.

Avoid blocking CLI queries on the UI thread. Preserve Claude Code's permission and
workspace-trust checks. Manage only sessions started by XianPal; do not terminate
unrelated Claude processes. Do not include credentials, private conversations or
intranet configuration in commits, issues or screenshots.

## Documentation and language

English is the default language for public project information. Keep
[README.md](README.md) and [README.zh-CN.md](README.zh-CN.md) aligned; retain
[README.en.md](README.en.md) as a compatible English entry point.

For a substantial feature, document its purpose, entry point, essential steps and
expected behavior in the same pull request. Add screenshots, animation or a diagram
under `docs/images/`, using relative paths and descriptive alternative text. Label
demonstration data. Put release-specific details in the changelog and release notes.
Describe measured behavior and distinguish automated checks from target-machine tests.

## Characters and animation

Read the [character customization guide](docs/CHARACTER-CUSTOMIZATION.md) and use the
[character specification](docs/templates/CHARACTER-SPEC.md). Include rebuildable
inputs, source and permission information, rig regions and anchors, and previews
for both walking directions and other affected states.

Preserve approved character designs when fixing motion. Bipeds use opposite arm
and leg movement; quadrupeds use diagonal coordination. Keep both arms or forelimbs
in the established front rendering layer. Adding a character must update assumptions
and tests about the number of profiles rather than remove those checks.

Contribute only work you have permission to license. Record the source, license and
creation process of visual assets, including reference permissions and prompts for
AI-assisted art. AI generation alone does not establish rights to a reference.
Code contributions use [Apache-2.0](LICENSE); consult the separate
[asset terms](ASSET-LICENSE.md) for artwork.
