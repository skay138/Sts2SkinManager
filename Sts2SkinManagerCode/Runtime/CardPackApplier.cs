using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Modding;
using Sts2SkinManager.Config;
using Sts2SkinManager.Discovery;

namespace Sts2SkinManager.Runtime;

public static class CardPackApplier
{
    public static bool ApplyToSettings(Sts2SettingsFile settings, CardPacksConfig packs, List<DetectedSkinMod> detectedCardMods)
    {
        var modList = settings.Root["mod_settings"]?["mod_list"]?.AsArray();
        if (modList == null) return false;

        var changed = false;
        var byModId = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<JsonNode>();
        foreach (var entry in modList)
        {
            if (entry == null) continue;
            entries.Add(entry);
            var id = entry["id"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(id)) byModId[id] = entry;
        }

        foreach (var packModId in packs.Enabled.Keys)
        {
            if (!byModId.TryGetValue(packModId, out var entry)) continue;
            var want = packs.Enabled[packModId];
            var current = entry["is_enabled"]?.GetValue<bool>() ?? true;
            if (current != want)
            {
                entry["is_enabled"] = want;
                changed = true;
            }
        }

        var orderedIds = packs.Ordering;
        var orderedSet = new HashSet<string>(orderedIds, StringComparer.OrdinalIgnoreCase);
        var nonCardEntries = entries.Where(e => !orderedSet.Contains(e["id"]?.GetValue<string>() ?? "")).ToList();
        var orderedCardEntries = orderedIds
            .Where(id => byModId.ContainsKey(id))
            .Select(id => byModId[id])
            .ToList();

        var newList = new List<JsonNode>(nonCardEntries);
        newList.AddRange(orderedCardEntries);

        var orderDiffers = false;
        if (newList.Count != entries.Count) orderDiffers = true;
        else
        {
            for (var i = 0; i < newList.Count; i++)
            {
                if (!ReferenceEquals(newList[i], entries[i])) { orderDiffers = true; break; }
            }
        }

        if (orderDiffers)
        {
            for (var i = modList.Count - 1; i >= 0; i--) modList.RemoveAt(i);
            foreach (var n in newList)
            {
                var serialized = n.ToJsonString();
                var clone = JsonNode.Parse(serialized);
                if (clone != null) modList.Add(clone);
            }
            changed = true;
        }

        return changed;
    }

    public static bool ApplyToMemoryModList(CardPacksConfig packs)
    {
        var settings = ModManager._settings;
        if (settings == null) return false;
        var changed = false;
        foreach (var entry in settings.ModList)
        {
            if (string.IsNullOrEmpty(entry.Id)) continue;
            if (packs.Enabled.TryGetValue(entry.Id, out var want) && entry.IsEnabled != want)
            {
                entry.IsEnabled = want;
                changed = true;
            }
        }
        return changed;
    }
}
