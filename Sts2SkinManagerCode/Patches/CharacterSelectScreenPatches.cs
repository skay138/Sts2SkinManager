using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using Sts2SkinManager.Runtime;

namespace Sts2SkinManager.Patches;

[HarmonyPatch(typeof(NCharacterSelectScreen), "_Ready")]
public static class CharacterSelectScreenReadyPatch
{
    public static void Postfix(NCharacterSelectScreen __instance)
    {
        try
        {
            SkinSelectorOverlay.Attach(__instance);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"_Ready postfix error: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "SelectCharacter")]
public static class CharacterSelectScreenSelectPatch
{
    public static void Postfix(CharacterModel characterModel)
    {
        try
        {
            var id = characterModel?.Id?.Entry;
            if (string.IsNullOrEmpty(id)) return;
            SkinSelectorOverlay.OnCharacterSelected(id);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"SelectCharacter postfix error: {ex.Message}");
        }
    }
}
