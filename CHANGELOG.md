# Changelog

All notable changes to Sts2SkinManager are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.10.0] - 2026-05-15

### Added
- **Recursive `mods/` scan.** Skin Manager's pck discovery now walks the entire `mods/` tree at any depth, so users can organize pcks under category folders like `mods/Characters/`, `mods/Artwork/`, `mods/Utility/` and they'll still be picked up. Hidden / `__MACOSX` / dotted folders are skipped. Duplicate pck filenames across subfolders log a warning and the first occurrence wins.

### Documentation
- README EN/KO clarify that STS2 itself also walks `mods/` recursively (`ModManager.ReadModsInDirRecursive`), so DLL+manifest+pck bundles work at any depth — **delete the original `mods/<modName>/` folder after relocating**, otherwise the framework discovers both copies and reports `DUPLICATE_ID`.

### Internal
- Auto-junction prototype (PR #2/#3) was reverted in PR #4 once `sts2.dll` decompilation confirmed the framework already handles nested mod folders natively. No runtime shim is needed.

## [0.9.0] - 2026-05-14

### Added
- **Configurable overlay anchor.** The character-select overlay can now be docked to either the **top-left** (original layout) or the **top-right** corner. Switch live via the optional [ModConfig (Nexus #27)](https://www.nexusmods.com/slaythespire2/mods/27) dropdown — *Overlay position (character select)*. Without ModConfig installed, the mod silently uses the default.
- Overlay layout is now **anchor-based** instead of absolute pixel coordinates — stays correctly placed across resolutions and aspect ratios. `AnchorTopLeft` / `AnchorTopRight` helpers in `SkinSelectorOverlay`; `ModConfigBridge.cs` mirrors the reflection-based pattern used by Sts2ShopVarianceTuner (zero hard dependency on ModConfig).

### Changed
- **Overlay default position is now Top Right** (was Top Left in v0.8 and earlier). The change avoids collision with the multiplayer lobby panel and other UI the game itself parks in the top-left corner of Character Select. Users who prefer the original layout can flip it back to Top Left via the ModConfig dropdown — the change applies immediately, no restart required.

## [0.8.0] - 2026-05-13

### Added
- **Modpack preset sharing.** Every Save now mirrors the current selection to `<sts2>/mods/Sts2SkinManager/modpack_preset.json`. To share a full modpack with a friend, zip your `mods/` folder and send it — on first launch their `skin_choices.json` is seeded from the bundled preset, so dropdown picks, card-skin order, and mixed-mod toggles all apply automatically without anyone having to touch the Roaming/AppData folder.
- README "Sharing your setup" section in both EN and KO with a warning that Nexus release zips must not contain `modpack_preset.json`.

### Notes
- Preset seeding only happens when `<user_data>/Sts2SkinManager/skin_choices.json` doesn't exist yet. Existing users with saved choices are unaffected.
- If a preset references a mod that isn't installed on the recipient's machine, that selection falls back to `default` via the existing `SyncAvailableVariants` path. The recipient's `Save` then mirrors the fallback back into the preset, so re-sharing carries the corrected state forward.

## [0.7.0] - 2026-05-13

### Added
- **DLL load blocking for non-active character mods.** Second Harmony patch on `ModManager.TryLoadMod` intercepts and skips DLL load for any character skin mod that isn't the currently selected variant (or an enabled mixed mod). Without this, mods like `Booba-Necrobinder-Mod` would register Harmony patches that force-override `CharacterModel.CreateVisuals` (scale `0.12`, position `(40, -250)`, skeleton swap) on every instance of the base character — even when you'd selected a completely different skin. Block list rebuilds every boot from `skin_choices.json`.
- **Restart modal on first-boot self-bootstrap.** When `LoadOrderEnforcer` reorders `settings.save` to put SkinManager first in the mod list, a 10-second countdown modal now appears (previously only a `Logger.Warn` line). Without restart, this session's character mods that loaded before SkinManager still have their Harmony patches live; the restart is what makes blocking actually take effect.
- 16-language coverage for the new `load_order_modal_title` / `load_order_modal_body` strings.

### Changed
- `RestartCountdownModal.ShowOrReset` accepts optional `titleKey` / `bodyKey` parameters so the same modal infrastructure can carry different copy for the load-order vs. skin-change cases.

## [0.6.0] - 2026-05-13

### Added
- **Mixed-mod awareness.** Mods that bundle a character spine with card art / event scenes (e.g. AncientWaifus) are now detected as mixed. They appear in the character dropdown with a `📦` indicator (selecting one applies spine + extras together) AND in a new "Mixed mods" panel, where you can toggle them independently to layer their extras on top of a different main-spine pick (the dropdown's character pick always wins spine conflicts).
- **Boot mount priority.** Mixed addons are mounted first (in reverse-priority order); the dropdown's main-spine choice is mounted last so it overrides any conflicting paths.
- **Collapsible Skin Manager section.** The whole UI is now wrapped in a single toggle header (`▶ 스킨 매니저`) — collapsed by default to keep Character Select clean. Save / Discard stay visible alongside the toggle no matter what state the body is in.
- **Tabbed inner layout.** Card-skin and mixed-mod sections live as tabs inside one panel — only one row list shows at a time, with a generous content area when expanded.
- 16-language coverage for every new key (mixed panel + skin manager header).

### Changed
- Dropdown item text for mixed mods is prefixed with `📦` and ships an explicit per-item tooltip (`SetItemTooltip`) so popup hover surfaces the explanation reliably.
- Inline help label inside the Mixed mod tab so the explanation is always visible without depending on hover tooltips.

## [0.5.0] - 2026-05-12

### Added
- **Per-mod display aliases.** Rename any character variant or card skin from the in-game panel — click ✏️, type a friendly name, ✓ to save (Enter also saves) or ✕ to cancel. The mod ID stays as the unique key; aliases are cosmetic and never break matching.
- **Alias uniqueness validation.** Names that collide with another mod ID or another alias are rejected inline (red highlight + tooltip explaining the conflict).

### Fixed
- **Custom-character mods are no longer blocked.** Mods that add brand-new characters (e.g. Ryoshu) had their `.pck` auto-mount intercepted because they shared the `animations/characters/{char}/` path pattern with skin mods, causing the character to disappear from Character Select. SkinManager now reads the base-game roster from `SlayTheSpire2.pck` and only manages mods that target a base character; custom-character mods stay auto-mountable.
- The ✏️ rename button stayed enabled after switching to a character that had no detected skin variants.

## [0.4.0] - 2026-05-12

### Added
- Skin preview on hover. Character skins: hover the 👁 icon beside the dropdown. Card skins: hover the row label. Sources: `{ModFolder}/preview.png|jpg|jpeg|webp` (author convention) or auto-extracted from the `.pck` (character-select art for characters, first card art for card packs). 120 ms hide-debounce to handle cursor traversal.
- Godot 4 PCK v2/v3 parser and `.ctex` WebP/PNG decoder so previews work without mounting the `.pck`.

## [0.3.0] - 2026-05-12

### Added
- Unified Save / Discard pattern: character dropdown and card-skin panel share a single Save button; one click, one modal, one restart.
- Drag-and-drop reordering for card skins with on/off ✓/✗ status icons.

### Changed
- "Card packs" naming → "Card skins" across all 16 languages.

## [0.2.1] - 2026-05-12

### Changed
- Card-pack panel UX polish: `ScrollContainer`, collapsible header, explicit order numbers, top-wins priority order with one-shot schema migration.

## [0.2.0] - 2026-05-12

### Added
- Card pack management: auto-detection of card-skin mods, JSON schema for ordering/enabled state, in-game UI panel, and integration with STS2's `settings.save` mod list.

## [0.1.3] - 2026-05-12

### Fixed
- Overlay was lost when the user changed STS2 language at runtime; now re-attached via `LocManager.SubscribeToLocaleChange`.

## [0.1.2] - 2026-05-12

### Changed
- Modal body wording and dropdown placeholder strings clarified across all locales.

## [0.1.1] - 2026-05-12

### Added
- i18n support — UI follows STS2's current language. 16 locales bundled; missing keys fall back to English.

## [0.1.0] - 2026-05-12

### Added
- Initial public release. Detects character-skin `.pck` mods, blocks their auto-mount, and mounts the chosen variant per character via an in-game dropdown on Character Select. Restart confirmation modal with auto-restart through Steam.
