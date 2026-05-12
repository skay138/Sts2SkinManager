using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sts2SkinManager.Discovery;

public sealed class PckEntry
{
    public string Path { get; init; } = "";
    public long Offset { get; init; }
    public long Size { get; init; }
}

// Reads Godot 4 PCK directory without mounting. Format probing rules:
//   - "GDPC" magic at offset 0 (we only handle standalone pcks, not embedded-in-exe).
//   - file_base lives in the header at offset 24 (uint64 LE).
//   - For v3 the directory is at the tail of the file but the trailer's stored offset is
//     inconsistent across mods, so we locate it by scanning backward for the first plausible
//     (file_count, plen, path-prefix) triple at 4-byte alignment.
//   - Entry offsets are stored relative to file_base.
public static class PckFileExtractor
{
    private const uint MagicGdpc = 0x43504447; // "GDPC" LE
    private const int TailScanWindow = 512 * 1024;

    private static readonly string[] PathPrefixHints =
    {
        ".godot/", "animations/", "images/", "card_", "res://", "characters/", "scripts/",
    };

    public static Dictionary<string, PckEntry>? TryReadIndex(string pckPath)
    {
        if (!File.Exists(pckPath)) return null;
        try
        {
            using var fs = new FileStream(pckPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            if (br.ReadUInt32() != MagicGdpc) return null;

            var packFormat = br.ReadUInt32();
            br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32();    // godot version triple

            long fileBase = 0;
            if (packFormat >= 2)
            {
                br.ReadUInt32();              // pack flags (rel_filebase ignored — STS2 .pcks are standalone)
                fileBase = br.ReadInt64();
            }

            var dirStart = LocateDirectoryStart(fs, fileBase);
            if (dirStart < 0) return null;

            fs.Seek(dirStart, SeekOrigin.Begin);
            var fileCount = br.ReadUInt32();
            if (fileCount > 1_000_000) return null;

            var entries = new Dictionary<string, PckEntry>(StringComparer.OrdinalIgnoreCase);
            for (uint i = 0; i < fileCount; i++)
            {
                var pathLen = br.ReadUInt32();
                if (pathLen > 4096) return null;
                var pathBytes = br.ReadBytes((int)pathLen);

                var actualLen = pathBytes.Length;
                while (actualLen > 0 && pathBytes[actualLen - 1] == 0) actualLen--;
                var path = Encoding.UTF8.GetString(pathBytes, 0, actualLen);

                var fileOffsetRel = br.ReadInt64();
                var fileSize = br.ReadInt64();
                br.ReadBytes(16);                                  // md5
                if (packFormat >= 2) br.ReadUInt32();              // per-entry flags

                entries[path] = new PckEntry
                {
                    Path = path,
                    Offset = fileBase + fileOffsetRel,
                    Size = fileSize,
                };
            }
            return entries;
        }
        catch
        {
            return null;
        }
    }

    // Scans the tail of the file (last TailScanWindow bytes) backward at 4-byte stride looking for
    // the first 8-byte sequence that decodes as (file_count, plen) followed by a path string with a
    // recognizable Godot resource prefix. Robust against v3 trailer inconsistencies.
    private static long LocateDirectoryStart(FileStream fs, long fileBase)
    {
        var len = fs.Length;
        if (len < 128) return -1;

        var windowSize = (int)Math.Min(TailScanWindow, Math.Max(0, len - fileBase));
        if (windowSize < 64) return -1;

        var windowBase = len - windowSize;
        fs.Seek(windowBase, SeekOrigin.Begin);
        var buf = new byte[windowSize];
        var total = 0;
        while (total < windowSize)
        {
            var read = fs.Read(buf, total, windowSize - total);
            if (read <= 0) break;
            total += read;
        }
        if (total != windowSize) return -1;

        var maxStartInBuf = windowSize - 12;
        for (var o = maxStartInBuf; o >= 0; o -= 4)
        {
            var fcount = BitConverter.ToUInt32(buf, o);
            if (fcount < 1 || fcount > 50_000) continue;

            var plen = BitConverter.ToUInt32(buf, o + 4);
            if (plen < 4 || plen > 512 || plen % 4 != 0) continue;
            if (o + 8 + plen > buf.Length) continue;

            var actLen = (int)plen;
            while (actLen > 0 && buf[o + 8 + actLen - 1] == 0) actLen--;
            if (actLen == 0) continue;

            string pathStr;
            try { pathStr = Encoding.UTF8.GetString(buf, o + 8, actLen); }
            catch { continue; }

            var matched = false;
            foreach (var hint in PathPrefixHints)
            {
                if (pathStr.StartsWith(hint, StringComparison.Ordinal)) { matched = true; break; }
            }
            if (!matched) continue;

            return windowBase + o;
        }
        return -1;
    }

    public static byte[]? TryRead(string pckPath, PckEntry entry)
    {
        if (entry.Size <= 0 || entry.Size > 64 * 1024 * 1024) return null;
        try
        {
            using var fs = new FileStream(pckPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Seek(entry.Offset, SeekOrigin.Begin);
            var buf = new byte[entry.Size];
            var total = 0;
            while (total < buf.Length)
            {
                var read = fs.Read(buf, total, buf.Length - total);
                if (read <= 0) return null;
                total += read;
            }
            return buf;
        }
        catch
        {
            return null;
        }
    }

    public static byte[]? TryReadFirstMatch(string pckPath, Func<string, bool> pathPredicate)
    {
        var idx = TryReadIndex(pckPath);
        if (idx == null) return null;
        foreach (var kv in idx)
        {
            if (pathPredicate(kv.Key)) return TryRead(pckPath, kv.Value);
        }
        return null;
    }
}
