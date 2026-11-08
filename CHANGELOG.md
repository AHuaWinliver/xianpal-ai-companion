# Changelog

[简体中文](CHANGELOG.zh-CN.md)

## Unreleased · Linux candidate

- Native X11 desktop for x64 and ARM64, with a separate glibc 2.17 compatibility candidate.
- Shared scheduling, owned terminal and native Background runs, Unicode chat, workshop handoffs, tray restoration and user installation.
- Shared route and travel timing, expressive motion, optional AI route batches, trails and external-character performances are under native validation.
- Linux target-kernel, TurboVNC, display scaling and real-CLI acceptance remain required before release. See docs/LINUX.en.md for evidence and limitations.

## 0.6.12 · Companion profiles and international setup · 2026-11-08

- Add English/Simplified Chinese UI switching, preserving user text and selections.
- Move infrequent options into a separate settings window with global and per-companion Claude executable/JSON configuration.
- Let scheduled tasks choose their execution companion. Aggregate all tasks with original avatars and preserve owner, directory and prompt snapshots in historical runs.
- Use the recorded companion's configuration when reconnecting to native Background; fail clearly for a missing configured executable.
- Add a selectable installation folder and four original animation previews, retaining current-user integration and upgrade checks.
- Make public documentation and introduction graphics English-first with Chinese navigation, including character authoring and pack guides.
- Expand isolated CLI, Unicode, task ownership, Background lifecycle, installer and locale regression coverage. Tests use fixtures, not paid model calls.

## 0.6.11 · Original character detail in the hall · 2026-11-07

- Render original transparent companion frames proportionally at workshop desks, retaining facial, hair and clothing detail.
- Correct failed-state frame counts so companions remain visible.

## 0.6.10 · Default settings and isolated task history · 2026-11-06

- Group infrequent settings and persist topmost/docked opacity.
- Add global and role-specific Claude JSON settings.
- Keep scheduled output in task run history, separate from regular chat; default new tasks to background execution.

## 0.6.9 · Drag-speed gait · 2026-11-05

- Estimate velocity from adjacent motion samples and advance gait from the animation clock.
- Smooth cadence changes while preserving the 24-phase cross-limb cycle and turn phase.
- Return to stance after a drag pause, with isolated regression tests.

## 0.6.8 · Responsive workshop hall · 2026-11-05

- Reflow workstations with window size and member count, preserving proportions and readable status.
- Add a resizable hall/detail divider and correct resized hit testing and scrolling.

## 0.6.7 · Spirit-artifact travel · 2026-11-04

- Mount character-specific vessels for long accumulated routes, with smooth takeoff/landing and actual sole anchors.
- Reuse local and AI routes without extra model requests; preserve cancellation and reduced-motion behavior.

## 0.6.6 · Anchored conversation placement · 2026-11-04

- Position conversations below their companion before first display, using the current monitor's work area.
- Adjust the pair for limited space and prevent recursive repositioning.

## 0.6.5 · Installed hall background · 2026-11-04

- Deploy, verify and back up scene resources and add an embedded Windows fallback.
- Add real isolated installation/repair checks.

## 0.6.4 · Remove face editing · 2026-11-04

- Remove face-shape editing and parameter application while retaining original characters and existing data.

## 0.6.3 · Linked chat-window dragging · 2026-11-04

- Move the companion with the conversation title area, preserving text selection and monitor bounds.

## 0.6.2 · Left/right conversation bubbles · 2026-11-04

- Add explicit user/assistant turns, avatars and bounded streaming updates with Unicode and selection preservation.

## 0.6.1 · Complementary role presets · 2026-11-04

- Add planning, implementation, independent review and validation presets with shared context and bounded handoffs.
- Include optional native Claude role definitions without automatically enabling Agent Teams.

## 0.6.0 · Workshop preview · 2026-11-04

- Add planned events, editable teams, dependency tasks, visible handoffs and a workshop hall.
- Face editing introduced in this development series was later removed.

## 0.5.0 · Open preview · 2026-10-21

- Introduce four original cultivation companions, transparent animation, file interactions, scheduled CLI work and character-specific reminders.
- Prepare source, artwork provenance, licensing, build tools and isolated tests for community contributions.

Detailed historical Chinese notes remain in the linked translation. Development snapshots are not a guarantee that every listed feature appeared in an already published installer.
