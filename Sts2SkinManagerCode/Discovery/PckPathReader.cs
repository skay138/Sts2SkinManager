using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sts2SkinManager.Discovery;

public static class PckPathReader
{
    public static List<string> ReadAsciiRuns(string pckPath, int minLength = 8)
    {
        var result = new List<string>();
        if (!File.Exists(pckPath)) return result;

        var bytes = File.ReadAllBytes(pckPath);
        var run = new StringBuilder();
        foreach (var b in bytes)
        {
            if (b >= 32 && b < 127)
            {
                run.Append((char)b);
            }
            else
            {
                if (run.Length >= minLength) result.Add(run.ToString());
                run.Clear();
            }
        }
        if (run.Length >= minLength) result.Add(run.ToString());
        return result;
    }
}
