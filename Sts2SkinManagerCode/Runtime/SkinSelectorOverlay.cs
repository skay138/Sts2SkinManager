using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using Sts2SkinManager.Config;
using Sts2SkinManager.Discovery;
using Sts2SkinManager.Localization;

namespace Sts2SkinManager.Runtime;

public static class SkinSelectorOverlay
{
    private static string _choicesPath = "";
    private static Dictionary<string, List<DetectedSkinMod>>? _byCharacter;
    private static List<DetectedSkinMod> _cardMods = new();

    private static OptionButton? _opt;
    private static Label? _label;
    private static Control? _hbox;
    private static VBoxContainer? _cardPackVBox;
    private static Button? _cardPackHeaderBtn;
    private static ScrollContainer? _cardPackScroll;
    private static VBoxContainer? _cardPackRows;
    private static bool _cardPackExpanded = true;

    private static Node? _lastScreen;
    private static string _currentCharacter = "";
    private static bool _suppressNextItemSelected;
    private static bool _localeChangeSubscribed;
    private static bool _alreadyHandledThisEvent;

    public static void Configure(string choicesPath, Dictionary<string, List<DetectedSkinMod>> byCharacter, List<DetectedSkinMod> cardMods)
    {
        _choicesPath = choicesPath;
        _byCharacter = byCharacter;
        _cardMods = cardMods;
    }

    public static void Attach(Node screen)
    {
        Callable.From(() => DoAttach(screen)).CallDeferred();
    }

    private static void DoAttach(Node screen)
    {
        try
        {
            if (_opt != null && GodotObject.IsInstanceValid(_opt) && _opt.IsInsideTree())
            {
                MainFile.Logger.Info("overlay already attached and in tree; skipping re-attach");
                return;
            }
            _lastScreen = screen;

            var hbox = new HBoxContainer
            {
                Position = new Vector2(40, 40),
                CustomMinimumSize = new Vector2(420, 56),
            };
            _hbox = hbox;
            _label = new Label
            {
                Text = Strings.Get("skin_label") + ":",
                CustomMinimumSize = new Vector2(80, 56),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _opt = new OptionButton { CustomMinimumSize = new Vector2(320, 56) };
            _opt.ItemSelected += OnVariantSelected;
            _opt.Connect("item_selected", Callable.From<long>(idx => OnVariantSelectedSafe(idx)));
            _opt.Pressed += () => MainFile.Logger.Info("OptionButton pressed (dropdown opened)");
            hbox.AddChild(_label);
            hbox.AddChild(_opt);
            hbox.ZIndex = 1000;
            screen.AddChild(hbox);
            MainFile.Logger.Info($"SkinSelectorOverlay attached (OptionButton) to {screen.Name}");
            RefreshItems();

            if (_cardMods.Count > 0) BuildCardPackPanel(screen);

            EnsureLocaleSubscribed();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"overlay attach failed: {ex.Message}");
        }
    }

    private static void BuildCardPackPanel(Node screen)
    {
        var vbox = new VBoxContainer
        {
            Position = new Vector2(40, 110),
            CustomMinimumSize = new Vector2(480, 40),
        };
        _cardPackVBox = vbox;

        var headerBtn = new Button
        {
            CustomMinimumSize = new Vector2(480, 32),
            Alignment = HorizontalAlignment.Left,
        };
        _cardPackHeaderBtn = headerBtn;
        headerBtn.Pressed += ToggleCardPackExpanded;
        vbox.AddChild(headerBtn);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(480, 200),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        _cardPackScroll = scroll;
        vbox.AddChild(scroll);

        var rows = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(460, 0),
        };
        _cardPackRows = rows;
        scroll.AddChild(rows);

        BuildCardPackRows();
        UpdateCardPackHeader();
        ApplyCardPackExpanded();

