using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sts2SkinManager.Config;
using Sts2SkinManager.Discovery;

namespace Sts2SkinManager.Runtime;

public sealed class ChoicesFileWatcher : IDisposable
{
    private readonly string _choicesPath;
    private readonly string _managerDataDir;
    private readonly IReadOnlyDictionary<string, List<DetectedSkinMod>> _byCharacter;
    private readonly List<DetectedSkinMod> _cardMods;
    private FileSystemWatcher? _watcher;
    private DateTime _lastFireUtc = DateTime.MinValue;

    private readonly Dictionary<string, string> _appliedActive = new(StringComparer.OrdinalIgnoreCase);
    private CardPacksConfig _appliedCardPacks;

    public ChoicesFileWatcher(
        string choicesPath,
        string managerDataDir,
        IReadOnlyDictionary<string, List<DetectedSkinMod>> byCharacter,
        List<DetectedSkinMod> cardMods,
        SkinChoicesConfig initial)
    {
        _choicesPath = choicesPath;
        _managerDataDir = managerDataDir;
        _byCharacter = byCharacter;
        _cardMods = cardMods;
        foreach (var (c, choice) in initial.Characters) _appliedActive[c] = choice.Active;
        _appliedCardPacks = Clone(initial.CardPacks);
    }

    public void Start()
    {
        var dir = Path.GetDirectoryName(_choicesPath);
        var name = Path.GetFileName(_choicesPath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) return;

        _watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        MainFile.Logger.Info($"watching {_choicesPath} for live skin changes");
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastFireUtc).TotalMilliseconds < 300) return;
        _lastFireUtc = now;

        try
        {
            System.Threading.Thread.Sleep(80);
            ApplyFromDisk();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"choices watcher error: {ex.Message}");
        }
    }

    private void ApplyFromDisk()
    {
        var fresh = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
        var anyChange = false;
        var revertActive = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (character, variants) in _byCharacter)
        {
            if (!fresh.Characters.TryGetValue(character, out var choice)) continue;
            var newActive = choice.Active ?? "default";
            var prevActive = _appliedActive.TryGetValue(character, out var p) ? p : "default";
            if (string.Equals(newActive, prevActive, StringComparison.OrdinalIgnoreCase)) continue;

            anyChange = true;
            MainFile.Logger.Info($"character change: {character} '{prevActive}' → '{newActive}'");
            revertActive[character] = prevActive;
            _appliedActive[character] = newActive;
        }

        var revertCardPacks = Clone(_appliedCardPacks);
        var cardPacksChanged = !CardPacksEqual(fresh.CardPacks, _appliedCardPacks);
        if (cardPacksChanged)
        {
            anyChange = true;
            MainFile.Logger.Info($"card packs changed:");
            foreach (var kv in fresh.CardPacks.Enabled)
            {
                var was = _appliedCardPacks.Enabled.TryGetValue(kv.Key, out var w) ? w : true;
                if (was != kv.Value) MainFile.Logger.Info($"  {kv.Key}: enabled {was} → {kv.Value}");
            }
            if (!fresh.CardPacks.Ordering.SequenceEqual(_appliedCardPacks.Ordering, StringComparer.OrdinalIgnoreCase))
            {
                MainFile.Logger.Info($"  ordering: [{string.Join(",", _appliedCardPacks.Ordering)}] → [{string.Join(",", fresh.CardPacks.Ordering)}]");
            }
            _appliedCardPacks = Clone(fresh.CardPacks);

            var userDataDir = Godot.OS.GetUserDataDir();
            var settings = Sts2SettingsWriter.FindAndLoad(userDataDir);
            if (settings != null)
            {
                var settingsChanged = CardPackApplier.ApplyToSettings(settings, fresh.CardPacks, _cardMods);
                var memChanged = CardPackApplier.ApplyToMemoryModList(fresh.CardPacks);
                if (settingsChanged) Sts2SettingsWriter.Save(settings);
                MainFile.Logger.Info($"card pack applied to settings: file={settingsChanged} mem={memChanged}");
            }
        }

        if (anyChange)
        {
            MainFile.Logger.Info($"showing 10s restart countdown modal");
            RestartCountdownModal.ShowOrReset(_managerDataDir, 10, () => RevertSnapshot(revertActive, revertCardPacks));
        }
    }

    private void RevertSnapshot(Dictionary<string, string> revertActive, CardPacksConfig revertCardPacks)
    {
        try
        {
            foreach (var kv in revertActive) _appliedActive[kv.Key] = kv.Value;
            _appliedCardPacks = Clone(revertCardPacks);

            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            foreach (var kv in revertActive)
            {
                if (choices.Characters.TryGetValue(kv.Key, out var c))
                {
                    MainFile.Logger.Info($"revert character: {kv.Key} → '{kv.Value}'");
                    c.Active = kv.Value;
                }
            }
            choices.CardPacks = Clone(revertCardPacks);
            choices.Save(_choicesPath);

            var userDataDir = Godot.OS.GetUserDataDir();
            var settings = Sts2SettingsWriter.FindAndLoad(userDataDir);
            if (settings != null)
            {
                CardPackApplier.ApplyToSettings(settings, revertCardPacks, _cardMods);
                CardPackApplier.ApplyToMemoryModList(revertCardPacks);
                Sts2SettingsWriter.Save(settings);
                MainFile.Logger.Info($"revert card packs applied to settings");
            }

            SkinSelectorOverlay.RefreshDropdown();
            SkinSelectorOverlay.RefreshCardPacks();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"RevertSnapshot error: {ex}");
        }
    }

    private static CardPacksConfig Clone(CardPacksConfig src) => new()
    {
        Ordering = new List<string>(src.Ordering),
        Enabled = new Dictionary<string, bool>(src.Enabled, StringComparer.OrdinalIgnoreCase),
    };

    private static bool CardPacksEqual(CardPacksConfig a, CardPacksConfig b)
    {
        if (!a.Ordering.SequenceEqual(b.Ordering, StringComparer.OrdinalIgnoreCase)) return false;
        if (a.Enabled.Count != b.Enabled.Count) return false;
        foreach (var kv in a.Enabled)
        {
            if (!b.Enabled.TryGetValue(kv.Key, out var bv) || bv != kv.Value) return false;
        }
        return true;
    }

    public Dictionary<string, string> AppliedActive => _appliedActive;

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
