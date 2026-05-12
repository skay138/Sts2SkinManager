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
    private FileSystemWatcher? _watcher;
    private DateTime _lastFireUtc = DateTime.MinValue;

    private readonly Dictionary<string, string> _appliedActive = new(StringComparer.OrdinalIgnoreCase);

    public ChoicesFileWatcher(string choicesPath, string managerDataDir, IReadOnlyDictionary<string, List<DetectedSkinMod>> byCharacter, SkinChoicesConfig initial)
    {
        _choicesPath = choicesPath;
        _managerDataDir = managerDataDir;
        _byCharacter = byCharacter;
        foreach (var (c, choice) in initial.Characters) _appliedActive[c] = choice.Active;
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
        var revertSnapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (character, variants) in _byCharacter)
        {
            if (!fresh.Characters.TryGetValue(character, out var choice)) continue;
            var newActive = choice.Active ?? "default";
            var prevActive = _appliedActive.TryGetValue(character, out var p) ? p : "default";
            if (string.Equals(newActive, prevActive, StringComparison.OrdinalIgnoreCase)) continue;

            anyChange = true;
            MainFile.Logger.Info($"choices change: {character} '{prevActive}' → '{newActive}'");
            revertSnapshot[character] = prevActive;
            _appliedActive[character] = newActive;
        }

        if (anyChange)
        {
            MainFile.Logger.Info($"showing 10s restart countdown modal (with revert-on-cancel snapshot)");
            RestartCountdownModal.ShowOrReset(_managerDataDir, 10, () => RevertSnapshot(revertSnapshot));
        }
    }

    private void RevertSnapshot(Dictionary<string, string> snapshot)
    {
        try
        {
            foreach (var kv in snapshot)
            {
                _appliedActive[kv.Key] = kv.Value;
            }
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            foreach (var kv in snapshot)
            {
                if (choices.Characters.TryGetValue(kv.Key, out var c))
                {
                    MainFile.Logger.Info($"revert: {kv.Key} → '{kv.Value}'");
                    c.Active = kv.Value;
                }
            }
            choices.Save(_choicesPath);
            SkinSelectorOverlay.RefreshDropdown();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"RevertSnapshot error: {ex}");
        }
    }

    public Dictionary<string, string> AppliedActive => _appliedActive;

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
