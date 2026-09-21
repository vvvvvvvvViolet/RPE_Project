using System.Text;
using RPEReader.Core.Detection;

namespace RPEReader.Core.Services;

public sealed record EncodingGuess(string Name, string Evidence, Encoding Encoding);

/// <summary>Best-effort text-encoding identification from a header sample.</summary>
public static class EncodingSniffer
{
    public static EncodingGuess Sniff(ReadOnlySpan<byte> sample)
    {
        if (FileSignatures.StartsWith(sample, FileSignatures.Utf8Bom))
        {
            return new EncodingGuess("UTF-8", "UTF-8 byte order mark (EF BB BF)", new UTF8Encoding(true));
        }

        if (FileSignatures.StartsWith(sample, FileSignatures.Utf16LeBom))
        {
            return new EncodingGuess("UTF-16 LE", "byte order mark (FF FE)", Encoding.Unicode);
        }

        if (FileSignatures.StartsWith(sample, FileSignatures.Utf16BeBom))
        {
            return new EncodingGuess("UTF-16 BE", "byte order mark (FE FF)", Encoding.BigEndianUnicode);
        }

        if (sample.Length >= 4 && LooksLikeUtf16NoBom(sample))
        {
            return new EncodingGuess("UTF-16 LE (no BOM)", "alternating NUL bytes in the sample", Encoding.Unicode);
        }

        if (IsValidUtf8(sample))
        {
            var ascii = true;
            foreach (var b in sample)
            {
                if (b > 0x7F)
                {
                    ascii = false;
                    break;
                }
            }

            return ascii
                ? new EncodingGuess("US-ASCII / UTF-8", "no byte above 0x7F in the sample", new UTF8Encoding(false))
                : new EncodingGuess("UTF-8 (no BOM)", "valid multi-byte UTF-8 sequences", new UTF8Encoding(false));
        }

        return new EncodingGuess("unknown / binary", "sample is not valid UTF-8", new UTF8Encoding(false));
    }

    private static bool LooksLikeUtf16NoBom(ReadOnlySpan<byte> sample)
    {
        var length = Math.Min(sample.Length, 512);
        var nulOdd = 0;
        var pairs = 0;
        for (var i = 1; i < length; i += 2)
        {
            pairs++;
            if (sample[i] == 0)
            {
                nulOdd++;
            }
        }

        return pairs > 8 && nulOdd * 100 / pairs > 70;
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> sample)
    {
        var i = 0;
        while (i < sample.Length)
        {
            var b = sample[i];
            int extra;
            if (b <= 0x7F)
            {
                extra = 0;
            }
            else if ((b & 0xE0) == 0xC0)
            {
                extra = 1;
            }
            else if ((b & 0xF0) == 0xE0)
            {
                extra = 2;
            }
            else if ((b & 0xF8) == 0xF0)
            {
                extra = 3;
            }
            else
            {
                return false;
            }

            if (i + extra >= sample.Length)
            {
                // A sequence cut off by the end of the sample is not evidence of
                // invalid text.
                return true;
            }

            for (var k = 1; k <= extra; k++)
            {
                if ((sample[i + k] & 0xC0) != 0x80)
                {
                    return false;
                }
            }

            i += extra + 1;
        }

        return true;
    }
}
