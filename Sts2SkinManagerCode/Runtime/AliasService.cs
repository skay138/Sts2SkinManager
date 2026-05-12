using System;
using System.Collections.Generic;
using System.Linq;
using Sts2SkinManager.Config;

namespace Sts2SkinManager.Runtime;

// modId → alias resolution + duplicate-name validation.
// modId is the unique key (never displayed for matching); alias is display-only.
// Aliases must not collide with other modIds OR other aliases, so the visible label
// is always uniquely resolvable back to one mod.
public static class AliasService
{
    public static string Resolve(string modId, IReadOnlyDictionary<string, string> aliases)
    {
        if (string.IsNullOrEmpty(modId)) return modId;
        if (aliases != null && aliases.TryGetValue(modId, out var alias) && !string.IsNullOrWhiteSpace(alias))
            return alias.Trim();
        return modId;
    }

    public enum AliasValidationResult
    {
        Ok,
        Empty,
        CollidesWithModId,
        CollidesWithOtherAlias,
        SameAsOwnModId,
    }

    // newAlias is what the user typed. Caller passes:
    //   ownModId: the mod the alias is being assigned to
    //   allModIds: every detected modId in the session (both character variants and card packs)
    //   currentAliases: existing alias map (the ownModId entry, if any, is ignored)
    public static AliasValidationResult Validate(
        string ownModId,
        string newAlias,
        IEnumerable<string> allModIds,
        IReadOnlyDictionary<string, string> currentAliases)
    {
        var trimmed = (newAlias ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed)) return AliasValidationResult.Empty;

        if (string.Equals(trimmed, ownModId, StringComparison.OrdinalIgnoreCase))
            return AliasValidationResult.SameAsOwnModId;

        foreach (var id in allModIds)
        {
            if (string.Equals(id, ownModId, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(id, trimmed, StringComparison.OrdinalIgnoreCase))
                return AliasValidationResult.CollidesWithModId;
        }

        if (currentAliases != null)
        {
            foreach (var kv in currentAliases)
            {
                if (string.Equals(kv.Key, ownModId, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(kv.Value, trimmed, StringComparison.OrdinalIgnoreCase))
                    return AliasValidationResult.CollidesWithOtherAlias;
            }
        }

        return AliasValidationResult.Ok;
    }
}
