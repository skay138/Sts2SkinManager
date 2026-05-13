using System;
using System.Collections.Generic;

namespace Sts2SkinManager.Runtime;

public static class ManagedDllRegistry
{
    private static readonly HashSet<string> ManagedIds = new(StringComparer.OrdinalIgnoreCase);

    public static void Manage(string modId) => ManagedIds.Add(modId);

    public static bool IsManaged(string modId) => ManagedIds.Contains(modId);

    public static bool ShouldBlock(string modId) => ManagedIds.Contains(modId);

    public static IReadOnlyCollection<string> AllManagedIds => ManagedIds;
}
