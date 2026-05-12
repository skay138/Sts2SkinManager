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

        foreach (var (character, variants) in _byCharacter)
        {
            if (!fresh.Characters.TryGetValue(character, out var choice)) continue;
            var newActive = choice.Active ?? "default";
            var prevActive = _appliedActive.TryGetValue(character, out var p) ? p : "default";
            if (string.Equals(newActive, prevActive, StringComparison.OrdinalIgnoreCase)) continue;

            anyChange = true;
            MainFile.Logger.Info($"character change: {character} '{prevActive}' → '{newActive}'");
            _appliedActive[character] = newActive;
        }

        // Unified Save pattern: changes are explicit (user pressed Save), so modal cancel
        // does NOT revert the change — it just means "don't restart right now".
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
            RestartCountdownModal.ShowOrReset(_managerDataDir, 10, () => { /* unified Save pattern: no revert */ });
            SkinSelectorOverlay.RefreshDropdown();
            SkinSelectorOverlay.RefreshCardPacks();
        }
    }

    // Called by SkinSelectorOverlay.OnDiscard after it has rewritten choices.json to the boot
    // snapshot. This keeps _applied* aligned with the new disk content so the upcoming watcher
    // fire sees "no change" and doesn't pop a redundant restart modal.
    public void NoteSavedAsApplied()
    {
        try
        {
            var fresh = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            _appliedActive.Clear();
            foreach (var kv in fresh.Characters) _appliedActive[kv.Key] = kv.Value.Active ?? "default";
            _appliedCardPacks = Clone(fresh.CardPacks);
            _lastFireUtc = DateTime.UtcNow.AddMilliseconds(500);
        }
        catch (Exception ex) { MainFile.Logger.Warn($"NoteSavedAsApplied error: {ex.Message}"); }
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
