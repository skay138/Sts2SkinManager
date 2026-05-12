# Changelog

All notable changes to Sts2SkinManager are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
