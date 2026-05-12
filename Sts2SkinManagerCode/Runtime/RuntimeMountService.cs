using Godot;

namespace Sts2SkinManager.Runtime;

public static class RuntimeMountService
{
    public static bool MountVariantPck(string pckPath)
    {
        if (ManagedPckRegistry.IsMounted(pckPath))
        {
            MainFile.Logger.Info($"variant pck already mounted: {pckPath}");
            return true;
        }
        var ok = ManagedPckRegistry.RunWithBypass(() => ProjectSettings.LoadResourcePack(pckPath));
        if (ok)
        {
            ManagedPckRegistry.MarkMounted(pckPath);
            MainFile.Logger.Info($"mounted variant pck: {pckPath}");
        }
        else
        {
            MainFile.Logger.Warn($"failed to mount variant pck: {pckPath}");
        }
        return ok;
    }
}
