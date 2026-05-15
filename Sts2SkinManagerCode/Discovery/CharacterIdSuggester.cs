using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sts2SkinManager.Discovery;

// "Which base character does this mod target?" — answered by inspecting the mod's actual binary
// content for evidence. Designed to scale without per-mod whitelists or keyword tables.
//
// The key insight: every .NET-based mod that swaps a base character has to reference the target
// character somewhere in its IL — as an asset path, an enum constant, or a string literal. The
// .NET assembly stores these in the user-string heap as UTF-16 LE. Pck files store asset paths
// as ASCII. By searching for the right byte sequences in the right encodings (and ignoring
// noisy ASCII matches from random byte alignments inside DLL metadata), we get a single
// dominant character for any focused skin mod, and "ambiguous" for any utility/advisor that
// references all characters equally.
//
// Scoring (per base character id):
//   pck:  +5 per "characters/{id}/" ASCII match  (literal asset override path = certain hit)
//   dll:  +5 per "characters/{id}/" UTF-16 match (DLL ResourceLoader.Load with the path)
//   dll:  +3 per "{ID_UPPERCASE}"   UTF-16 match (CHARACTER.{X} enum constant in IL)
//   dll:  +1 per "{id}"             UTF-16 match (any user-string reference)
//
// ASCII matches inside DLL bytes are intentionally NOT scored — DLL metadata aligns randomly
// and produces noisy matches against short character ids (Sts2CardAdvisor's DLL hits 16-66
// ASCII matches per character due to type/method name table, false positives on dominance).
//
// Selection:
//   - top score >= 4 (meaningful evidence)
//   - top score >= 2 * runner-up score (clear dominance, not just a tie-break margin)
//   - otherwise null (ambiguous → manifest hinter / manual assignment fallback)
public static class CharacterIdSuggester
{
    private const int MinScore = 4;
    private const double DominanceRatio = 2.0;

    public static string? Suggest(string modFolder, IReadOnlySet<string> baseCharacters)
    {
        if (baseCharacters.Count == 0) return null;
        if (!Directory.Exists(modFolder)) return null;

        var counts = baseCharacters.ToDictionary(c => c, _ => 0, StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(modFolder, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".dll" || ext == ".pck";
                });
        }
        catch { return null; }

        foreach (var file in files)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(file); }
            catch { continue; }

            var isDll = file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            ScoreFile(bytes, isDll, counts);
        }

        if (counts.Count == 0) return null;
        var ranked = counts.OrderByDescending(kv => kv.Value).ToList();
        var top = ranked[0];
        if (top.Value < MinScore) return null;
        if (ranked.Count >= 2)
        {
            var runnerUp = ranked[1].Value;
            if (runnerUp > 0 && top.Value < runnerUp * DominanceRatio) return null;
        }
        return top.Key;
    }

    private static void ScoreFile(byte[] bytes, bool isDll, Dictionary<string, int> counts)
    {
        foreach (var charId in counts.Keys.ToList())
        {
            var lc = charId.ToLowerInvariant();
            var uc = charId.ToUpperInvariant();
            var pathFragment = $"characters/{lc}/";

            // Asset-path matches are the strongest single signal. Pck files store paths in
            // ASCII; DLLs that load assets via ResourceLoader.Load("res://...") store the path
            // as UTF-16 in the user-string heap.
            counts[charId] += 5 * CountSequence(bytes, Encoding.ASCII.GetBytes(pathFragment));
            if (isDll)
            {
                counts[charId] += 5 * CountSequence(bytes, Encoding.Unicode.GetBytes(pathFragment));
                counts[charId] += 3 * CountSequence(bytes, Encoding.Unicode.GetBytes(uc));
                counts[charId] += 1 * CountSequence(bytes, Encoding.Unicode.GetBytes(lc));
            }
        }
    }

    // Naive O(N*M) byte sequence search. Fast enough for typical mod file sizes (under 100 MB
    // total). Avoids any allocation per match.
    private static int CountSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length) return 0;
        var count = 0;
        var limit = haystack.Length - needle.Length;
        for (var i = 0; i <= limit; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match)
            {
                count++;
                i += needle.Length - 1; // non-overlapping
            }
        }
        return count;
    }
}
