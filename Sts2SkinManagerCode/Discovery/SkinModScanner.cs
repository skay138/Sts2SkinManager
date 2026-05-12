using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sts2SkinManager.Discovery;

public record DetectedSkinMod(
    string ModId,
    string ModFolder,
    string PckPath,
    IReadOnlyList<string> Characters
);

public static class SkinModScanner
{
    private static readonly Regex CharacterPathRegex = new(
        @"animations/characters/([a-z_][a-z0-9_]*)/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public static List<DetectedSkinMod> Scan(string modsDir)
    {
        var result = new List<DetectedSkinMod>();
        if (!Directory.Exists(modsDir)) return result;

        foreach (var modDir in Directory.EnumerateDirectories(modsDir))
        {
            var pckFiles = Directory.EnumerateFiles(modDir, "*.pck", SearchOption.TopDirectoryOnly).ToList();
            if (pckFiles.Count == 0) continue;

            var modId = Path.GetFileName(modDir);
            foreach (var pck in pckFiles)
            {
                var paths = PckPathReader.ReadAsciiRuns(pck);
                var chars = new HashSet<string>();
                foreach (var p in paths)
                {
                    var m = CharacterPathRegex.Match(p);
                    if (m.Success) chars.Add(m.Groups[1].Value.ToLowerInvariant());
                }
                if (chars.Count > 0)
                {
                    var pckId = Path.GetFileNameWithoutExtension(pck);
                    result.Add(new DetectedSkinMod(pckId, modDir, pck, chars.ToList()));
                }
            }
        }
        return result;
    }
}
