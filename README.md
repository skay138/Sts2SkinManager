# Sts2SkinManager

A Slay the Spire 2 mod that lets you switch between character skin mods at runtime via an in-game dropdown. When multiple skin mods are installed for the same character, you pick which one is active — no manual file renaming, no folder shuffling.

🇰🇷 [한국어 README](README.ko.md)

## Features

- **Automatic detection** — scans `<sts2>/mods/*/` for any `.pck` containing `res://animations/characters/{character}/...` paths. No manifest convention required, works with existing skin mods (e.g. Mesugaki_Regent, Booba-Necrobinder-Mod) out of the box.
- **In-game UI** — adds a dropdown overlay on the Character Select screen. Click a character, pick a skin variant from the dropdown, done.
- **Confirmation modal with countdown** — uses STS2's native `NVerticalPopup` style. Pick "Restart now" to apply immediately, "Restart later" to keep playing.
- **Auto-restart via Steam** — when you confirm, the mod spawns a helper that waits for STS2 to exit, then relaunches it through `steam://run/2868840`. New session boots with the chosen variant pre-mounted.
- **Cancel → revert** — if you click "Restart later", your selection is rolled back so re-picking the same option triggers the modal again.

## How it works

1. **Boot interception**: a Harmony patch on `ProjectSettings.LoadResourcePack` blocks STS2 from auto-mounting any detected skin-variant `.pck`. Default state → no variant mounted → base game skin shown.
2. **Selective mount**: the manager reads `skin_choices.json` and manually mounts each character's active variant pck. By mounting before the character `SpineSprite` is instantiated, the new data is used at scene load.
3. **Live UI**: when you change your selection in the dropdown, the manager updates `skin_choices.json`, prompts a 10-second auto-restart countdown, and on confirm relaunches STS2.

## Install

1. Download the latest release (or build from source — see below).
2. Copy the `Sts2SkinManager` folder into `<Slay the Spire 2 install>/mods/`.
3. Make sure `Sts2SkinManager` is loaded **first** in your mod list. The mod will rewrite the load order itself on first boot; you may need to restart once for the self-bootstrap to take effect.

Folder layout after install:
```
<sts2>/mods/Sts2SkinManager/
  Sts2SkinManager.dll
  Sts2SkinManager.json
```

## Usage

### In-game

1. Launch STS2 and go to Character Select.
2. A dropdown labeled **Skin [<character>]:** appears in the top-left.
3. Click on a character (e.g. Regent). The dropdown updates with available variants (`default`, `Mesugaki`, etc.).
4. Open the dropdown and pick a variant.
5. A modal appears: "Skin selection changed. Auto-restart in 10 seconds via Steam."
6. Click **Restart now** to apply immediately, or **Restart later** to roll back your choice.

### JSON file (advanced)

The mod stores choices at `<user_data>/SlayTheSpire2/Sts2SkinManager/skin_choices.json`. You can edit this directly — the file watcher reacts in under a second and shows the same confirmation modal.

```json
{
  "regent": {
    "active": "Mesugaki",
    "available_variants": ["default", "Mesugaki"]
  },
  "necrobinder": {
    "active": "default",
    "available_variants": ["default", "Booba-Necrobinder-Mod"]
  }
}
```

Setting `active` to `"default"` disables all variants and shows the base game's character.

## Limitations

- **Restart required for visual change.** STS2's character spine actors don't support runtime data swap — we tried (`set_skeleton_data_res`, detach/reattach, full re-instantiation) and all hit the same wall: either the visual didn't update or the surrounding UI broke from broken script references. Auto-restart through Steam (~5-10 seconds) is the practical solution.
- **Variant detection requires the mod's `.pck` to contain `res://animations/characters/{character}/...` paths.** Skin mods that only override portraits/icons but not combat spine aren't picked up. Extending the path patterns in `SkinModScanner.cs` is trivial.
- **First boot needs a second restart** — when first installed, Sts2SkinManager rewrites the mod load order so it loads before other skin mods. The next boot fully activates interception.
- **Encrypted `.pck` files** (`PACK_ENCRYPTED` flag) cannot be parsed. Not an issue for any known mod today.

## For mod authors

If you publish a skin mod, no special manifest is needed for compatibility. Just ensure your `.pck` contains paths under `res://animations/characters/{characterId}/...` and Sts2SkinManager will pick it up.

The skin mod's `id` field (from its `.json` manifest) is what appears in the dropdown as the variant name.

## Build from source

Requires .NET 9 SDK and a local STS2 install (so the build can resolve `sts2.dll` and `0Harmony.dll`).

```bash
cd Sts2SkinManager
dotnet build -c Debug
```

The post-build step copies the DLL and manifest to `<sts2>/mods/Sts2SkinManager/`.

## Architecture

| Component | Responsibility |
|---|---|
| `Discovery/SkinModScanner.cs` | Walks `mods/` directory, parses `.pck` paths, detects character variants |
| `Discovery/PckPathReader.cs` | Extracts printable ASCII runs from `.pck` binary (used for path detection) |
| `Patches/LoadResourcePackPatch.cs` | Harmony prefix on `ProjectSettings.LoadResourcePack` — blocks auto-mount of managed pcks |
| `Patches/CharacterSelectScreenPatches.cs` | Harmony postfix on `NCharacterSelectScreen._Ready` and `SelectCharacter` — attaches overlay |
| `Runtime/ManagedPckRegistry.cs` | Thread-safe state of managed/mounted pcks + bypass flag for manual mount |
| `Runtime/RuntimeMountService.cs` | Manual mount of variant pcks via `ProjectSettings.LoadResourcePack` |
| `Runtime/LoadOrderEnforcer.cs` | Ensures Sts2SkinManager is at `mod_list[0]` via in-memory + file rewrites |
| `Runtime/SkinSelectorOverlay.cs` | The in-game `OptionButton` dropdown UI |
| `Runtime/ChoicesFileWatcher.cs` | `FileSystemWatcher` on `skin_choices.json` + revert-on-cancel logic |
| `Runtime/RestartCountdownModal.cs` | STS2 native `NVerticalPopup` with countdown text + Yes/No callbacks |
| `Runtime/RestartHelper.cs` | Spawns a `.bat` helper that waits for STS2 to exit, then opens `steam://run/2868840` |
| `Config/SkinChoicesConfig.cs` | Reads/writes `skin_choices.json` |
| `Config/Sts2SettingsWriter.cs` | Reads/writes STS2's `settings.save` for load-order enforcement |

## License

MIT.

## Acknowledgements

- Tested with [Mesugaki_Regent](https://example) by Seic_Oh and Dodobird, and Booba-Necrobinder-Mod.
- Built on top of [STS2 modding](https://www.megacrit.com/) infrastructure and [HarmonyX](https://github.com/BepInEx/HarmonyX).
