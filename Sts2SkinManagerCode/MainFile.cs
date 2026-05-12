using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using Sts2SkinManager.Config;
using Sts2SkinManager.Discovery;
using Sts2SkinManager.Runtime;

namespace Sts2SkinManager;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "Sts2SkinManager";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; }
        = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        try
        {
            ApplyHarmonyPatches();
            Run();
        }
        catch (Exception ex)
        {
            Logger.Warn($"init failed: {ex}");
        }
    }

    private static void ApplyHarmonyPatches()
    {
        var harmony = new Harmony(ModId);
        harmony.PatchAll(typeof(MainFile).Assembly);
        Logger.Info("Harmony patches applied.");
    }

    private static void Run()
    {
        var executablePath = OS.GetExecutablePath();
        var gameDir = Path.GetDirectoryName(executablePath)!;
        var modsDir = Path.Combine(gameDir, "mods");
        var userDataDir = OS.GetUserDataDir();
        var managerDataDir = Path.Combine(userDataDir, ModId);
        Directory.CreateDirectory(managerDataDir);

        var detected = SkinModScanner.Scan(modsDir);
        var characterMods = detected.Where(d => d.Kind == SkinModKind.Character).ToList();
        var cardMods = detected.Where(d => d.Kind == SkinModKind.Cards).ToList();

        Logger.Info($"detected {characterMods.Count} character skin pck(s), {cardMods.Count} card pack pck(s):");
        foreach (var d in characterMods)
        {
            ManagedPckRegistry.Manage(d.PckPath);
            Logger.Info($"  [char] {d.ModId} → [{string.Join(",", d.Characters)}]");
        }
        foreach (var d in cardMods)
        {
            Logger.Info($"  [cards] {d.ModId}");
        }

        var byCharacter = new Dictionary<string, List<DetectedSkinMod>>();
        foreach (var d in characterMods)
        {
            foreach (var c in d.Characters)
            {
                if (!byCharacter.TryGetValue(c, out var list))
                {
                    list = new List<DetectedSkinMod>();
                    byCharacter[c] = list;
                }
                list.Add(d);
            }
        }

        var settings = Sts2SettingsWriter.FindAndLoad(userDataDir);
        var fileReordered = settings != null && LoadOrderEnforcer.EnsureFirstInModList(settings, ModId);
        if (fileReordered && settings != null) Sts2SettingsWriter.Save(settings);

        var memoryReordered = LoadOrderEnforcer.EnsureFirstInMods(ModId);
        if (fileReordered || memoryReordered)
        {
            Logger.Warn($"self-bootstrap: file_reorder={fileReordered} memory_reorder={memoryReordered}. " +
                        "*** RESTART STS2 ONCE *** for full activation.");
        }

        var choicesPath = Path.Combine(managerDataDir, "skin_choices.json");
        var choices = SkinChoicesConfig.LoadOrEmpty(choicesPath);
        foreach (var (character, variants) in byCharacter)
        {
            choices.SyncAvailableVariants(character, variants.Select(v => v.ModId));
        }
        choices.SyncCardPacks(cardMods.Select(c => c.ModId));
        choices.Save(choicesPath);
        Logger.Info($"skin_choices.json → {choicesPath}");
        Logger.Info($"card pack state: ordering=[{string.Join(", ", choices.CardPacks.Ordering)}], enabled={{ {string.Join(", ", choices.CardPacks.Enabled.Select(kv => $"{kv.Key}={kv.Value}"))} }}");

        foreach (var (character, variants) in byCharacter)
        {
            if (!choices.Characters.TryGetValue(character, out var choice)) continue;
            if (string.Equals(choice.Active, "default", StringComparison.OrdinalIgnoreCase)) continue;

            var variant = variants.FirstOrDefault(v => string.Equals(v.ModId, choice.Active, StringComparison.OrdinalIgnoreCase));
            if (variant == null)
            {
                Logger.Warn($"choices.json says character '{character}' active='{choice.Active}' but no such variant found.");
                continue;
            }
            RuntimeMountService.MountVariantPck(variant.PckPath);
        }

        if (settings != null && cardMods.Count > 0)
        {
            var settingsChanged = CardPackApplier.ApplyToSettings(settings, choices.CardPacks, cardMods);
            var memoryChanged = CardPackApplier.ApplyToMemoryModList(choices.CardPacks);
            if (settingsChanged)
            {
                Sts2SettingsWriter.Save(settings);
                Logger.Info($"card pack settings.save updated (mem={memoryChanged}); takes full effect on next restart");
            }
        }

        _watcher = new ChoicesFileWatcher(choicesPath, managerDataDir, byCharacter, cardMods, choices);
        _watcher.Start();

        SkinSelectorOverlay.Configure(choicesPath, byCharacter, cardMods);
        SkinSelectorOverlay.SetWatcher(_watcher);
    }

    private static ChoicesFileWatcher? _watcher;
}
