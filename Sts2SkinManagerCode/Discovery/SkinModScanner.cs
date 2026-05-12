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
    IReadOnlyList<string> Characters,
    string? PreviewPath
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

    // Reads `animations/characters/{char}/` from the base game pck so we can tell
    // "skin for an existing character" apart from "mod that adds a brand-new character".
    // Touching only the raw bytes here keeps us clear of ModelDb.AllCharacters caching/Harmony timing.
    public static HashSet<string> ScanBaseCharacters(string gameDir)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var basePck = Path.Combine(gameDir, "SlayTheSpire2.pck");
        if (!File.Exists(basePck)) return result;
        foreach (var p in PckPathReader.ReadAsciiRuns(basePck))
        {
            var m = CharacterPathRegex.Match(p);
            if (m.Success) result.Add(m.Groups[1].Value.ToLowerInvariant());
        }
        return result;
    }

    // Probed in order; first hit wins.
    private static readonly string[] PreviewCandidateNames =
    {
        "preview.png", "preview.jpg", "preview.jpeg", "preview.webp",
        "thumbnail.png", "thumbnail.jpg", "thumbnail.jpeg", "thumbnail.webp",
    };

    public static List<DetectedSkinMod> Scan(string modsDir, IReadOnlySet<string> baseCharacters, out List<string> skippedCustomCharacterMods)
    {
        var result = new List<DetectedSkinMod>();
        skippedCustomCharacterMods = new List<string>();
        if (!Directory.Exists(modsDir)) return result;

        foreach (var modDir in Directory.EnumerateDirectories(modsDir))
        {
            var pckFiles = Directory.EnumerateFiles(modDir, "*.pck", SearchOption.TopDirectoryOnly).ToList();
            if (pckFiles.Count == 0) continue;

            var previewPath = FindPreview(modDir);

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
                    // A mod that targets ONLY characters not in the base roster is adding a brand-new
                    // character, not skinning an existing one. Skip so its pck stays auto-mountable —
                    // otherwise our LoadResourcePack intercept would strand the character mod.
                    // Skip only when base whitelist is non-empty (empty = base pck couldn't be read; fall through).
                    var baseHits = baseCharacters.Count == 0
                        ? chars
                        : chars.Where(c => baseCharacters.Contains(c)).ToHashSet();
                    if (baseHits.Count == 0)
                    {
                        skippedCustomCharacterMods.Add($"{pckId} → [{string.Join(",", chars)}]");
                        continue;
                    }
                    result.Add(new DetectedSkinMod(pckId, modDir, pck, SkinModKind.Character, baseHits.ToList(), previewPath));
                }
                else if (isCardMod)
                {
                    result.Add(new DetectedSkinMod(pckId, modDir, pck, SkinModKind.Cards, new List<string>(), previewPath));
                }
            }
        }
        return result;
    }

    private static string? FindPreview(string modDir)
    {
        foreach (var name in PreviewCandidateNames)
        {
            var candidate = Path.Combine(modDir, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