        vbox.ZIndex = 1000;
        screen.AddChild(vbox);
        MainFile.Logger.Info($"card pack panel attached ({_cardMods.Count} packs)");
    }

    private static void ToggleCardPackExpanded()
    {
        _cardPackExpanded = !_cardPackExpanded;
        ApplyCardPackExpanded();
        UpdateCardPackHeader();
        MainFile.Logger.Info($"card pack panel {(_cardPackExpanded ? "expanded" : "collapsed")}");
    }

    private static void ApplyCardPackExpanded()
    {
        if (_cardPackScroll != null && GodotObject.IsInstanceValid(_cardPackScroll))
        {
            _cardPackScroll.Visible = _cardPackExpanded;
        }
    }

    private static void UpdateCardPackHeader()
    {
        if (_cardPackHeaderBtn == null || !GodotObject.IsInstanceValid(_cardPackHeaderBtn)) return;
        var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
        var total = choices.CardPacks.Ordering.Count;
        var enabled = choices.CardPacks.Enabled.Count(kv => kv.Value);
        var arrow = _cardPackExpanded ? "▼" : "▶";
        _cardPackHeaderBtn.Text = $"{arrow}  {Strings.Get("card_packs_header")} ({enabled}/{total})";
    }

    private static void BuildCardPackRows()
    {
        if (_cardPackRows == null || !GodotObject.IsInstanceValid(_cardPackRows)) return;

        for (var i = _cardPackRows.GetChildCount() - 1; i >= 0; i--)
        {
            var child = _cardPackRows.GetChild(i);
            _cardPackRows.RemoveChild(child);
            child.QueueFree();
        }

        var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
        for (var i = 0; i < choices.CardPacks.Ordering.Count; i++)
        {
            var modId = choices.CardPacks.Ordering[i];
            var row = BuildCardPackRow(modId, choices.CardPacks, i, choices.CardPacks.Ordering.Count);
            _cardPackRows.AddChild(row);
        }
        UpdateCardPackHeader();
    }

    private static Control BuildCardPackRow(string modId, CardPacksConfig packs, int index, int total)
    {
        var hbox = new HBoxContainer();
        var enabled = packs.Enabled.TryGetValue(modId, out var e) ? e : true;

        var check = new CheckBox
        {
            ButtonPressed = enabled,
            CustomMinimumSize = new Vector2(32, 32),
        };
        check.Toggled += isOn => OnCardPackToggle(modId, isOn);
        hbox.AddChild(check);

        var orderLabel = new Label
        {
            Text = $"{index + 1}.",
            CustomMinimumSize = new Vector2(32, 32),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        hbox.AddChild(orderLabel);

        var label = new Label
        {
            Text = modId,
            CustomMinimumSize = new Vector2(308, 32),
            VerticalAlignment = VerticalAlignment.Center,
        };
        hbox.AddChild(label);

        var upBtn = new Button
        {
            Text = "↑",
            CustomMinimumSize = new Vector2(32, 32),
            Disabled = index == 0,
        };
        upBtn.Pressed += () => MoveCardPack(modId, -1);
        hbox.AddChild(upBtn);

        var downBtn = new Button
        {
            Text = "↓",
            CustomMinimumSize = new Vector2(32, 32),
            Disabled = index == total - 1,
        };
        downBtn.Pressed += () => MoveCardPack(modId, +1);
        hbox.AddChild(downBtn);

        return hbox;
    }

    private static void OnCardPackToggle(string modId, bool isOn)
    {
        try
        {
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            var current = choices.CardPacks.Enabled.TryGetValue(modId, out var c) ? c : true;
            if (current == isOn) return;
            choices.CardPacks.Enabled[modId] = isOn;
            choices.Save(_choicesPath);
            MainFile.Logger.Info($"card pack toggle: {modId} → {isOn}");
        }
        catch (Exception ex) { MainFile.Logger.Warn($"card pack toggle error: {ex.Message}"); }
    }

    private static void MoveCardPack(string modId, int delta)
    {
        try
        {
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            var idx = choices.CardPacks.Ordering.IndexOf(modId);
            if (idx < 0) return;
            var newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= choices.CardPacks.Ordering.Count) return;

            var item = choices.CardPacks.Ordering[idx];
            choices.CardPacks.Ordering.RemoveAt(idx);
            choices.CardPacks.Ordering.Insert(newIdx, item);
            choices.Save(_choicesPath);
            MainFile.Logger.Info($"card pack reorder: {modId} moved to index {newIdx}");
            Callable.From(BuildCardPackRows).CallDeferred();
        }
        catch (Exception ex) { MainFile.Logger.Warn($"card pack reorder error: {ex.Message}"); }
    }

    public static void RefreshCardPacks()
    {
        Callable.From(BuildCardPackRows).CallDeferred();
    }

    private static void EnsureLocaleSubscribed()
    {
        if (_localeChangeSubscribed) return;
        try
        {
            if (LocManager.Instance == null) { MainFile.Logger.Warn("LocManager.Instance null at subscribe time"); return; }
            LocManager.Instance.SubscribeToLocaleChange(OnLocaleChanged);
            _localeChangeSubscribed = true;
            MainFile.Logger.Info("subscribed to LocManager locale change");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"failed to subscribe to locale change: {ex.Message}");
        }
    }

    private static void OnLocaleChanged()
    {
        Callable.From(() =>
        {
            try
            {
                MainFile.Logger.Info($"locale changed → reconciling overlay (was attached to '{_lastScreen?.Name}')");
                if (_hbox != null && GodotObject.IsInstanceValid(_hbox) && _hbox.IsInsideTree())
                {
                    MainFile.Logger.Info("overlay still in tree; refreshing label/items + card pack panel");
                    if (_label != null && GodotObject.IsInstanceValid(_label))
                    {
                        _label.Text = Strings.Get("skin_label") + ":";
                    }
                    RefreshItems();
                    BuildCardPackRows();
                    return;
                }

                MainFile.Logger.Info("overlay lost from tree → re-attaching");
                _opt = null;
                _label = null;
                _hbox = null;
                _cardPackVBox = null;
                _cardPackHeaderBtn = null;
                _cardPackScroll = null;
                _cardPackRows = null;
                var mainLoop = Engine.GetMainLoop();
                if (mainLoop is not SceneTree tree) return;
                var screen = FindCharacterSelectScreen(tree.Root);
                if (screen != null) DoAttach(screen);
                else MainFile.Logger.Info("no CharacterSelectScreen in tree currently; will re-attach when user navigates back");
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"OnLocaleChanged error: {ex.Message}");
            }
        }).CallDeferred();
    }

    private static Node? FindCharacterSelectScreen(Node start)
    {
        if (start.Name.ToString().Contains("CharacterSelectScreen", StringComparison.OrdinalIgnoreCase)) return start;
        foreach (var child in start.GetChildren())
        {
            var found = FindCharacterSelectScreen(child);
            if (found != null) return found;
        }
        return null;
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
            var skinLabel = Strings.Get("skin_label");
            if (_label != null) _label.Text = $"{skinLabel} [{(string.IsNullOrEmpty(_currentCharacter) ? "—" : _currentCharacter)}]:";

            if (_byCharacter == null || !_byCharacter.TryGetValue(_currentCharacter, out var variants) || variants.Count == 0)
            {
                _opt.AddItem(Strings.Get("no_variants"));
                _opt.Disabled = true;
                return;
            }
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            if (!choices.Characters.TryGetValue(_currentCharacter, out var c))
            {
                _opt.AddItem(Strings.Get("not_configured"));
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
