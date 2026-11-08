# Companion specification

[简体中文](CHARACTER-SPEC.zh-CN.md)

Copy and complete this review record. It is not an importable manifest. Read the [customization rules](../CHARACTER-CUSTOMIZATION.md) and [pack contract](../CHARACTER-PACKS.md).

## Identity and scope

- Display name / stable ID:
- Author / resource version / date:
- Scope: motion fix / appearance replacement / external pack / built-in renderer / performance.
- Body type: biped / quadruped / other (describe the solver and tests).
- Features that must remain fixed: head/body ratio, facial anchors, silhouette, clothing, patterns, colors.
- Personality, voice and role boundaries:
- Backup location / baseline commit:

## Provenance and rights

- Approved designs and layered source paths:
- References, authors, permission evidence and allowed uses:
- AI tool/date, actual prompts, image references and human revisions:
- Artwork license and rights you can grant:
- Provenance entry and unresolved review questions:

Original or AI-generated is not proof of rights. Do not merely rename or recolor an existing character.

## Art and attachment review

- Approved normal / side view:
- Native facing direction of each view:
- Source size and alpha review on light/dark/checkerboard backgrounds:
- Actual `rigs`, `side-rigs` and optional `side-torsos` crops:
- Confirmation that no adjacent ears, sleeves or props enter a crop:

| Anchor | Normal view | Side view | Units / coordinate system |
| --- | --- | --- | --- |
| Skin neck root and collar | | | |
| Head bounds and aspect ratio | | | |
| Old collar exclusion | | | |
| Near/far shoulder | | | |
| Opaque sleeve shoulder and palm | | | |
| Hip / shell sockets | | | |
| Boot opening, sole and toe | | | |
| Eye, original mouth and gesture mouth | | | |
| Vessel/prop pivot | | | |

Do not mix source pixels, 384×416 render coordinates and local UVs. Label anatomical limbs separately from near/far and screen-left/right.

## Motion design

- At least 24 unique walking phases, cycle timing and frame count:
- Stance/swing ratio, ground line, lift and stride:
- Opposite arm/leg relationship, slight phase lag, arm amplitude and elbow range:
- Velocity/cadence response, stopping, turns and loop seam:
- Subtle body and neck inertia:
- Diagonal quadruped support, if applicable:
- Outermost arms/front limbs:

| States | Pose range and intent | Entry/exit to stance | Review result |
| --- | --- | --- | --- |
| idle / waiting / failed | | | |
| running / running-left / running-right | | | |
| waving / review | | | |
| jumping / punching (compatibility name) | | | |
| eating / wiping-mouth | | | |

## Expressions and file interaction

- Independent blink/mouth timing and normal/side patch alignment:
- Mouth bounds without skull or identity changes:
- Prepare → swallow → chew → raise hand → wipe → lower timing and targets:
- Confirmation that the animation does not delete originals or bypass file request confirmation:

## Fullscreen storyboard

- Theme / supported style / personality:
- Prelude, main action, return and card timing:
- Character poses, props, scenery, colors, trails and opacity:
- Shared character/prop pivot, rotation and facing:
- One-monitor, small-display and scaling bounds:
- Reduced motion / popup / countdown / Escape / snooze / fallback:
- Cache bounds and native resource cleanup:

## Delivery and approval

- Changed files, source inputs, crops, calibration and rationale:
- Build commands, environment and results:
- Focused tests, evidence and unverified cases:
- Standing, both walking directions, frame-by-frame loop and performance previews:
- Attachment/layer/alpha/clipping/proportion/expression/multi-monitor review:
- Visual reviewer and date:
- Approval to package the reviewed images:

Include rebuildable inputs. Exclude private settings, chat, secrets and unlicensed material.
