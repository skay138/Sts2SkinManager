using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Modding;

namespace Sts2SkinManager.Config;

public record Sts2SettingsFile(string Path, JsonNode Root);

public static class Sts2SettingsWriter
{
    public static Sts2SettingsFile? FindAndLoad(string userDataDir)
    {
        var steamDir = Path.Combine(userDataDir, "steam");
        if (!Directory.Exists(steamDir)) return null;

        var profileDirs = Directory.EnumerateDirectories(steamDir).ToList();
        if (profileDirs.Count == 0) return null;

        var candidates = profileDirs
            .Select(d => Path.Combine(d, "settings.save"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        if (candidates.Count == 0) return null;
        var path = candidates[0];

        try
        {
            var json = File.ReadAllText(path);
            var root = JsonNode.Parse(json);
            if (root == null) return null;
            return new Sts2SettingsFile(path, root);
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, bool> ReadModEnabledState(Sts2SettingsFile settings)
    {
        var result = new Dictionary<string, bool>();
        var modList = settings.Root["mod_settings"]?["mod_list"]?.AsArray();
        if (modList == null) return result;

        foreach (var entry in modList)
        {
            if (entry == null) continue;
            var id = entry["id"]?.GetValue<string>();
            var enabled = entry["is_enabled"]?.GetValue<bool>() ?? true;
            if (!string.IsNullOrEmpty(id)) result[id] = enabled;
        }
        return result;
    }

    public static bool ApplyModEnabledState(Sts2SettingsFile settings, IReadOnlyDictionary<string, bool> desired)
    {
        var modSettings = settings.Root["mod_settings"];
        if (modSettings == null) return false;
        var modList = modSettings["mod_list"]?.AsArray();
        if (modList == null) return false;

        var changed = false;
        foreach (var entry in modList)
        {
            if (entry == null) continue;
            var id = entry["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(id) || !desired.TryGetValue(id, out var want)) continue;
            var current = entry["is_enabled"]?.GetValue<bool>() ?? true;
            if (current != want)
            {
                entry["is_enabled"] = want;
                changed = true;
            }
        }
        return changed;
    }

    public static void Save(Sts2SettingsFile settings)
    {
        var backup = settings.Path + ".skinmgr.bak";
        if (!File.Exists(backup)) File.Copy(settings.Path, backup);
        var json = settings.Root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settings.Path, json);
    }

    public static int MutateInMemoryModList(IReadOnlyDictionary<string, bool> desired)
    {
        var settings = ModManager._settings;
        if (settings == null) return 0;
        var changed = 0;
        foreach (var entry in settings.ModList)
        {
            if (string.IsNullOrEmpty(entry.Id)) continue;
            if (desired.TryGetValue(entry.Id, out var want) && entry.IsEnabled != want)
            {
                entry.IsEnabled = want;
                changed++;
            }
        }
        return changed;
    }
}
