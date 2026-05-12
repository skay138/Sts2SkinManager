using System;

namespace Sts2SkinManager.Discovery;

// Godot CompressedTexture2D (.ctex) wraps PNG/WebP/VRAM-compressed payloads after a small header.
// Rather than decoding the format struct, we search for the inner payload's magic bytes and slice
// from there. PNG/WebP decoders ignore trailing bytes (IEND chunk / chunk-size driven), so the tail
// past the embedded image data is harmless.
public static class CtexImageExtractor
{
    public enum CtexFormat { Unknown, Png, Webp }

    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static (CtexFormat fmt, byte[]? data) ExtractEmbedded(byte[] ctex)
    {
        if (ctex == null || ctex.Length < 16) return (CtexFormat.Unknown, null);

        var pngIdx = IndexOf(ctex, PngMagic);
        if (pngIdx >= 0)
        {
            var slice = new byte[ctex.Length - pngIdx];
            Buffer.BlockCopy(ctex, pngIdx, slice, 0, slice.Length);
            return (CtexFormat.Png, slice);
        }

        for (var i = 0; i <= ctex.Length - 12; i++)
        {
            if (ctex[i] == 0x52 && ctex[i + 1] == 0x49 && ctex[i + 2] == 0x46 && ctex[i + 3] == 0x46 &&
                ctex[i + 8] == 0x57 && ctex[i + 9] == 0x45 && ctex[i + 10] == 0x42 && ctex[i + 11] == 0x50)
            {
                var slice = new byte[ctex.Length - i];
                Buffer.BlockCopy(ctex, i, slice, 0, slice.Length);
                return (CtexFormat.Webp, slice);
            }
        }

        return (CtexFormat.Unknown, null);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        var end = haystack.Length - needle.Length;
        for (var i = 0; i <= end; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
