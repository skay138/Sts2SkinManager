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
    private static List<DetectedSkinMod> _mixedMods = new();

    private static OptionButton? _opt;
    private static Label? _label;
    private static Control? _hbox;
    private static Button? _variantEditBtn;
    private static Button? _variantSaveBtn;
    private static Button? _variantCancelBtn;
    private static LineEdit? _variantEditLine;
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
    // Outer collapsible wrapper: a single toggle header reveals/hides the whole skin manager UI
    // (Save/Discard + Tabs). Default = collapsed so the character select screen stays clean.
    // Tab-style inner: a TabContainer toggles between the card-skin and mixed-addon sections so
    // only one set of rows is visible at a time.
    private static VBoxContainer? _accordionVBox;
    private static Button? _outerToggleBtn;
    private static VBoxContainer? _outerBody;
    private static bool _outerExpanded = false;
    private static TabContainer? _tabContainer;
    private static Button? _cardPackSaveBtn;
    private static Button? _cardPackDiscardBtn;
    private static int _cardPackTabIndex = -1;
    private static int _mixedTabIndex = -1;
    private static ScrollContainer? _cardPackScroll;
    private static VBoxContainer? _cardPackRows;

    private static ScrollContainer? _mixedScroll;
    private static VBoxContainer? _mixedRows;

    // Pending (in-memory) state shared by character dropdown + card pack panel + mixed-addon panel.
    // Mutations here don't touch disk; OnSave commits to choices.json and triggers the modal.
    private static CardPacksConfig? _pendingCardPacks;
    private static CardPacksConfig? _pendingMixedAddons;
    private static readonly Dictionary<string, string> _pendingActiveByCharacter = new(StringComparer.OrdinalIgnoreCase);

    // Boot snapshot — what the game actually has loaded right now. dirty = (effective state != boot snapshot).
    // Stays dirty until the user restarts (which re-captures the snapshot). OnDiscard restores everything to this.
    private static CardPacksConfig? _bootSnapshotCardPacks;
    private static CardPacksConfig? _bootSnapshotMixedAddons;
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

    public static void Configure(string choicesPath, Dictionary<string, List<DetectedSkinMod>> byCharacter, List<DetectedSkinMod> cardMods, List<DetectedSkinMod> mixedMods)
    {
        _choicesPath = choicesPath;
        _byCharacter = byCharacter;
        _cardMods = cardMods;
        _mixedMods = mixedMods;

        var initial = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
        _pendingCardPacks = ClonePacks(initial.CardPacks);
        _pendingMixedAddons = ClonePacks(initial.MixedAddons);
        _bootSnapshotCardPacks = ClonePacks(initial.CardPacks);
        _bootSnapshotMixedAddons = ClonePacks(initial.MixedAddons);
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

            _variantEditLine = new LineEdit
            {
                CustomMinimumSize = new Vector2(240, 56),
                PlaceholderText = Strings.Get("alias_placeholder"),
                Visible = false,
            };
            hbox.AddChild(_variantEditLine);
            _variantEditLine.TextSubmitted += OnVariantAliasSubmitted;
            _variantEditLine.TextChanged += _ =>
            {
                if (_variantEditLine == null || !GodotObject.IsInstanceValid(_variantEditLine)) return;
                _variantEditLine.Modulate = Colors.White;
                _variantEditLine.TooltipText = "";
            };

            _variantEditBtn = new Button
            {
                Text = "✏",
                CustomMinimumSize = new Vector2(40, 56),
                TooltipText = Strings.Get("alias_edit_tooltip"),
            };
            _variantEditBtn.Pressed += ToggleVariantEditMode;
            hbox.AddChild(_variantEditBtn);

            _variantSaveBtn = new Button
            {
                Text = "✓",
                CustomMinimumSize = new Vector2(40, 56),
                TooltipText = Strings.Get("alias_save_tooltip"),
                Visible = false,
            };
            _variantSaveBtn.Pressed += () => OnVariantAliasSubmitted(_variantEditLine.Text);
            hbox.AddChild(_variantSaveBtn);

            _variantCancelBtn = new Button
            {
                Text = "✕",
                CustomMinimumSize = new Vector2(40, 56),
                TooltipText = Strings.Get("alias_cancel_tooltip"),
                Visible = false,
            };
            _variantCancelBtn.Pressed += ExitVariantEditMode;
            hbox.AddChild(_variantCancelBtn);

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

            if (_cardMods.Count > 0 || _mixedMods.Count > 0) BuildAccordionPanel(screen);

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
            _previewCaption.Text = AliasService.Resolve(variant, LoadAliases());
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
            _previewCaption.Text = _previewAvailable ? AliasService.Resolve(modId, LoadAliases()) : "";
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

    // Single panel that hosts both card-skin and mixed-addon sections as tabs. Save/Discard live
    // above the TabContainer so they stay reachable regardless of the active tab. Only one tab's
    // rows are visible at a time — the user clicks the tab header to switch.
    private static void BuildAccordionPanel(Node screen)
    {
        var vbox = new VBoxContainer
        {
            Position = new Vector2(40, 110),
            CustomMinimumSize = new Vector2(480, 40),
        };
        _accordionVBox = vbox;

        // Top row — toggle (compact) + Save / Discard always visible alongside.
        var topRow = new HBoxContainer { CustomMinimumSize = new Vector2(480, 36) };

        var outerToggle = new Button
        {
            CustomMinimumSize = new Vector2(220, 36),
            Alignment = HorizontalAlignment.Left,
        };
        _outerToggleBtn = outerToggle;
        outerToggle.Pressed += ToggleOuterExpanded;
        topRow.AddChild(outerToggle);

        var saveBtn = new Button { Text = Strings.Get("save_changes"), CustomMinimumSize = new Vector2(120, 36) };
        _cardPackSaveBtn = saveBtn;
        saveBtn.Pressed += OnSave;
        topRow.AddChild(saveBtn);

        var discardBtn = new Button { Text = Strings.Get("discard_changes"), CustomMinimumSize = new Vector2(120, 36) };
        _cardPackDiscardBtn = discardBtn;
        discardBtn.Pressed += OnDiscard;
        topRow.AddChild(discardBtn);

        vbox.AddChild(topRow);

        // Body wraps just the tabs — collapsed by default so the screen stays clean.
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(480, 0) };
        _outerBody = body;
        vbox.AddChild(body);

        // Tabs — taller now since the outer toggle gives us screen budget when expanded.
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(480, 400) };
        _tabContainer = tabs;
        body.AddChild(tabs);

        _cardPackTabIndex = -1;
        _mixedTabIndex = -1;

        if (_cardMods.Count > 0)
        {
            var cardTab = new VBoxContainer { Name = "CardSkinTab" };
            var cardScroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(460, 360),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            };
            _cardPackScroll = cardScroll;
            cardTab.AddChild(cardScroll);

            var cardRows = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
            _cardPackRows = cardRows;
            cardScroll.AddChild(cardRows);

            tabs.AddChild(cardTab);
            _cardPackTabIndex = cardTab.GetIndex();
            BuildCardPackRows();
        }

        if (_mixedMods.Count > 0)
        {
            var mixedTab = new VBoxContainer { Name = "MixedAddonTab" };

            var helpLabel = new Label
            {
                Text = Strings.Get("mixed_panel_help"),
                CustomMinimumSize = new Vector2(460, 0),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.75f, 0.75f, 0.75f),
            };
            mixedTab.AddChild(helpLabel);

            var mixedScroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(460, 340),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            };
            _mixedScroll = mixedScroll;
            mixedTab.AddChild(mixedScroll);

            var mixedRows = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
            _mixedRows = mixedRows;
            mixedScroll.AddChild(mixedRows);

            tabs.AddChild(mixedTab);
            _mixedTabIndex = mixedTab.GetIndex();
            BuildMixedAddonRows();
        }

        UpdateCardPackHeader();
        UpdateMixedHeader();
        ApplyOuterExpanded();
        UpdateOuterToggleText();

        vbox.ZIndex = 1000;
        screen.AddChild(vbox);
        MainFile.Logger.Info($"tab panel attached (card={_cardMods.Count}, mixed={_mixedMods.Count})");
    }

    private static void ToggleOuterExpanded()
    {
        _outerExpanded = !_outerExpanded;
        ApplyOuterExpanded();
        UpdateOuterToggleText();
    }

    private static void ApplyOuterExpanded()
    {
        if (_outerBody != null && GodotObject.IsInstanceValid(_outerBody))
            _outerBody.Visible = _outerExpanded;
    }

    private static void UpdateOuterToggleText()
    {
        if (_outerToggleBtn == null || !GodotObject.IsInstanceValid(_outerToggleBtn)) return;
        var arrow = _outerExpanded ? "▼" : "▶";
        var dirty = IsAnyDirty();
        var dirtyMark = dirty ? " *" : "";
        _outerToggleBtn.Text = $"{arrow}  {Strings.Get("skin_manager_section_header")}{dirtyMark}";
    }

    private static void BuildCardPackPanel(Node screen) => BuildAccordionPanel(screen);
    private static void BuildMixedAddonPanel(Node screen) { /* folded into BuildAccordionPanel */ }

    private static void ToggleCardPackExpanded()
    {
        // Tab-based: just switch to the card tab if present.
        if (_tabContainer != null && GodotObject.IsInstanceValid(_tabContainer) && _cardPackTabIndex >= 0)
            _tabContainer.CurrentTab = _cardPackTabIndex;
    }

    private static void ApplyCardPackExpanded() { /* tabs handle visibility */ }

    private static void UpdateCardPackHeader()
    {
        var pending = _pendingCardPacks ?? new CardPacksConfig();
        var total = pending.Ordering.Count;
        var enabled = pending.Enabled.Count(kv => kv.Value);
        var dirty = IsAnyDirty();
        var dirtyMark = dirty ? " *" : "";
        var title = $"{Strings.Get("card_packs_header")} ({enabled}/{total}){dirtyMark}";

        if (_tabContainer != null && GodotObject.IsInstanceValid(_tabContainer) && _cardPackTabIndex >= 0)
            _tabContainer.SetTabTitle(_cardPackTabIndex, title);

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
        UpdateOuterToggleText();
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

        var aliases = LoadAliases();
        var label = new Label
        {
            Text = AliasService.Resolve(modId, aliases),
            CustomMinimumSize = new Vector2(248, 32),
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = modId, // raw key always reachable via hover
        };
        label.MouseEntered += () => OnCardRowHoverStart(modId);
        label.MouseExited += () => OnCardRowHoverEnd(modId);
        hbox.AddChild(label);

        var aliasEdit = new LineEdit
        {
            CustomMinimumSize = new Vector2(192, 32),
            PlaceholderText = Strings.Get("alias_placeholder"),
            Visible = false,
        };
        hbox.AddChild(aliasEdit);

        var editBtn = new Button
        {
            Text = "✏",
            CustomMinimumSize = new Vector2(28, 32),
            TooltipText = Strings.Get("alias_edit_tooltip"),
        };
        hbox.AddChild(editBtn);

        var saveBtn = new Button
        {
            Text = "✓",
            CustomMinimumSize = new Vector2(28, 32),
            TooltipText = Strings.Get("alias_save_tooltip"),
            Visible = false,
        };
        hbox.AddChild(saveBtn);

        var cancelBtn = new Button
        {
            Text = "✕",
            CustomMinimumSize = new Vector2(28, 32),
            TooltipText = Strings.Get("alias_cancel_tooltip"),
            Visible = false,
        };
        hbox.AddChild(cancelBtn);

        void EnterEditMode()
        {
            aliasEdit.Text = LoadAliases().TryGetValue(modId, out var cur) ? cur : "";
            label.Visible = false;
            editBtn.Visible = false;
            aliasEdit.Visible = true;
            saveBtn.Visible = true;
            cancelBtn.Visible = true;
            aliasEdit.Modulate = Colors.White;
            aliasEdit.TooltipText = "";
            aliasEdit.GrabFocus();
            aliasEdit.CaretColumn = aliasEdit.Text.Length;
        }

        void ExitEditMode()
        {
            aliasEdit.Visible = false;
            saveBtn.Visible = false;
            cancelBtn.Visible = false;
            label.Visible = true;
            editBtn.Visible = true;
            label.Text = AliasService.Resolve(modId, LoadAliases());
        }

        void TrySave()
        {
            if (TrySaveAlias(modId, aliasEdit.Text, aliasEdit)) ExitEditMode();
        }

        editBtn.Pressed += EnterEditMode;
        saveBtn.Pressed += TrySave;
        cancelBtn.Pressed += ExitEditMode;
        aliasEdit.TextSubmitted += _ => TrySave();
        aliasEdit.TextChanged += _ =>
        {
            // Clear error styling while user is still typing.
            aliasEdit.Modulate = Colors.White;
            aliasEdit.TooltipText = "";
        };

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

    // === Mixed-addon helpers (panel is built inside BuildAccordionPanel) ===

    private static void ToggleMixedExpanded()
    {
        if (_tabContainer != null && GodotObject.IsInstanceValid(_tabContainer) && _mixedTabIndex >= 0)
            _tabContainer.CurrentTab = _mixedTabIndex;
    }

    private static void ApplyMixedExpanded() { /* tabs handle visibility */ }

    private static void UpdateMixedHeader()
    {
        var pending = _pendingMixedAddons ?? new CardPacksConfig();
        var total = pending.Ordering.Count;
        var enabled = pending.Enabled.Count(kv => kv.Value);
        var title = $"{Strings.Get("mixed_panel_header")} ({enabled}/{total})";

        if (_tabContainer != null && GodotObject.IsInstanceValid(_tabContainer) && _mixedTabIndex >= 0)
        {
            _tabContainer.SetTabTitle(_mixedTabIndex, title);
            _tabContainer.SetTabTooltip(_mixedTabIndex, Strings.Get("mixed_panel_tooltip"));
        }
    }

    private static void BuildMixedAddonRows()
    {
        if (_mixedRows == null || !GodotObject.IsInstanceValid(_mixedRows)) return;

        for (var i = _mixedRows.GetChildCount() - 1; i >= 0; i--)
        {
            var child = _mixedRows.GetChild(i);
            _mixedRows.RemoveChild(child);
            child.QueueFree();
        }

        var packs = _pendingMixedAddons ?? new CardPacksConfig();
        for (var i = 0; i < packs.Ordering.Count; i++)
        {
            var modId = packs.Ordering[i];
            var row = BuildMixedAddonRow(modId, packs, i, packs.Ordering.Count);
            _mixedRows.AddChild(row);
        }
        UpdateMixedHeader();
        UpdateCardPackHeader(); // dirty mark on shared Save button
    }

    private static Control BuildMixedAddonRow(string modId, CardPacksConfig packs, int index, int total)
    {
        var hbox = new CardPackRow
        {
            ModId = modId,
            Category = SkinRowCategory.MixedAddon,
            MouseFilter = Control.MouseFilterEnum.Pass,
            MouseDefaultCursorShape = Control.CursorShape.Move,
        };
        var enabled = packs.Enabled.TryGetValue(modId, out var e) ? e : false;

        var dragHandle = new Label
        {
            Text = "⋮⋮",
            CustomMinimumSize = new Vector2(20, 32),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hbox.AddChild(dragHandle);

        var check = new CheckBox { ButtonPressed = enabled, CustomMinimumSize = new Vector2(32, 32) };
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

        var aliases = LoadAliases();
        var label = new Label
        {
            Text = AliasService.Resolve(modId, aliases),
            CustomMinimumSize = new Vector2(248, 32),
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = modId,
        };
        label.MouseEntered += () => OnCardRowHoverStart(modId);
        label.MouseExited += () => OnCardRowHoverEnd(modId);
        hbox.AddChild(label);

        var aliasEdit = new LineEdit
        {
            CustomMinimumSize = new Vector2(192, 32),
            PlaceholderText = Strings.Get("alias_placeholder"),
            Visible = false,
        };
        hbox.AddChild(aliasEdit);

        var editBtn = new Button { Text = "✏", CustomMinimumSize = new Vector2(28, 32), TooltipText = Strings.Get("alias_edit_tooltip") };
        hbox.AddChild(editBtn);

        var saveBtn = new Button { Text = "✓", CustomMinimumSize = new Vector2(28, 32), TooltipText = Strings.Get("alias_save_tooltip"), Visible = false };
        hbox.AddChild(saveBtn);

        var cancelBtn = new Button { Text = "✕", CustomMinimumSize = new Vector2(28, 32), TooltipText = Strings.Get("alias_cancel_tooltip"), Visible = false };
        hbox.AddChild(cancelBtn);

        void EnterEditMode()
        {
            aliasEdit.Text = LoadAliases().TryGetValue(modId, out var cur) ? cur : "";
            label.Visible = false;
            editBtn.Visible = false;
            aliasEdit.Visible = true;
            saveBtn.Visible = true;
            cancelBtn.Visible = true;
            aliasEdit.Modulate = Colors.White;
            aliasEdit.TooltipText = "";
            aliasEdit.GrabFocus();
            aliasEdit.CaretColumn = aliasEdit.Text.Length;
        }

        void ExitEditMode()
        {
            aliasEdit.Visible = false;
            saveBtn.Visible = false;
            cancelBtn.Visible = false;
            label.Visible = true;
            editBtn.Visible = true;
            label.Text = AliasService.Resolve(modId, LoadAliases());
        }

        void TrySave()
        {
            if (TrySaveAlias(modId, aliasEdit.Text, aliasEdit)) ExitEditMode();
        }

        editBtn.Pressed += EnterEditMode;
        saveBtn.Pressed += TrySave;
        cancelBtn.Pressed += ExitEditMode;
        aliasEdit.TextSubmitted += _ => TrySave();
        aliasEdit.TextChanged += _ =>
        {
            aliasEdit.Modulate = Colors.White;
            aliasEdit.TooltipText = "";
        };

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
            OnMixedAddonToggle(modId, isOn);
            ApplyVisual(isOn);
        };

        var upBtn = new Button { Text = "↑", CustomMinimumSize = new Vector2(32, 32), Disabled = index == 0 };
        upBtn.Pressed += () => MoveMixedAddon(modId, -1);
        hbox.AddChild(upBtn);

        var downBtn = new Button { Text = "↓", CustomMinimumSize = new Vector2(32, 32), Disabled = index == total - 1 };
        downBtn.Pressed += () => MoveMixedAddon(modId, +1);
        hbox.AddChild(downBtn);

        return hbox;
    }

    private static void OnMixedAddonToggle(string modId, bool isOn)
    {
        try
        {
            _pendingMixedAddons ??= ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).MixedAddons);
            var current = _pendingMixedAddons.Enabled.TryGetValue(modId, out var c) ? c : false;
            if (current == isOn) return;
            _pendingMixedAddons.Enabled[modId] = isOn;
            MainFile.Logger.Info($"mixed addon pending toggle: {modId} → {isOn}");
            UpdateMixedHeader();
            UpdateCardPackHeader();
        }
        catch (Exception ex) { MainFile.Logger.Warn($"mixed addon toggle error: {ex.Message}"); }
    }

    private static void MoveMixedAddon(string modId, int delta)
    {
        try
        {
            _pendingMixedAddons ??= ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).MixedAddons);
            var idx = _pendingMixedAddons.Ordering.IndexOf(modId);
            if (idx < 0) return;
            var newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= _pendingMixedAddons.Ordering.Count) return;
            var item = _pendingMixedAddons.Ordering[idx];
            _pendingMixedAddons.Ordering.RemoveAt(idx);
            _pendingMixedAddons.Ordering.Insert(newIdx, item);
            Callable.From(BuildMixedAddonRows).CallDeferred();
        }
        catch (Exception ex) { MainFile.Logger.Warn($"mixed addon reorder error: {ex.Message}"); }
    }

    public static void HandleMixedAddonDragDropReorder(string sourceModId, string targetModId, bool insertAbove)
    {
        try
        {
            _pendingMixedAddons ??= ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).MixedAddons);
            var srcIdx = _pendingMixedAddons.Ordering.IndexOf(sourceModId);
            var targetIdx = _pendingMixedAddons.Ordering.IndexOf(targetModId);
            if (srcIdx < 0 || targetIdx < 0 || srcIdx == targetIdx) return;
            var insertIdx = insertAbove ? targetIdx : targetIdx + 1;
            var item = _pendingMixedAddons.Ordering[srcIdx];
            _pendingMixedAddons.Ordering.RemoveAt(srcIdx);
            if (srcIdx < insertIdx) insertIdx--;
            if (insertIdx < 0) insertIdx = 0;
            if (insertIdx > _pendingMixedAddons.Ordering.Count) insertIdx = _pendingMixedAddons.Ordering.Count;
            _pendingMixedAddons.Ordering.Insert(insertIdx, item);
            Callable.From(BuildMixedAddonRows).CallDeferred();
        }
        catch (Exception ex) { MainFile.Logger.Warn($"mixed addon drag-drop error: {ex.Message}"); }
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
            if (_pendingMixedAddons != null) choices.MixedAddons = ClonePacks(_pendingMixedAddons);
            choices.Save(_choicesPath);
            _pendingActiveByCharacter.Clear();

            MainFile.Logger.Info("save → choices.json updated (watcher may also fire; ShowOrReset will dedupe)");
            UpdateCardPackHeader();
            UpdateMixedHeader();

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
            if (_bootSnapshotMixedAddons != null) _pendingMixedAddons = ClonePacks(_bootSnapshotMixedAddons);

            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            foreach (var kv in _bootSnapshotActive)
            {
                if (choices.Characters.TryGetValue(kv.Key, out var c)) c.Active = kv.Value;
            }
            choices.CardPacks = ClonePacks(_bootSnapshotCardPacks);
            if (_bootSnapshotMixedAddons != null) choices.MixedAddons = ClonePacks(_bootSnapshotMixedAddons);
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
                BuildMixedAddonRows();
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

        if (_bootSnapshotMixedAddons != null)
        {
            var pendingMixed = _pendingMixedAddons ?? new CardPacksConfig();
            if (!pendingMixed.Ordering.SequenceEqual(_bootSnapshotMixedAddons.Ordering, StringComparer.OrdinalIgnoreCase)) return true;
            if (pendingMixed.Enabled.Count != _bootSnapshotMixedAddons.Enabled.Count) return true;
            foreach (var kv in pendingMixed.Enabled)
            {
                if (!_bootSnapshotMixedAddons.Enabled.TryGetValue(kv.Key, out var v) || v != kv.Value) return true;
            }
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

    private static string GetSelectedVariantModId()
    {
        if (_opt == null || !GodotObject.IsInstanceValid(_opt) || _opt.ItemCount == 0) return "";
        var idx = _opt.Selected;
        if (idx < 0 || idx >= _opt.ItemCount) return "";
        var meta = _opt.GetItemMetadata(idx);
        return meta.VariantType == Variant.Type.String ? meta.AsString() : _opt.GetItemText(idx);
    }

    private static void UpdateVariantEditBtnState(string variantModId)
    {
        if (_variantEditBtn == null || !GodotObject.IsInstanceValid(_variantEditBtn)) return;
        // "default" is a virtual variant (= unmount everything) — no alias makes sense for it.
        // Dropdown being disabled (no variants / no config) also disables aliasing.
        var dropdownActive = _opt != null && GodotObject.IsInstanceValid(_opt) && !_opt.Disabled;
        var canEdit = dropdownActive
            && !string.IsNullOrEmpty(variantModId)
            && !string.Equals(variantModId, "default", StringComparison.OrdinalIgnoreCase);
        _variantEditBtn.Disabled = !canEdit;
        _variantEditBtn.Modulate = canEdit ? Colors.White : new Color(0.55f, 0.55f, 0.55f);
    }

    private static void ToggleVariantEditMode()
    {
        if (_variantEditLine == null || !GodotObject.IsInstanceValid(_variantEditLine)) return;
        if (_opt == null || !GodotObject.IsInstanceValid(_opt)) return;
        if (_variantEditLine.Visible) { ExitVariantEditMode(); return; }

        var modId = GetSelectedVariantModId();
        if (string.IsNullOrEmpty(modId) || string.Equals(modId, "default", StringComparison.OrdinalIgnoreCase))
            return;

        _variantEditLine.Text = LoadAliases().TryGetValue(modId, out var cur) ? cur : "";
        _variantEditLine.Modulate = Colors.White;
        _variantEditLine.TooltipText = "";
        _opt.Visible = false;
        if (_variantEditBtn != null && GodotObject.IsInstanceValid(_variantEditBtn)) _variantEditBtn.Visible = false;
        _variantEditLine.Visible = true;
        if (_variantSaveBtn != null && GodotObject.IsInstanceValid(_variantSaveBtn)) _variantSaveBtn.Visible = true;
        if (_variantCancelBtn != null && GodotObject.IsInstanceValid(_variantCancelBtn)) _variantCancelBtn.Visible = true;
        _variantEditLine.GrabFocus();
        _variantEditLine.CaretColumn = _variantEditLine.Text.Length;
    }

    private static void ExitVariantEditMode()
    {
        if (_variantEditLine == null || !GodotObject.IsInstanceValid(_variantEditLine)) return;
        if (_opt == null || !GodotObject.IsInstanceValid(_opt)) return;
        _variantEditLine.Visible = false;
        if (_variantSaveBtn != null && GodotObject.IsInstanceValid(_variantSaveBtn)) _variantSaveBtn.Visible = false;
        if (_variantCancelBtn != null && GodotObject.IsInstanceValid(_variantCancelBtn)) _variantCancelBtn.Visible = false;
        _opt.Visible = true;
        if (_variantEditBtn != null && GodotObject.IsInstanceValid(_variantEditBtn)) _variantEditBtn.Visible = true;
    }

    private static void OnVariantAliasSubmitted(string newText)
    {
        if (_variantEditLine == null || !GodotObject.IsInstanceValid(_variantEditLine)) return;
        var modId = GetSelectedVariantModId();
        if (string.IsNullOrEmpty(modId)) { ExitVariantEditMode(); return; }
        if (TrySaveAlias(modId, newText, _variantEditLine)) ExitVariantEditMode();
    }

    private static Dictionary<string, string> LoadAliases()
    {
        var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
        return new Dictionary<string, string>(choices.Aliases, StringComparer.OrdinalIgnoreCase);
    }

    // All modIds the user could possibly assign an alias to — variant pcks + card packs.
    // Used to enforce "alias must not collide with any modId or other alias".
    private static IEnumerable<string> EnumerateAllModIds()
    {
        if (_byCharacter != null)
        {
            foreach (var kv in _byCharacter)
                foreach (var v in kv.Value)
                    yield return v.ModId;
        }
        foreach (var m in _cardMods) yield return m.ModId;
        // Mixed mods are already present in _byCharacter (registered as Character variants),
        // so we don't double-yield them here.
    }

    // Saves an alias attempt. Returns true when the alias is accepted (incl. empty = clear);
    // returns false when validation rejects the input, and styles `edit` to indicate the error.
    private static bool TrySaveAlias(string modId, string newAlias, LineEdit edit)
    {
        try
        {
            var trimmed = (newAlias ?? "").Trim();
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);

            if (string.IsNullOrEmpty(trimmed))
            {
                // Empty = clear the alias.
                if (choices.Aliases.Remove(modId))
                {
                    choices.Save(_choicesPath);
                    MainFile.Logger.Info($"alias cleared: {modId}");
                    Callable.From(() => { BuildCardPackRows(); BuildMixedAddonRows(); RefreshItems(); }).CallDeferred();
                }
                return true;
            }

            var verdict = AliasService.Validate(modId, trimmed, EnumerateAllModIds(), choices.Aliases);
            if (verdict != AliasService.AliasValidationResult.Ok)
            {
                edit.Modulate = new Color(1f, 0.55f, 0.55f);
                edit.TooltipText = verdict switch
                {
                    AliasService.AliasValidationResult.CollidesWithModId => Strings.Get("alias_dup_modid"),
                    AliasService.AliasValidationResult.CollidesWithOtherAlias => Strings.Get("alias_dup_alias"),
                    AliasService.AliasValidationResult.SameAsOwnModId => Strings.Get("alias_same_as_own"),
                    _ => "",
                };
                MainFile.Logger.Info($"alias rejected for {modId}: {verdict} (input='{trimmed}')");
                return false;
            }

            choices.Aliases[modId] = trimmed;
            choices.Save(_choicesPath);
            MainFile.Logger.Info($"alias saved: {modId} → '{trimmed}'");
            Callable.From(() => { BuildCardPackRows(); BuildMixedAddonRows(); RefreshItems(); }).CallDeferred();
            return true;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"TrySaveAlias error: {ex.Message}");
            return false;
        }
    }

    public static void RefreshCardPacks()
    {
        Callable.From(() =>
        {
            _pendingCardPacks = ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).CardPacks);
            BuildCardPackRows();
        }).CallDeferred();
    }

    public static void RefreshMixedAddons()
    {
        Callable.From(() =>
        {
            _pendingMixedAddons = ClonePacks(SkinChoicesConfig.LoadOrEmpty(_choicesPath).MixedAddons);
            BuildMixedAddonRows();
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
                    if (_variantEditBtn != null && GodotObject.IsInstanceValid(_variantEditBtn))
                    {
                        _variantEditBtn.TooltipText = Strings.Get("alias_edit_tooltip");
                    }
                    if (_variantSaveBtn != null && GodotObject.IsInstanceValid(_variantSaveBtn))
                    {
                        _variantSaveBtn.TooltipText = Strings.Get("alias_save_tooltip");
                    }
                    if (_variantCancelBtn != null && GodotObject.IsInstanceValid(_variantCancelBtn))
                    {
                        _variantCancelBtn.TooltipText = Strings.Get("alias_cancel_tooltip");
                    }
                    if (_variantEditLine != null && GodotObject.IsInstanceValid(_variantEditLine))
                    {
                        _variantEditLine.PlaceholderText = Strings.Get("alias_placeholder");
                    }
                    RefreshItems();
                    BuildCardPackRows();
                    BuildMixedAddonRows();
                    UpdateMixedHeader();
                    return;
                }

                MainFile.Logger.Info("overlay lost from tree → re-attaching");
                _opt = null;
                _label = null;
                _hbox = null;
                _variantEditBtn = null;
                _variantSaveBtn = null;
                _variantCancelBtn = null;
                _variantEditLine = null;
                _previewHoverIcon = null;
                _previewContainer = null;
                _previewRect = null;
                _previewCaption = null;
                _previewHovered = false;
                _accordionVBox = null;
                _outerToggleBtn = null;
                _outerBody = null;
                _tabContainer = null;
                _cardPackTabIndex = -1;
                _mixedTabIndex = -1;
                _cardPackSaveBtn = null;
                _cardPackDiscardBtn = null;
                _cardPackScroll = null;
                _cardPackRows = null;
                _mixedScroll = null;
                _mixedRows = null;
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
                ExitVariantEditMode();
                UpdateVariantEditBtnState("");
                UpdatePreview("default");
                return;
            }
            var choices = SkinChoicesConfig.LoadOrEmpty(_choicesPath);
            if (!choices.Characters.TryGetValue(_currentCharacter, out var c))
            {
                if (_label != null) _label.Text = $"{skinLabel} [{(string.IsNullOrEmpty(_currentCharacter) ? "—" : _currentCharacter)}]:";
                _opt.AddItem(Strings.Get("not_configured"));
                _opt.Disabled = true;
                ExitVariantEditMode();
                UpdateVariantEditBtnState("");
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

            var aliases = LoadAliases();
            var mixedIds = new HashSet<string>(_mixedMods.Select(m => m.ModId), StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < c.AvailableVariants.Count; i++)
            {
                var v = c.AvailableVariants[i];
                var isDefault = string.Equals(v, "default", StringComparison.OrdinalIgnoreCase);
                var resolved = isDefault ? v : AliasService.Resolve(v, aliases);
                // 📦 marks mixed mods (bring card art / events in addition to the spine) so the user
                // knows picking it from the dropdown applies the whole bundle as main + extras.
                var isMixedItem = !isDefault && mixedIds.Contains(v);
                var displayText = isMixedItem ? $"📦 {resolved}" : resolved;
                _opt.AddItem(displayText, i);
                _opt.SetItemMetadata(i, v);
                if (isMixedItem) _opt.SetItemTooltip(i, Strings.Get("mixed_indicator_tooltip"));
                if (string.Equals(v, effectiveActive, StringComparison.OrdinalIgnoreCase))
                {
                    _opt.Selected = i;
                }
            }
            _opt.TooltipText = Strings.Get("mixed_indicator_tooltip");
            UpdateVariantEditBtnState(effectiveActive);
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
            // GetItemText returns the display label (which may be an alias); GetItemMetadata
            // returns the underlying ModId we stored at AddItem time. We always match on the
            // ModId so aliases stay purely cosmetic.
            var meta = _opt.GetItemMetadata((int)index);
            var chosen = meta.VariantType == Variant.Type.String ? meta.AsString() : _opt.GetItemText((int)index);
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
