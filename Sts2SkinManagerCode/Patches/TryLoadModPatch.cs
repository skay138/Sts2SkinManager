using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using Sts2SkinManager.Runtime;

namespace Sts2SkinManager.Patches;

[HarmonyPatch(typeof(ModManager), "TryLoadMod")]
public static class TryLoadModPatch
{
    public static bool Prefix(Mod mod)
    {
        var id = mod?.manifest?.id;
        if (string.IsNullOrEmpty(id)) return true;
        if (id == MainFile.ModId) return true;
        if (!ManagedDllRegistry.ShouldBlock(id)) return true;

        MainFile.Logger.Info($"intercepted TryLoadMod (managed mod disabled): {id}");
        mod!.state = ModLoadState.Disabled;
        return false;
    }
}
