using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sts2SkinManager.Discovery;

public enum SkinModKind { Character, Cards }

public record DetectedSkinMod(
    string ModId,
    string ModFolder,
    string PckPath,
    SkinModKind Kind,
    IReadOnlyList<string> Characters
);

public static class SkinModScanner
{
    private static readonly Regex CharacterPathRegex = new(
        @"animations/characters/([a-z_][a-z0-9_]*)/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex CardArtBaseOverrideRegex = new(
        @"card_art/MegaCrit\.Sts2\.Core\.Models\.Cards\.",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex CardPortraitsNamespaceRegex = new(
        @"/card_portraits/",
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

            foreach (var pck in pckFiles)
            {
                var paths = PckPathReader.ReadAsciiRuns(pck);
                var chars = new HashSet<string>();
                var isCardMod = false;
                foreach (var p in paths)
                {
                    var m = CharacterPathRegex.Match(p);
                    if (m.Success) chars.Add(m.Groups[1].Value.ToLowerInvariant());
                    if (!isCardMod && (CardArtBaseOverrideRegex.IsMatch(p) || CardPortraitsNamespaceRegex.IsMatch(p)))
                    {
                        isCardMod = true;
                    }
                }

                var pckId = Path.GetFileNameWithoutExtension(pck);
                if (chars.Count > 0)
                {
                    result.Add(new DetectedSkinMod(pckId, modDir, pck, SkinModKind.Character, chars.ToList()));
                }
                else if (isCardMod)
                {
                    result.Add(new DetectedSkinMod(pckId, modDir, pck, SkinModKind.Cards, new List<string>()));
                }
            }
        }
        return result;
    }
}
