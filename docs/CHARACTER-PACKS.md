# Character packs and hot updates

[简体中文](CHARACTER-PACKS.zh-CN.md)

A compatible XianPal installation can load new companions from local resource files without recompiling or reinstalling. Packs are data-only: character metadata, complete PNG animation frames and SHA256 checksums. They cannot load assemblies, scripts or commands.

## Install or update a pack

1. Right-click the companion and open the character resource directory.
2. Copy the complete `<id>` folder into it. `character.json` must be directly inside that folder.
3. Allow copying and validation to finish, then select the new external companion. Use refresh/validation results if it does not appear.
4. When updating, replace frame files first and the manifest last. Prefer preparing a complete pack in a dot-prefixed staging folder and moving it into place.

Windows defaults to `%LOCALAPPDATA%\XianPal\Characters`; `XIANPAL_CHARACTER_DIR` can select another directory before launch. Linux uses the XDG data directory's `xianpal/Characters`. Paths must not contain symlinks or directory junctions. Packs live outside the installation and do not require administrator access.

Windows debounces changes before validation. The Linux port currently rescans periodically. An invalid update retains the already decoded character in memory; this is not a persistent backup. Invalid files will not load after restart. Keep a separate known-good pack. Removing the selected external pack falls back to Qingheng.

## Folder contract

```text
Characters/my-companion/
  character.json
  idle/00.png ... 07.png
  waiting/00.png ... 07.png
  failed/00.png ... 07.png
  running/00.png ... 23.png
  running-left/00.png ... 23.png
  running-right/00.png ... 23.png
  waving/00.png ... 23.png
  review/00.png ... 23.png
  jumping/00.png ... 23.png
  punching/00.png ... 23.png
  eating/00.png ... 23.png
  wiping-mouth/00.png ... 23.png
```

Each pack contains 240 RGBA PNGs at exactly 384 × 416. Each image is limited to 2 MB, the manifest to 64 KB and the complete pack to 64 MB. Up to 16 non-staging pack directories are scanned. The three running states must each contain 24 distinct image hashes, not copies of eight poses. Optional root files are `README.md` and `LICENSE.txt`; unknown files or subdirectories are rejected.

## Manifest

Start with the [profile template](templates/character-profile.json). It is exporter input, not an installable manifest until the checksum list has been generated.

| Field | Contract |
| --- | --- |
| `schemaVersion` | `1` |
| `id` | 3–48 characters, starts with a lowercase letter, lowercase letters/digits/hyphens only; matches its folder |
| `version` | Three-part resource version, independent of the app version |
| `name`, `persona` | Display name up to 20 characters; personality up to 4,000 |
| `quotes` | 5–50 quotes, each 1–200 characters |
| `canvasWidth`, `canvasHeight` | `384`, `416` |
| `mouthX`, `mouthY` | Normalized file-card target, strictly between 0 and 1 |
| `reminderStyle` | `command` sword flight, `beam` talismans, `warning` plants, `stone` ink ripples |
| `files` | Exported map of all 240 relative frame paths to SHA256 hashes |

Built-in IDs `orbit`, `spark`, `moss`, `pippin` and historical compatibility IDs are reserved. New external companions do not need to share the built-ins' body types or names.

## Export and validate

Render all animation frames with your preferred tools. Layered art or rig JSON cannot replace the finished frames. The exporter currently runs on Windows with the .NET Framework compiler; recipients of the completed pack need neither.

```powershell
.\tools\ExportCharacterPack.ps1 `
  -FramesDirectory C:\my-art\rendered `
  -ProfilePath C:\my-art\character-profile.json `
  -OutputRoot C:\my-packs
.\tools\ValidateCharacterPack.ps1 -PackDirectory C:\my-packs\my-companion
```

The exporter copies frames, computes checksums and uses the production validator. It refuses to overwrite an existing output. Review the result, include artwork provenance and licensing, then share the whole `<id>` folder.

## Animation requirements

Keep identity, proportions, lighting and outlines consistent across states. Use real alpha, safe margins and smooth contralateral walking. Check both directions and the final-to-first frame transition.

External packs own their complete expression frames. Built-in head/neck parameters and eye/mouth patches are not applied to arbitrary external artwork. Encode blinks and mouth changes into your own frames. Reminder styles reuse the supported timelines with the selected character's frames; packs cannot provide executable effect scripts.

See the [customization guide](CHARACTER-CUSTOMIZATION.md) for joints, contact points, layering and review criteria. Structural validation does not establish visual quality or artwork rights.

After building, run `tests/character-pack-test.ps1` for isolated add/switch/update/corruption/recovery/removal and export checks. No real model is called.
