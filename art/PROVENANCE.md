# Original cultivation companion art

- User approved the project-generated cultivation design and requested a turtle-based
  spirit companion, then approved the darker charcoal turtle color scheme.
- approved-design.png preserves that approved original design board.
- rigs/orbit.png: 青衡, blue-robed wandering cultivator.
- rigs/spark.png: 绯笺, peach-robed talisman artisan.
- rigs/moss.png: 苍芷, silver-haired herbal alchemist.
- rigs/pippin.png: 小墨, charcoal-grey turtle with dark cloud-pattern shell.
- All four atlases were created with the built-in imagegen transparent edit tool
  using only this approved board as the image reference. Actual prompts accompany
  each atlas; no external character images are build inputs.
- regions.json records reviewed component extraction regions. The lower two head
  images touch in two atlases, so those regions are explicitly separated rather
  than using a blind regular-grid crop.
- tools/BuildOriginalCharacters.cs locally renders animated sleeves, opposing limbs,
  hidden knees, expressions and turtle feet from these textures. No AI calls occur
  at build/install/runtime to generate art. Claude use remains a separate optional
  conversation/task feature controlled by the user.

This provenance record does not establish exclusivity, copyright eligibility,
trademark availability, or absolute non-infringement. See ASSET-LICENSE.md.

## Walking-specific side artwork (2026-10-20)

- side-rigs/*.png were created with the built-in imagegen transparent edit tool,
  using the corresponding original rigs/*.png as the sole image reference.
- The user requested more side-facing heads and bodies and then approved using
  these new materials. Exact prompts are preserved in side-rigs/prompts.md.
- Walking uses these directional atlases; standing and interaction states retain
  the original atlases. The same local gait, elbow mesh and pinned-neck pipeline
  drives both viewpoints. Build and runtime do not call image generation.

### Side torso correction

- The first side atlases still depicted frontal collars, chest ornaments and broad
  front-facing skirts. The user reported that the bodies had not turned enough.
- side-torsos/orbit.png, spark.png and moss.png are targeted built-in imagegen edits
  of the corresponding side atlas, changing only the garment viewpoint. The original
  side atlases and approved heads, sleeves and legs are retained unchanged.
- side-torsos/prompts.md preserves the exact prompts. Reviewed crops, opaque neck
  sockets and overlapping side shoulder anchors are used without horizontal squeezing.
- The turtle already uses its side silhouette and is not regenerated in this correction.
