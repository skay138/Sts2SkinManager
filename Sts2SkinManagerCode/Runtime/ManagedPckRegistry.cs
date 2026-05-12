using System;
using System.Collections.Generic;
using System.IO;

namespace Sts2SkinManager.Runtime;

public static class ManagedPckRegistry
{
    private static readonly HashSet<string> ManagedPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MountedPaths = new(StringComparer.OrdinalIgnoreCase);

    [ThreadStatic] private static bool _bypassInterceptForCurrentThread;

    public static void Manage(string pckPath) => ManagedPaths.Add(Normalize(pckPath));

    public static bool IsManaged(string pckPath) => ManagedPaths.Contains(Normalize(pckPath));

    public static bool ShouldBlock(string pckPath)
    {
        if (_bypassInterceptForCurrentThread) return false;
        return IsManaged(pckPath);
    }

    public static T RunWithBypass<T>(Func<T> action)
    {
        var prior = _bypassInterceptForCurrentThread;
        _bypassInterceptForCurrentThread = true;
        try { return action(); }
        finally { _bypassInterceptForCurrentThread = prior; }
    }

    public static void MarkMounted(string pckPath) => MountedPaths.Add(Normalize(pckPath));

    public static bool IsMounted(string pckPath) => MountedPaths.Contains(Normalize(pckPath));

    public static IReadOnlyCollection<string> AllMountedPaths => MountedPaths;

    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
