using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Modding;
using Sts2SkinManager.Config;

namespace Sts2SkinManager.Runtime;

public static class LoadOrderEnforcer
{
    public static bool EnsureFirstInModList(Sts2SettingsFile settings, string modId)
    {
        var modList = settings.Root["mod_settings"]?["mod_list"]?.AsArray();
        if (modList == null) return false;

        var currentIndex = -1;
        for (var i = 0; i < modList.Count; i++)
        {
            if (modList[i]?["id"]?.GetValue<string>() == modId)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex == 0) return false;
        if (currentIndex < 0) return false;

        var node = modList[currentIndex];
        if (node == null) return false;
        var serialized = node.ToJsonString();
        var clone = JsonNode.Parse(serialized);
        modList.RemoveAt(currentIndex);
        modList.Insert(0, clone);
        return true;
    }

    public static bool EnsureFirstInMods(string modId)
    {
        var oldMods = ModManager._mods;
        if (oldMods == null || oldMods.Count == 0) return false;
        if (oldMods[0]?.manifest?.id == modId) return false;
        var ourMod = oldMods.FirstOrDefault(m => m.manifest?.id == modId);
        if (ourMod == null) return false;

        var newMods = new List<Mod>(oldMods.Count) { ourMod };
        foreach (var m in oldMods)
        {
            if (!ReferenceEquals(m, ourMod)) newMods.Add(m);
        }
        ModManager._mods = newMods;
        return true;
    }
}
