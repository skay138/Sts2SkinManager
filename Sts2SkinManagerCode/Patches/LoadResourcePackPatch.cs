using Godot;
using HarmonyLib;
using Sts2SkinManager.Runtime;

namespace Sts2SkinManager.Patches;

[HarmonyPatch(typeof(ProjectSettings), nameof(ProjectSettings.LoadResourcePack))]
public static class LoadResourcePackPatch
{
    public static bool Prefix(string pack, ref bool __result)
    {
        if (ManagedPckRegistry.ShouldBlock(pack))
        {
            MainFile.Logger.Info($"intercepted auto-mount of managed pck: {pack}");
            __result = true;
            return false;
        }
        return true;
    }
}
