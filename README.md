# Sts2SkinManager

A Slay the Spire 2 mod that lets you manage installed **character skin mods** and **card skin mods** (card portraits / card art) from one in-game panel on the Character Select screen.

🇰🇷 [한국어 README](README.ko.md)

![Character Select with skin dropdown, card skin panel, and restart modal](docs/screenshots/character-select.png)

## Features

- **Auto-detection** — scans `<sts2>/mods/*/` and detects:
  - **Character skin mods** — `.pck` files containing `res://animations/characters/{character}/...` paths
  - **Card skin mods** — `.pck` files overriding `card_art/...` or shipping `card_portraits/`
- **In-game UI** on Character Select:
  - **Character skin dropdown** — pick which variant is active per character
  - **Card skin panel** — toggle individual packs, reorder priority (top wins for overlapping cards), drag-and-drop or ↑/↓ arrows
- **Batch Save / Discard** — both panels share a single Save button. Make all your changes, click Save once, restart once.
- **Auto-restart via Steam** — confirm and the mod relaunches STS2 through Steam (~5-10s). Cancel and the change stays queued until next launch (Discard to fully revert).
- **Multi-language UI** — follows the game's current language. 16 languages supported; unsupported fall back to English.

## How it works

A Harmony patch on `ProjectSettings.LoadResourcePack` intercepts mod boot. The manager reads `skin_choices.json`, mounts the chosen character variant before the character actor is instantiated, and writes the chosen card-skin enable/order state to STS2's `settings.save`.

Changing your selection writes to `skin_choices.json` and pops a 10-second countdown modal. Confirm to auto-restart; cancel keeps the change queued. Discard restores everything to the state STS2 booted with.

## Install

1. Download the latest release zip.
2. Extract the `Sts2SkinManager` folder into `<Slay the Spire 2 install>/mods/`.
3. First boot does a one-time self-bootstrap (rewrites mod load order so the manager loads first). Restart STS2 once more for full activation.

## Usage

Launch STS2 → Character Select.

- **Top-left**: `Skin [<character>]:` dropdown. Click a character, pick a variant.
- **Below dropdown**: `Card skins (N/M)` panel with check, order number, ↑/↓, drag handle. Toggle, reorder by arrow or drag-and-drop.
- Make any combination of changes → click **Save** → restart modal appears → **Restart now** or **Restart later** (changes stay queued until next launch).
- **Discard** rolls everything back to the boot state.

`skin_choices.json` lives at `<user_data>/SlayTheSpire2/Sts2SkinManager/`. Direct edits are detected via file watcher and trigger the same modal.

## Limitations

- **Restart required.** STS2's character spine actors don't support hot-swapping data; auto-restart through Steam is the practical solution.
- Character detection looks at `res://animations/characters/{character}/...`. Card detection looks at `card_art/...` or `/card_portraits/`. Mods that only swap icons aren't picked up.
- First install needs one extra restart (load-order self-bootstrap).
- Encrypted `.pck` files aren't supported.

## License

MIT.
