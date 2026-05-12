using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Sts2SkinManager.Config;
using Sts2SkinManager.Discovery;

namespace Sts2SkinManager.Runtime;

public static class SkinSelectorOverlay
{
    private static string _choicesPath = "";
    private static Dictionary<string, List<DetectedSkinMod>>? _byCharacter;
    private static OptionButton? _opt;
    private static Label? _label;
    private static string _currentCharacter = "";
    private static bool _suppressNextItemSelected;

    public static void Configure(string choicesPath, Dictionary<string, List<DetectedSkinMod>> byCharacter)
    {
        _choicesPath = choicesPath;
        _byCharacter = byCharacter;
    }

    public static void Attach(Node screen)
    {
        Callable.From(() => DoAttach(screen)).CallDeferred();
    }

    private static void DoAttach(Node screen)
    {
        try
        {
            if (_opt != null && GodotObject.IsInstanceValid(_opt)) return;

            var hbox = new HBoxContainer
            {
                Position = new Vector2(40, 40),
                CustomMinimumSize = new Vector2(420, 56),
            };
            _label = new Label
            {
                Text = "Skin:",
                CustomMinimumSize = new Vector2(80, 56),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _opt = new OptionButton
            {
                CustomMinimumSize = new Vector2(320, 56),
            };
            _opt.ItemSelected += OnVariantSelected;
            _opt.Connect("item_selected", Callable.From<long>(idx => OnVariantSelectedSafe(idx)));
            _opt.Pressed += () => MainFile.Logger.Info("OptionButton pressed (dropdown opened)");
            hbox.AddChild(_label);
            hbox.AddChild(_opt);
            hbox.ZIndex = 1000;
            screen.AddChild(hbox);
            MainFile.Logger.Info($"SkinSelectorOverlay attached (OptionButton) to {screen.Name}");
            RefreshItems();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"overlay attach failed: {ex.Message}");
        }
    }

    public static void OnCharacterSelected(string characterId)
    {
        _currentCharacter = (characterId ?? "").ToLowerInvariant();
        Callable.From(RefreshItems).CallDeferred();
    }

    public static void RefreshDropdown()
    {
        Callable.From(RefreshItems).CallDeferred();
    }

    private static void RefreshItems()
    {
        if (_opt == null || !GodotObject.IsInstanceValid(_opt)) return;
        _suppressNextItemSelected = true;
        try
        {
            _opt.Clear();
            if (_label != null) _label.Text = $"Skin [{(string.IsNullOrEmpty(_currentCharacter) ? "—" : _currentCharacter)}]:";

            if (_byCharacter == null || !_byCharacter.TryGetValue(_currentCharacter, out var variants) || variants.Count == 0)
            {
                _opt.AddItem("(no variants)");
                _opt.Disabled = true;
                return;
            }
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            if (!choices.Characters.TryGetValue(_currentCharacter, out var c))
            {
                _opt.AddItem("(not configured)");
                _opt.Disabled = true;
                return;
            }
            _opt.Disabled = false;
            for (var i = 0; i < c.AvailableVariants.Count; i++)
            {
                var v = c.AvailableVariants[i];
                _opt.AddItem(v, i);
                if (string.Equals(v, c.Active, StringComparison.OrdinalIgnoreCase))
                {
                    _opt.Selected = i;
                }
            }
            MainFile.Logger.Info($"OptionButton populated for '{_currentCharacter}': {c.AvailableVariants.Count} items, active='{c.Active}'");
        }
        finally
        {
            _suppressNextItemSelected = false;
        }
    }

    private static bool _alreadyHandledThisEvent;

    private static void OnVariantSelected(long index)
    {
        MainFile.Logger.Info($"OptionButton.ItemSelected event fired: index={index}, suppress={_suppressNextItemSelected}");
        HandleSelection(index);
    }

    private static void OnVariantSelectedSafe(long index)
    {
        if (_alreadyHandledThisEvent) { _alreadyHandledThisEvent = false; return; }
        MainFile.Logger.Info($"OptionButton Connect callback: index={index}");
        HandleSelection(index);
    }

    private static void HandleSelection(long index)
    {
        if (_suppressNextItemSelected)
        {
            MainFile.Logger.Info($"  suppressed (programmatic update)");
            return;
        }
        _alreadyHandledThisEvent = true;
        try
        {
            if (_opt == null) return;
            var chosen = _opt.GetItemText((int)index);
            MainFile.Logger.Info($"  chosen='{chosen}' for character='{_currentCharacter}'");
            if (string.IsNullOrEmpty(chosen) || chosen.StartsWith("("))
            {
                MainFile.Logger.Info("  ignoring placeholder/empty option");
                return;
            }

            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            if (!choices.Characters.TryGetValue(_currentCharacter, out var c))
            {
                MainFile.Logger.Warn($"  no choice entry for '{_currentCharacter}'");
                return;
            }
            if (string.Equals(c.Active, chosen, StringComparison.OrdinalIgnoreCase))
            {
                MainFile.Logger.Info($"  already active, skipping write");
                return;
            }

            c.Active = chosen;
            choices.Save(_choicesPath);
            MainFile.Logger.Info($"overlay select: {_currentCharacter} → {chosen} (saved; watcher will trigger modal)");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"select error: {ex.Message}");
        }
    }
}
