using System;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using Sts2SkinManager.Localization;

namespace Sts2SkinManager.Runtime;

public static class RestartCountdownModal
{
    private const int DefaultSeconds = 10;
    private static NVerticalPopup? _popup;
    private static Godot.Timer? _timer;
    private static int _remaining;
    private static string _managerDataDir = "";
    private static bool _resolved;
    private static Action? _onCancelCallback;

    public static void ShowOrReset(string managerDataDir, int seconds, Action? onCancel)
    {
        _onCancelCallback = onCancel;
        ShowOrResetInner(managerDataDir, seconds);
    }

    public static void ShowOrReset(string managerDataDir, int seconds = DefaultSeconds)
    {
        ShowOrResetInner(managerDataDir, seconds);
    }

    private static void ShowOrResetInner(string managerDataDir, int seconds)
    {
        var mainLoop = Engine.GetMainLoop();
        if (mainLoop is not SceneTree tree)
        {
            MainFile.Logger.Warn("no SceneTree; cannot show modal");
            return;
        }

        Callable.From(() =>
        {
            try
            {
                _managerDataDir = managerDataDir;
                _remaining = seconds;
                _resolved = false;

                if (_popup != null && GodotObject.IsInstanceValid(_popup))
                {
                    RefreshText();
                    _timer?.Start();
                    MainFile.Logger.Info($"countdown reset on existing popup ({seconds}s)");
                    return;
                }

                ShowNew(tree);
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"failed to show modal: {ex}");
            }
        }).CallDeferred();
    }

    private static void ShowNew(SceneTree tree)
    {
        try
        {
            var scenePath = SceneHelper.GetScenePath("ui/vertical_popup");
            var packed = ResourceLoader.Load<PackedScene>(scenePath);
            if (packed == null)
            {
                MainFile.Logger.Warn($"NVerticalPopup scene not found at {scenePath}; modal will not appear.");
                return;
            }
            var popup = packed.Instantiate<NVerticalPopup>();
            tree.Root.AddChild(popup);
            popup.SetText(Strings.Get("modal_title"), BuildBody());
            popup.YesButton.SetText(Strings.Get("btn_restart_now"));
            popup.YesButton.IsYes = true;
            popup.YesButton.Released += _ => OnYes();
            popup.NoButton.SetText(Strings.Get("btn_restart_later"));
            popup.NoButton.IsYes = false;
            popup.NoButton.Visible = true;
            popup.NoButton.Released += _ => OnNo();
            popup.TreeExiting += OnPopupExiting;
            _popup = popup;
            StartTimer(tree, popup);
            MainFile.Logger.Info($"native NVerticalPopup modal shown ({_remaining}s)");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"native modal failed: {ex}");
        }
    }

    private static void StartTimer(SceneTree tree, Node parent)
    {
        if (_timer != null && GodotObject.IsInstanceValid(_timer))
        {
            try { _timer.QueueFree(); } catch { }
        }
        _timer = null;
        _timer = new Godot.Timer { WaitTime = 1.0, Autostart = true, OneShot = false };
        parent.AddChild(_timer);
        _timer.Timeout += OnTick;
    }

    private static void OnTick()
    {
        if (_resolved) return;
        _remaining--;
        RefreshText();
        if (_remaining <= 0)
        {
            _resolved = true;
            _timer?.Stop();
            TriggerRestart();
        }
    }

    private static void RefreshText()
    {
        if (_popup != null && GodotObject.IsInstanceValid(_popup))
        {
            _popup.SetText(Strings.Get("modal_title"), BuildBody());
        }
    }

    private static string BuildBody() => Strings.Get("modal_body", _remaining);

    private static void OnYes()
    {
        if (_resolved) return;
        _resolved = true;
        _timer?.Stop();
        TriggerRestart();
    }

    private static void OnNo()
    {
        if (_resolved) return;
        _resolved = true;
        _timer?.Stop();
        MainFile.Logger.Info("user chose Restart later → reverting choice to previous state.");
        try { _onCancelCallback?.Invoke(); } catch (Exception ex) { MainFile.Logger.Warn($"revert error: {ex.Message}"); }
        if (_popup != null && GodotObject.IsInstanceValid(_popup)) _popup.QueueFree();
    }

    private static void OnPopupExiting()
    {
        _popup = null;
        _timer = null;
        _onCancelCallback = null;
    }

    private static void TriggerRestart()
    {
        RestartHelper.TriggerRestart(_managerDataDir);
    }
}
