using System;
using System.Linq;
using System.Reflection;
using Sts2SkinManager.Runtime;

namespace Sts2SkinManager;

/// <summary>
/// Optional ModConfig (Nexus #27) integration via reflection — zero hard dependency.
/// Exposes a single Dropdown for the character-select overlay anchor (Top Left / Top Right)
/// so users can dodge whatever the game itself parks in the opposite corner (MP lobby panel etc.).
/// </summary>
internal static class ModConfigBridge
{
    private const string EntryKey = "overlayAnchor";
    private static readonly string[] AnchorOptions = { "Top Left", "Top Right" };
    private const string AnchorDefault = "Top Right";

    private static bool _attempted;

    public static void TryRegister()
    {
        if (_attempted) return;
        _attempted = true;

        Type? apiType = null;
        Type? entryType = null;
        Type? configTypeEnum = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            apiType = asm.GetType("ModConfig.ModConfigApi", throwOnError: false);
            if (apiType != null)
            {
                entryType = asm.GetType("ModConfig.ConfigEntry", throwOnError: false);
                configTypeEnum = asm.GetType("ModConfig.ConfigType", throwOnError: false);
                break;
            }
        }
        if (apiType == null || entryType == null || configTypeEnum == null)
        {
            MainFile.Logger.Info($"[{MainFile.ModId}] ModConfig not found; overlay anchor defaults to {AnchorDefault}.");
            return;
        }

        try
        {
            var dropdownValue = Enum.Parse(configTypeEnum, "Dropdown");

            var entry = BuildEntry(entryType, dropdownValue,
                key: EntryKey,
                label: "Overlay position (character select)",
                description: "Where to dock the skin manager overlay on the character select screen. Change applies immediately.",
                defaultValue: AnchorDefault,
                options: AnchorOptions,
                onChanged: v => { if (v is string s) SkinSelectorOverlay.SetAnchor(s); });

            var entriesArray = Array.CreateInstance(entryType, 1);
            entriesArray.SetValue(entry, 0);

            var register = apiType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Register"
                                     && m.GetParameters().Length == 3
                                     && m.GetParameters()[1].ParameterType == typeof(string));
            if (register == null)
            {
                MainFile.Logger.Warn($"[{MainFile.ModId}] ModConfigApi.Register(string,string,ConfigEntry[]) not found; skipping.");
                return;
            }
            register.Invoke(null, new object?[] { MainFile.ModId, "Skin Manager", entriesArray });

            // Pull persisted value so the live overlay honors the saved setting on first attach.
            var getValue = apiType.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static);
            if (getValue != null && getValue.IsGenericMethodDefinition)
            {
                try
                {
                    var typedString = getValue.MakeGenericMethod(typeof(string));
                    var saved = typedString.Invoke(null, new object?[] { MainFile.ModId, EntryKey });
                    if (saved is string s && !string.IsNullOrEmpty(s)) SkinSelectorOverlay.SetAnchor(s);
                }
                catch
                {
                    // GetValue<string> may not be callable yet — OnChanged will sync later.
                }
            }

            MainFile.Logger.Info($"[{MainFile.ModId}] ModConfig integration active (overlay anchor dropdown).");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[{MainFile.ModId}] ModConfig register failed: {ex.Message}");
        }
    }

    private static object BuildEntry(
        Type entryType,
        object configType,
        string key,
        string label,
        string description,
        string defaultValue,
        string[] options,
        Action<object?> onChanged)
    {
        var entry = Activator.CreateInstance(entryType)
            ?? throw new InvalidOperationException("ConfigEntry instance creation returned null.");
        SetProp(entry, "Key", key);
        SetProp(entry, "Type", configType);
        SetProp(entry, "Label", label);
        SetProp(entry, "Description", description);
        SetProp(entry, "DefaultValue", defaultValue);
        SetProp(entry, "Options", options);
        SetProp(entry, "OnChanged", onChanged);
        return entry;
    }

    private static void SetProp(object target, string name, object? value)
    {
        var p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        p?.SetValue(target, value);
    }
}
