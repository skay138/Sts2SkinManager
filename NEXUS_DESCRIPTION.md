# Sts2 Skin Manager — Nexus Mods listing

## Short description (one-liner)
Switch between installed character skin mods at runtime via an in-game dropdown — no folder shuffling.

## Long description (paste into Nexus Mods description box)

[size=4][b]What this does[/b][/size]

Slay the Spire 2's modding scene already has multiple character skin mods (Mesugaki for Regent, Booba for Necrobinder, etc.), but if you have several installed for the same character only one can be active. Sts2 Skin Manager solves this: install all of them, then pick which skin is active right from the Character Select screen.

[size=4][b]Features[/b][/size]

[list]
[*] [b]Auto-detection[/b] — any skin mod that ships a [color=#ffcc00]res://animations/characters/{character}/[/color] override inside its .pck is picked up automatically. No registration, no compatibility file. Works with existing mods out of the box.
[*] [b]In-game dropdown[/b] — a [color=#ffcc00]Skin [character]:[/color] dropdown appears on the Character Select screen. Click a character, pick a variant, done.
[*] [b]Native confirmation modal[/b] — uses STS2's own popup style with a 10-second countdown.
[*] [b]Steam auto-relaunch[/b] — confirm the change and the mod restarts STS2 via Steam in the background, applying the new skin in the next session.
[*] [b]Cancel-to-revert[/b] — if you change your mind, your dropdown selection rolls back so re-picking the same option re-triggers the confirmation modal.
[/list]

[size=4][b]Installation[/b][/size]

[list=1]
[*] Download the latest release ([color=#ffcc00]Sts2SkinManager-v0.1.0.zip[/color]).
[*] Extract the [color=#ffcc00]Sts2SkinManager[/color] folder into [color=#ffcc00]<Slay the Spire 2 install>/mods/[/color]. The folder should sit next to your other mods, alongside their own folders.
[*] Launch the game. The first boot does a one-time self-bootstrap (rewriting the mod load order so Sts2SkinManager loads first). You'll see a notification in the log — restart STS2 once more and the system is fully active.
[*] Go to Character Select. The [color=#ffcc00]Skin [character]:[/color] dropdown should appear in the top-left.
[/list]

[size=4][b]Compatibility[/b][/size]

Tested with:
[list]
[*] Mesugaki_Regent
[*] Booba-Necrobinder-Mod
[/list]

Any skin mod that overrides paths under [color=#ffcc00]res://animations/characters/{characterId}/[/color] inside its .pck will be detected automatically. Mod authors don't need to do anything special.

[size=4][b]Limitations[/b][/size]

[list]
[*] [b]A restart is required for the visual change.[/b] STS2's character spine actors don't support hot-swapping their data at runtime — we tried several approaches and all hit the same wall. The mod handles the restart automatically through Steam (about 5-10 seconds), so it feels close to live.
[*] [b]Detection only catches mods that override the combat spine path[/b] ([color=#ffcc00]animations/characters/{char}/[/color]). Mods that only swap portraits or icons aren't picked up. The path pattern is easy to extend in the source.
[*] [b]Encrypted .pck files aren't supported.[/b] No known mod uses encryption today.
[/list]

[size=4][b]Source code[/b][/size]

GitHub: [url=https://github.com/ing-gom/Sts2SkinManager]https://github.com/ing-gom/Sts2SkinManager[/url]

MIT licensed. Bug reports and pull requests welcome.

[size=4][b]Credits[/b][/size]

Built on top of STS2's modding infrastructure and HarmonyX. Tested with Mesugaki_Regent (Seic_Oh & Dodobird) and Booba-Necrobinder-Mod.

---

## Categories (suggest on Nexus)
- Utilities
- User Interface (for the in-game dropdown)
- Tools for Modders (for the auto-detection layer)

## Tags
sts2, skin manager, character skin, mod manager, ui mod, dropdown, runtime switch, steam restart

## Required mods
None. Works standalone.

## Optional dependencies
Any skin mod you want to manage. Sts2SkinManager only provides the switching layer — it doesn't ship any skins.
