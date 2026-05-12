using System;
using System.Collections.Generic;
using System.IO;
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
    private static Label? _previewHoverIcon;
    private static Control? _previewContainer;
    private static TextureRect? _previewRect;
    private static Label? _previewCaption;
    private static bool _previewHovered;
    private static bool _previewAvailable;
    private static long _previewHoverExitToken;
    private static string? _hoveredCardModId;
    private static readonly Dictionary<string, ImageTexture?> _previewCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ImageTexture?> _cardPreviewCache = new(StringComparer.OrdinalIgnoreCase);
    private static VBoxContainer? _cardPackVBox;
    private static Button? _cardPackHeaderBtn;
    private static Button? _cardPackSaveBtn;
    private static Button? _cardPackDiscardBtn;
    private static ScrollContainer? _cardPackScroll;
    private static VBoxContainer? _cardPackRows;
    private static bool _cardPackExpanded = true;

    // Pending (in-memory) state shared by character dropdown + card pack panel.
    // Mutations here don't touch disk; OnSave commits to choices.json and triggers the modal.
    private static CardPacksConfig? _pendingCardPacks;
    private static readonly Dictionary<string, string> _pendingActiveByCharacter = new(StringComparer.OrdinalIgnoreCase);

    // Boot snapshot — what the game actually has loaded right now. dirty = (effective state != boot snapshot).
    // Stays dirty until the user restarts (which re-captures the snapshot). OnDiscard restores everything to this.
    private static CardPacksConfig? _bootSnapshotCardPacks;
    private static readonly Dictionary<string, string> _bootSnapshotActive = new(StringComparer.OrdinalIgnoreCase);

    // Set by MainFile after the watcher is constructed; OnDiscard calls NoteSavedAsApplied()
    // so the post-discard disk write doesn't trigger a phantom restart modal.
    private static ChoicesFileWatcher? _watcher;
    public static void SetWatcher(ChoicesFileWatcher w) => _watcher = w;

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

        var initial = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
        _pendingCardPacks = ClonePacks(initial.CardPacks);
        _bootSnapshotCardPacks = ClonePacks(initial.CardPacks);
        _bootSnapshotActive.Clear();
        foreach (var kv in initial.Characters) _bootSnapshotActive[kv.Key] = kv.Value.Active ?? "default";
        _pendingActiveByCharacter.Clear();
        _previewHovered = false;
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

            var hoverIcon = new Label
            {
                Text = "👁",
                CustomMinimumSize = new Vector2(48, 56),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Stop,
                TooltipText = Strings.Get("preview_toggle_tooltip"),
                Visible = false,
            };
            _previewHoverIcon = hoverIcon;
            hoverIcon.MouseEntered += OnPreviewHoverStart;
            hoverIcon.MouseExited += OnPreviewHoverEnd;
            hbox.AddChild(hoverIcon);

            hbox.ZIndex = 1000;
            screen.AddChild(hbox);
            MainFile.Logger.Info($"SkinSelectorOverlay attached (OptionButton) to {screen.Name}");

            BuildPreviewPanel(screen);
            ApplyPreviewVisibility();
            RefreshItems();

            if (_cardMods.Count > 0) BuildCardPackPanel(screen);

            EnsureLocaleSubscribed();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"overlay attach failed: {ex.Message}");
        }
    }

    private static void BuildPreviewPanel(Node screen)
    {
        var container = new VBoxContainer
        {
            Position = new Vector2(540, 40),
            CustomMinimumSize = new Vector2(240, 280),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _previewContainer = container;
        container.MouseEntered += OnPreviewHoverStart;
        container.MouseExited += OnPreviewHoverEnd;

        var rect = new TextureRect
        {
            CustomMinimumSize = new Vector2(240, 240),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        _previewRect = rect;
        container.AddChild(rect);

        var caption = new Label
        {
            CustomMinimumSize = new Vector2(240, 32),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _previewCaption = caption;
        container.AddChild(caption);

        container.ZIndex = 1000;
        screen.AddChild(container);
        MainFile.Logger.Info("preview panel attached");
    }

    private static void UpdatePreview(string variant)
    {
        if (_previewRect == null || !GodotObject.IsInstanceValid(_previewRect)) goto finalize;
        if (_previewCaption == null || !GodotObject.IsInstanceValid(_previewCaption)) goto finalize;

        DetectedSkinMod? mod = null;
        if (_byCharacter != null && _byCharacter.TryGetValue(_currentCharacter, out var variants))
        {
            mod = variants.FirstOrDefault(v => string.Equals(v.ModId, variant, StringComparison.OrdinalIgnoreCase));
        }

        var bootActive = _bootSnapshotActive.TryGetValue(_currentCharacter, out var b) ? b : "default";
        var isCurrentActive = string.Equals(variant, bootActive, StringComparison.OrdinalIgnoreCase);

        var tex = isCurrentActive ? null : LoadPreviewTexture(mod, variant);
        _previewAvailable = tex != null;

        if (_previewAvailable)
        {
            _previewRect.Texture = tex;
            _previewCaption.Text = variant;
        }
        else
        {
            _previewRect.Texture = null;
            _previewCaption.Text = "";
        }

    finalize:
        if (_previewHoverIcon != null && GodotObject.IsInstanceValid(_previewHoverIcon))
        {
            _previewHoverIcon.Visible = _previewAvailable;
        }
        if (!_previewAvailable) _previewHovered = false;
        ApplyPreviewVisibility();
    }

    private static ImageTexture? LoadPreviewTexture(DetectedSkinMod? mod, string variant)
    {
        if (mod == null) return null;

        var cacheKey = $"{mod.PckPath}|{_currentCharacter}";
        if (_previewCache.TryGetValue(cacheKey, out var cached)) return cached;

        var tex = TryLoadFromConventionFile(mod.PreviewPath) ?? TryLoadFromPckCharSelect(mod.PckPath, _currentCharacter);
        _previewCache[cacheKey] = tex;
        return tex;
    }

    private static ImageTexture? TryLoadFromConventionFile(string? previewPath)
    {
        if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath)) return null;
        try
        {
            var image = new Image();
            var err = image.Load(previewPath);
            if (err != Error.Ok)
            {
                MainFile.Logger.Warn($"preview.png load failed: {previewPath} → {err}");
                return null;
            }
            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"preview.png load error: {previewPath}: {ex.Message}");
            return null;
        }
    }

    private static ImageTexture? TryLoadFromPckCharSelect(string pckPath, string character)
    {
        if (string.IsNullOrEmpty(character)) return null;
        try
        {
            var charLower = character.ToLowerInvariant();
            var prefix = $".godot/imported/char_select_{charLower}.png-";
            var ctex = PckFileExtractor.TryReadFirstMatch(pckPath, p =>
                p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                p.EndsWith(".ctex", StringComparison.OrdinalIgnoreCase) &&
                !p.Contains("_locked", StringComparison.OrdinalIgnoreCase));
            if (ctex == null)
            {
                MainFile.Logger.Info($"no char_select_{charLower} ctex found in {pckPath}");
                return null;
            }

            var (fmt, data) = CtexImageExtractor.ExtractEmbedded(ctex);
            if (data == null || fmt == CtexImageExtractor.CtexFormat.Unknown)
            {
                MainFile.Logger.Warn($"could not extract embedded image from ctex in {pckPath}");
                return null;
            }

            var image = new Image();
            var err = fmt == CtexImageExtractor.CtexFormat.Png
                ? image.LoadPngFromBuffer(data)
                : image.LoadWebpFromBuffer(data);
            if (err != Error.Ok)
            {
                MainFile.Logger.Warn($"{fmt} decode failed for {pckPath}: {err}");
                return null;
            }
            MainFile.Logger.Info($"loaded {fmt} char_select preview from {Path.GetFileName(pckPath)} ({image.GetWidth()}x{image.GetHeight()})");
            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"pck char_select fallback error for {pckPath}: {ex.Message}");
            return null;
        }
    }

    private static ImageTexture? LoadCardPreviewTexture(string modId)
    {
        var mod = _cardMods.FirstOrDefault(m => string.Equals(m.ModId, modId, StringComparison.OrdinalIgnoreCase));
        if (mod == null) return null;

        var cacheKey = $"card|{mod.PckPath}";
        if (_cardPreviewCache.TryGetValue(cacheKey, out var cached)) return cached;

        var tex = TryLoadFromConventionFile(mod.PreviewPath) ?? TryLoadFirstCardArt(mod.PckPath);
        _cardPreviewCache[cacheKey] = tex;
        return tex;
    }

    // First card-art .ctex in alphabetical order. Two real-world patterns supported:
    //   A) base override:   .godot/imported/MegaCrit.Sts2.Core.Models.Cards.{Name}_card_art.png-*.ctex
    //   B) own namespace:   .godot/imported/{Name}.png-*.ctex   (and not char_select)
    private static ImageTexture? TryLoadFirstCardArt(string pckPath)
    {
        try
        {
            var idx = PckFileExtractor.TryReadIndex(pckPath);
            if (idx == null) return null;

            var sortedKeys = idx.Keys
                .Where(k => k.StartsWith(".godot/imported/", StringComparison.OrdinalIgnoreCase)
                            && k.EndsWith(".ctex", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? chosen = null;
            foreach (var k in sortedKeys)
            {
                if (k.Contains("MegaCrit.Sts2.Core.Models.Cards.", StringComparison.OrdinalIgnoreCase)
                    && k.Contains("_card_art", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = k;
                    break;
                }
            }
            if (chosen == null)
            {
                foreach (var k in sortedKeys)
                {
                    if (k.Contains("char_select", StringComparison.OrdinalIgnoreCase)) continue;
                    if (k.Contains("characterselect", StringComparison.OrdinalIgnoreCase)) continue;
                    chosen = k;
                    break;
                }
            }
            if (chosen == null) { MainFile.Logger.Info($"no card art ctex found in {pckPath}"); return null; }

            var ctex = PckFileExtractor.TryRead(pckPath, idx[chosen]);
            if (ctex == null) return null;

            var (fmt, data) = CtexImageExtractor.ExtractEmbedded(ctex);
            if (data == null || fmt == CtexImageExtractor.CtexFormat.Unknown) return null;

            var image = new Image();
            var err = fmt == CtexImageExtractor.CtexFormat.Png
                ? image.LoadPngFromBuffer(data)
                : image.LoadWebpFromBuffer(data);
            if (err != Error.Ok) return null;
            MainFile.Logger.Info($"loaded {fmt} card preview from {Path.GetFileName(pckPath)} ({image.GetWidth()}x{image.GetHeight()}): {chosen}");
            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"card art preview load error for {pckPath}: {ex.Message}");
            return null;
        }
    }

    private static void OnCardRowHoverStart(string modId)
    {
        _previewHoverExitToken++;
        _hoveredCardModId = modId;
        var tex = LoadCardPreviewTexture(modId);
        _previewAvailable = tex != null;
        if (_previewRect != null && GodotObject.IsInstanceValid(_previewRect))
        {
            _previewRect.Texture = tex;
        }
        if (_previewCaption != null && GodotObject.IsInstanceValid(_previewCaption))
        {
            _previewCaption.Text = _previewAvailable ? modId : "";
        }
        _previewHovered = _previewAvailable;
        ApplyPreviewVisibility();
    }

    private static void OnCardRowHoverEnd(string modId)
    {
        if (!string.Equals(_hoveredCardModId, modId, StringComparison.OrdinalIgnoreCase)) return;
        var hbox = _hbox;
        if (hbox == null || !GodotObject.IsInstanceValid(hbox) || !hbox.IsInsideTree())
        {
            _previewHovered = false;
            _hoveredCardModId = null;
            ApplyPreviewVisibility();
            return;
        }
        var myToken = ++_previewHoverExitToken;
        var timer = hbox.GetTree().CreateTimer(0.12);
        timer.Timeout += () =>
        {
            if (_previewHoverExitToken != myToken) return;
            _previewHovered = false;
            _hoveredCardModId = null;
            ApplyPreviewVisibility();
        };
    }

    private static void OnPreviewHoverStart()
    {
        _previewHoverExitToken++;
        _previewHovered = true;
        ApplyPreviewVisibility();
    }

    // Debounce hide: Godot fires spurious MouseExited when sibling controls toggle visibility
    // (panel show/hide triggers input pick re-eval). Wait ~120 ms; if a new Enter arrives in
    // that window, the token mismatches and we keep the panel up. Also covers the 52 px gap
    // between icon and panel during normal hover-traversal.
    private static void OnPreviewHoverEnd()
    {
        var hbox = _hbox;
        if (hbox == null || !GodotObject.IsInstanceValid(hbox) || !hbox.IsInsideTree())
        {
            _previewHovered = false;
            ApplyPreviewVisibility();
            return;
        }
        var myToken = ++_previewHoverExitToken;
        var timer = hbox.GetTree().CreateTimer(0.12);
        timer.Timeout += () =>
        {
            if (_previewHoverExitToken != myToken) return;
            _previewHovered = false;
            ApplyPreviewVisibility();
        };
    }

    private static void ApplyPreviewVisibility()
    {
        if (_previewContainer != null && GodotObject.IsInstanceValid(_previewContainer))
        {
            _previewContainer.Visible = _previewHovered && _previewAvailable;
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

        var headerHbox = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(480, 32),
        };

        var headerBtn = new Button
        {
            CustomMinimumSize = new Vector2(280, 32),
            Alignment = HorizontalAlignment.Left,
        };
        _cardPackHeaderBtn = headerBtn;
        headerBtn.Pressed += ToggleCardPackExpanded;
        headerHbox.AddChild(headerBtn);

        var saveBtn = new Button
        {
            Text = Strings.Get("save_changes"),
            CustomMinimumSize = new Vector2(90, 32),
        };
        _cardPackSaveBtn = saveBtn;
        saveBtn.Pressed += OnSave;
        headerHbox.AddChild(saveBtn);

        var discardBtn = new Button
        {
            Text = Strings.Get("discard_changes"),
            CustomMinimumSize = new Vector2(90, 32),
        };
        _cardPackDiscardBtn = discardBtn;
        discardBtn.Pressed += OnDiscard;
        headerHbox.AddChild(discardBtn);

        vbox.AddChild(headerHbox);

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
        var pending = _pendingCardPacks ?? new CardPacksConfig();
        var total = pending.Ordering.Count;
        var enabled = pending.Enabled.Count(kv => kv.Value);
        var arrow = _cardPackExpanded ? "▼" : "▶";
        var dirty = IsAnyDirty();
        var dirtyMark = dirty ? " *" : "";
        _cardPackHeaderBtn.Text = $"{arrow}  {Strings.Get("card_packs_header")} ({enabled}/{total}){dirtyMark}";

        if (_cardPackSaveBtn != null && GodotObject.IsInstanceValid(_cardPackSaveBtn))
        {
            _cardPackSaveBtn.Disabled = !dirty;
            _cardPackSaveBtn.Text = Strings.Get("save_changes");
        }
        if (_cardPackDiscardBtn != null && GodotObject.IsInstanceValid(_cardPackDiscardBtn))
        {
            _cardPackDiscardBtn.Disabled = !dirty;
            _cardPackDiscardBtn.Text = Strings.Get("discard_changes");
        }
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

        var packs = _pendingCardPacks ?? new CardPacksConfig();
        for (var i = 0; i < packs.Ordering.Count; i++)
        {
            var modId = packs.Ordering[i];
            var row = BuildCardPackRow(modId, packs, i, packs.Ordering.Count);
            _cardPackRows.AddChild(row);
        }
        UpdateCardPackHeader();
    }

    private static Control BuildCardPackRow(string modId, CardPacksConfig packs, int index, int total)
    {
        var hbox = new CardPackRow
        {
            ModId = modId,
            MouseFilter = Control.MouseFilterEnum.Pass,
            MouseDefaultCursorShape = Control.CursorShape.Move,
        };
        var enabled = packs.Enabled.TryGetValue(modId, out var e) ? e : true;

        var dragHandle = new Label
        {
            Text = "⋮⋮",
            CustomMinimumSize = new Vector2(20, 32),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hbox.AddChild(dragHandle);

        var check = new CheckBox
        {
            ButtonPressed = enabled,
            CustomMinimumSize = new Vector2(32, 32),
        };
        hbox.AddChild(check);

        var status = new Label
        {
            CustomMinimumSize = new Vector2(28, 32),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hbox.AddChild(status);

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
            CustomMinimumSize = new Vector2(280, 32),
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = Strings.Get("preview_toggle_tooltip"),
        };
        label.MouseEntered += () => OnCardRowHoverStart(modId);
        label.MouseExited += () => OnCardRowHoverEnd(modId);
        hbox.AddChild(label);

        void ApplyVisual(bool isOn)
        {
            status.Text = isOn ? "✓" : "✗";
            status.Modulate = isOn ? new Color(0.4f, 0.95f, 0.45f) : new Color(0.95f, 0.45f, 0.45f);
            check.Modulate = isOn ? new Color(0.6f, 1.0f, 0.6f) : new Color(0.55f, 0.55f, 0.55f);
            label.Modulate = isOn ? Colors.White : new Color(0.55f, 0.55f, 0.55f);
            dragHandle.Modulate = isOn ? Colors.White : new Color(0.55f, 0.55f, 0.55f);
            orderLabel.Modulate = isOn ? Colors.White : new Color(0.55f, 0.55f, 0.55f);
        }
        ApplyVisual(enabled);

        check.Toggled += isOn =>
        {
            OnCardPackToggle(modId, isOn);
            ApplyVisual(isOn);
        };

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
            _pendingCardPacks ??= ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).CardPacks);
            var current = _pendingCardPacks.Enabled.TryGetValue(modId, out var c) ? c : true;
            if (current == isOn) return;
            _pendingCardPacks.Enabled[modId] = isOn;
            MainFile.Logger.Info($"card pack pending toggle: {modId} → {isOn}");
            UpdateCardPackHeader();
        }
        catch (Exception ex) { MainFile.Logger.Warn($"card pack toggle error: {ex.Message}"); }
    }

    private static void MoveCardPack(string modId, int delta)
    {
        try
        {
            _pendingCardPacks ??= ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).CardPacks);
            var idx = _pendingCardPacks.Ordering.IndexOf(modId);
            if (idx < 0) return;
            var newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= _pendingCardPacks.Ordering.Count) return;

            var item = _pendingCardPacks.Ordering[idx];
            _pendingCardPacks.Ordering.RemoveAt(idx);
            _pendingCardPacks.Ordering.Insert(newIdx, item);
            MainFile.Logger.Info($"card pack pending reorder: {modId} → index {newIdx}");
            Callable.From(BuildCardPackRows).CallDeferred();
        }
        catch (Exception ex) { MainFile.Logger.Warn($"card pack reorder error: {ex.Message}"); }
    }

    public static void HandleDragDropReorder(string sourceModId, string targetModId, bool insertAbove)
    {
        try
        {
            _pendingCardPacks ??= ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).CardPacks);
            var srcIdx = _pendingCardPacks.Ordering.IndexOf(sourceModId);
            var targetIdx = _pendingCardPacks.Ordering.IndexOf(targetModId);
            if (srcIdx < 0 || targetIdx < 0 || srcIdx == targetIdx) return;

            var insertIdx = insertAbove ? targetIdx : targetIdx + 1;
            var item = _pendingCardPacks.Ordering[srcIdx];
            _pendingCardPacks.Ordering.RemoveAt(srcIdx);
            if (srcIdx < insertIdx) insertIdx--;
            if (insertIdx < 0) insertIdx = 0;
            if (insertIdx > _pendingCardPacks.Ordering.Count) insertIdx = _pendingCardPacks.Ordering.Count;
            _pendingCardPacks.Ordering.Insert(insertIdx, item);

            MainFile.Logger.Info($"card pack drag-drop: {sourceModId} → {(insertAbove ? "above" : "below")} {targetModId} (idx {insertIdx})");
            Callable.From(BuildCardPackRows).CallDeferred();
        }
        catch (Exception ex) { MainFile.Logger.Warn($"drag-drop reorder error: {ex.Message}"); }
    }

    private static void OnSave()
    {
        try
        {
            if (!IsAnyDirty())
            {
                MainFile.Logger.Info("save clicked but no pending changes (vs boot snapshot)");
                return;
            }
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            foreach (var kv in _pendingActiveByCharacter)
            {
                if (choices.Characters.TryGetValue(kv.Key, out var c)) c.Active = kv.Value;
            }
            if (_pendingCardPacks != null) choices.CardPacks = ClonePacks(_pendingCardPacks);
            choices.Save(_choicesPath);
            _pendingActiveByCharacter.Clear();

            MainFile.Logger.Info("save → choices.json updated (watcher may also fire; ShowOrReset will dedupe)");
            UpdateCardPackHeader();

            // Show modal directly so this doesn't depend on the file watcher firing.
            // The watcher may also call ShowOrReset; the second call just resets the countdown.
            var managerDataDir = Path.GetDirectoryName(_choicesPath);
            if (!string.IsNullOrEmpty(managerDataDir))
            {
                RestartCountdownModal.ShowOrReset(managerDataDir, 10, () => { });
            }
        }
        catch (Exception ex) { MainFile.Logger.Warn($"OnSave error: {ex.Message}"); }
    }

    private static void OnDiscard()
    {
        try
        {
            if (_bootSnapshotCardPacks == null) return;
            _pendingActiveByCharacter.Clear();
            _pendingCardPacks = ClonePacks(_bootSnapshotCardPacks);

            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            foreach (var kv in _bootSnapshotActive)
            {
                if (choices.Characters.TryGetValue(kv.Key, out var c)) c.Active = kv.Value;
            }
            choices.CardPacks = ClonePacks(_bootSnapshotCardPacks);
            choices.Save(_choicesPath);

            // Restore settings.save card-pack state too so the next launch matches boot.
            var userDataDir = OS.GetUserDataDir();
            var settings = Sts2SettingsWriter.FindAndLoad(userDataDir);
            if (settings != null && _cardMods.Count > 0)
            {
                CardPackApplier.ApplyToSettings(settings, _bootSnapshotCardPacks, _cardMods);
                CardPackApplier.ApplyToMemoryModList(_bootSnapshotCardPacks);
                Sts2SettingsWriter.Save(settings);
            }

            // Tell watcher the new disk state is the applied state so its imminent fire is a no-op.
            _watcher?.NoteSavedAsApplied();

            MainFile.Logger.Info("discard → all changes reverted to boot snapshot (disk + settings + pending)");
            Callable.From(() =>
            {
                BuildCardPackRows();
                RefreshItems();
            }).CallDeferred();
        }
        catch (Exception ex) { MainFile.Logger.Warn($"OnDiscard error: {ex.Message}"); }
    }

    private static bool IsAnyDirty()
    {
        if (_bootSnapshotCardPacks == null) return false;

        var pending = _pendingCardPacks ?? new CardPacksConfig();
        if (!pending.Ordering.SequenceEqual(_bootSnapshotCardPacks.Ordering, StringComparer.OrdinalIgnoreCase)) return true;
        if (pending.Enabled.Count != _bootSnapshotCardPacks.Enabled.Count) return true;
        foreach (var kv in pending.Enabled)
        {
            if (!_bootSnapshotCardPacks.Enabled.TryGetValue(kv.Key, out var v) || v != kv.Value) return true;
        }

        var disk = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
        foreach (var (character, choice) in disk.Characters)
        {
            var bootActive = _bootSnapshotActive.TryGetValue(character, out var b) ? b : choice.Active;
            var effectiveActive = _pendingActiveByCharacter.TryGetValue(character, out var p) ? p : (choice.Active ?? "default");
            if (!string.Equals(effectiveActive, bootActive, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static CardPacksConfig ClonePacks(CardPacksConfig src) => new()
    {
        Schema = src.Schema,
        Ordering = new List<string>(src.Ordering),
        Enabled = new Dictionary<string, bool>(src.Enabled, StringComparer.OrdinalIgnoreCase),
    };

    public static void RefreshCardPacks()
    {
        Callable.From(() =>
        {
            _pendingCardPacks = ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).CardPacks);
            BuildCardPackRows();
        }).CallDeferred();
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
                    if (_previewHoverIcon != null && GodotObject.IsInstanceValid(_previewHoverIcon))
                    {
                        _previewHoverIcon.TooltipText = Strings.Get("preview_toggle_tooltip");
                    }
                    RefreshItems();
                    BuildCardPackRows();
                    return;
                }

                MainFile.Logger.Info("overlay lost from tree → re-attaching");
                _opt = null;
                _label = null;
                _hbox = null;
                _previewHoverIcon = null;
                _previewContainer = null;
                _previewRect = null;
                _previewCaption = null;
                _previewHovered = false;
                _cardPackVBox = null;
                _cardPackHeaderBtn = null;
                _cardPackSaveBtn = null;
                _cardPackDiscardBtn = null;
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
        Callable.From(() =>
        {
            // External disk change (e.g. user-edited choices.json) — drop any pending in-memory char selection
            // so the dropdown reflects what's actually on disk.
            _pendingActiveByCharacter.Clear();
            RefreshItems();
            UpdateCardPackHeader();
        }).CallDeferred();
    }

    private static void RefreshItems()
    {
        if (_opt == null || !GodotObject.IsInstanceValid(_opt)) return;
        _suppressNextItemSelected = true;
        try
        {
            _opt.Clear();
            var skinLabel = Strings.Get("skin_label");

            if (_byCharacter == null || !_byCharacter.TryGetValue(_currentCharacter, out var variants) || variants.Count == 0)
            {
                if (_label != null) _label.Text = $"{skinLabel} [{(string.IsNullOrEmpty(_currentCharacter) ? "—" : _currentCharacter)}]:";
                _opt.AddItem(Strings.Get("no_variants"));
                _opt.Disabled = true;
                UpdatePreview("default");
                return;
            }
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            if (!choices.Characters.TryGetValue(_currentCharacter, out var c))
            {
                if (_label != null) _label.Text = $"{skinLabel} [{(string.IsNullOrEmpty(_currentCharacter) ? "—" : _currentCharacter)}]:";
                _opt.AddItem(Strings.Get("not_configured"));
                _opt.Disabled = true;
                UpdatePreview("default");
                return;
            }
            _opt.Disabled = false;

            // pending > disk
            var effectiveActive = _pendingActiveByCharacter.TryGetValue(_currentCharacter, out var pa) ? pa : (c.Active ?? "default");
            var bootActive = _bootSnapshotActive.TryGetValue(_currentCharacter, out var b) ? b : effectiveActive;
            var charDirty = !string.Equals(effectiveActive, bootActive, StringComparison.OrdinalIgnoreCase);
            var dirtyMark = charDirty ? " *" : "";
            if (_label != null) _label.Text = $"{skinLabel} [{_currentCharacter}]:{dirtyMark}";

            for (var i = 0; i < c.AvailableVariants.Count; i++)
            {
                var v = c.AvailableVariants[i];
                _opt.AddItem(v, i);
                if (string.Equals(v, effectiveActive, StringComparison.OrdinalIgnoreCase))
                {
                    _opt.Selected = i;
                }
            }
            MainFile.Logger.Info($"OptionButton populated for '{_currentCharacter}': {c.AvailableVariants.Count} items, effective='{effectiveActive}' (disk='{c.Active}', boot='{bootActive}')");
            UpdatePreview(effectiveActive);
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

            // pending tracks "what the user wants" vs. disk.
            // If equal to disk, remove the pending entry (no-op).
            if (string.Equals(c.Active, chosen, StringComparison.OrdinalIgnoreCase))
            {
                if (_pendingActiveByCharacter.Remove(_currentCharacter))
                {
                    MainFile.Logger.Info($"  pending cleared (matches disk active='{c.Active}')");
                }
            }
            else
            {
                _pendingActiveByCharacter[_currentCharacter] = chosen;
                MainFile.Logger.Info($"  pending set: {_currentCharacter} → {chosen}");
            }

            // Update label dirty mark + Save/Discard buttons.
            RefreshItems();
            UpdateCardPackHeader();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"select error: {ex.Message}");
        }
    }
}
