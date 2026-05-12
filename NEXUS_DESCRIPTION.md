# Sts2 Skin Manager — Nexus Mods listing

## Short description (one-liner)
Manage installed character skin mods and card skin mods from one in-game panel — no folder shuffling.

## Long description (paste into Nexus Mods description box)

[size=4][b]What this does[/b][/size]

Slay the Spire 2's modding scene has plenty of character skins (Mesugaki for Regent, Booba for Necrobinder, ...) and card-art / portrait reskins. If you have several installed, normally only one wins or you have to shuffle folders. Sts2 Skin Manager lets you keep them all installed and switch them on/off from the Character Select screen.

[size=4][b]Features[/b][/size]

[list]
[*] [b]Auto-detection[/b] — any skin mod whose [color=#ffcc00].pck[/color] overrides character spine paths or card art/portraits is picked up automatically. No registration, no compatibility file.
[*] [b]Character skin dropdown[/b] — pick which variant is active per character right on Character Select.
[*] [b]Card skin panel[/b] — toggle multiple card-skin packs at once, reorder priority (top wins for overlapping cards) by drag-and-drop or arrow buttons.
[*] [b]Batch Save / Discard[/b] — make a pile of changes, click Save once, restart once.
[*] [b]Auto-relaunch via Steam[/b] — confirm and the mod restarts STS2 through Steam in the background.
[*] [b]Cancel-friendly[/b] — modal cancel keeps your changes queued for next launch; Discard rolls everything back to the boot state.
[*] [b]Multi-language UI[/b] — follows the game's current language. 16 languages, English fallback.
[/list]

[size=4][b]Installation[/b][/size]

[list=1]
[*] Download the latest release zip.
[*] Extract the [color=#ffcc00]Sts2SkinManager[/color] folder into [color=#ffcc00]<Slay the Spire 2 install>/mods/[/color].
[*] First boot does a one-time self-bootstrap (rewrites load order). Restart STS2 once more and you're set.
[*] On Character Select you'll see the [color=#ffcc00]Skin [character]:[/color] dropdown and the [color=#ffcc00]Card skins[/color] panel.
[/list]

[size=4][b]Compatibility[/b][/size]

Any character skin mod that overrides paths under [color=#ffcc00]res://animations/characters/{characterId}/[/color], and any card-art / portrait mod that overrides [color=#ffcc00]card_art/...[/color] or ships [color=#ffcc00]card_portraits/[/color]. Mod authors don't need to do anything special.

[size=4][b]Limitations[/b][/size]

[list]
[*] [b]A restart is required for the visual change[/b] — STS2's spine actors don't support hot-swapping their data at runtime, so the mod auto-restarts via Steam (~5-10s).
[*] Detection patterns are limited to combat-spine paths (characters) and card art / portrait paths (cards). Icon-only mods aren't picked up.
[*] Encrypted [color=#ffcc00].pck[/color] files aren't supported.
[/list]

[size=4][b]Source code[/b][/size]

GitHub: [url=https://github.com/ing-gom/Sts2SkinManager]https://github.com/ing-gom/Sts2SkinManager[/url]

MIT licensed. Bug reports and pull requests welcome.

---

## Categories (suggest on Nexus)
- Utilities
- User Interface
- Tools for Modders

## Tags
sts2, skin manager, character skin, card skin, card portraits, mod manager, ui mod, runtime switch, steam restart

## Required mods
None. Works standalone.

## Optional dependencies
Any character skin or card skin mod you want to manage.
