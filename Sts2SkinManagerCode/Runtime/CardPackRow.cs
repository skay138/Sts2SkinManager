using System;
using System.Collections.Generic;
using Godot;

namespace Sts2SkinManager.Runtime;

public enum SkinRowCategory { CardPack, MixedAddon }

public partial class CardPackRow : HBoxContainer
{
    public string ModId { get; set; } = "";
    public SkinRowCategory Category { get; set; } = SkinRowCategory.CardPack;

    private static readonly Color IndicatorColor = new(0.35f, 0.85f, 1.0f, 0.95f);
    private static readonly HashSet<CardPackRow> _allRows = new();

    private ColorRect? _topLine;
    private ColorRect? _bottomLine;

    public override void _Ready()
    {
        _topLine = CreateLine();
        _bottomLine = CreateLine();
        _allRows.Add(this);
        TreeExiting += () => _allRows.Remove(this);
    }

    private ColorRect CreateLine()
    {
        var rect = new ColorRect
        {
            Color = IndicatorColor,
            Visible = false,
            TopLevel = true,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 2000,
            Size = new Vector2(0, 3),
        };
        AddChild(rect);
        return rect;
    }

    private void UpdateIndicatorPositions()
    {
        if (_topLine != null && GodotObject.IsInstanceValid(_topLine))
        {
            _topLine.GlobalPosition = GlobalPosition - new Vector2(0, 2);
            _topLine.Size = new Vector2(Size.X, 3);
        }
        if (_bottomLine != null && GodotObject.IsInstanceValid(_bottomLine))
        {
            _bottomLine.GlobalPosition = GlobalPosition + new Vector2(0, Size.Y - 1);
            _bottomLine.Size = new Vector2(Size.X, 3);
        }
    }

    private string DragType => Category == SkinRowCategory.MixedAddon ? "mixed_addon_row" : "card_pack_row";

    public override Variant _GetDragData(Vector2 atPosition)
    {
        try
        {
            var dict = new Godot.Collections.Dictionary
            {
                { "type", DragType },
                { "modId", ModId },
            };
            var preview = new Label
            {
                Text = $"⇅ {ModId}",
                CustomMinimumSize = new Vector2(280, 32),
                Modulate = new Color(1f, 1f, 1f, 0.85f),
            };
            SetDragPreview(preview);
            return dict;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"_GetDragData error: {ex.Message}");
            return default;
        }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        try
        {
            if (data.VariantType != Variant.Type.Dictionary) { ClearAllIndicators(); return false; }
            var dict = data.AsGodotDictionary();
            // Reject cross-category drops — drag is scoped per panel.
            if (!dict.TryGetValue("type", out var t) || t.AsString() != DragType)
            {
                ClearAllIndicators();
                return false;
            }
            var above = atPosition.Y < Size.Y * 0.5f;
            ShowIndicator(above);
            return true;
        }
        catch { ClearAllIndicators(); return false; }
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        try
        {
            ClearAllIndicators();
            var dict = data.AsGodotDictionary();
            if (!dict.TryGetValue("modId", out var srcVar)) return;
            var src = srcVar.AsString();
            if (string.IsNullOrEmpty(src) || src == ModId) return;
            var above = atPosition.Y < Size.Y * 0.5f;
            if (Category == SkinRowCategory.MixedAddon)
            {
                SkinSelectorOverlay.HandleMixedAddonDragDropReorder(src, ModId, above);
            }
            else
            {
                SkinSelectorOverlay.HandleDragDropReorder(src, ModId, above);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"_DropData error: {ex.Message}");
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd)
        {
            ClearAllIndicators();
        }
    }

    private void ShowIndicator(bool above)
    {
        ClearAllIndicators();
        UpdateIndicatorPositions();
        if (above && _topLine != null) _topLine.Visible = true;
        else if (!above && _bottomLine != null) _bottomLine.Visible = true;
    }

    private static void ClearAllIndicators()
    {
        foreach (var row in _allRows)
        {
            if (!GodotObject.IsInstanceValid(row)) continue;
            if (row._topLine != null && GodotObject.IsInstanceValid(row._topLine)) row._topLine.Visible = false;
            if (row._bottomLine != null && GodotObject.IsInstanceValid(row._bottomLine)) row._bottomLine.Visible = false;
        }
    }
}
