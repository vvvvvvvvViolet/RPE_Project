using System.Text;

namespace RPEReader.Core.Services;

public sealed record ExtractedString(long Offset, string Value, string Kind);

/// <summary>
/// Pulls printable runs out of arbitrary bytes, the way the classic
/// <c>strings</c> utility does. Used by the generic inspector when no
/// structural parser applies.
/// </summary>
public static class StringsExtractor
{
    public static IReadOnlyList<ExtractedString> Extract(
        ReadOnlySpan<byte> data,
        int minimumLength = 4,
        int maxResults = 20_000)
    {
        var results = new List<ExtractedString>();
        ExtractAscii(data, minimumLength, maxResults, results);
        if (results.Count < maxResults)
        {
            ExtractUtf16(data, minimumLength, maxResults, results);
        }

        return results.OrderBy(r => r.Offset).ToList();
    }

    private static void ExtractAscii(ReadOnlySpan<byte> data, int minimumLength, int maxResults, List<ExtractedString> results)
    {
        var builder = new StringBuilder();
        long start = 0;

        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            if (IsPrintable(b))
            {
                if (builder.Length == 0)
                {
                    start = i;
                }

                builder.Append((char)b);
                continue;
            }

            Flush(builder, start, minimumLength, maxResults, results, "ASCII");
            if (results.Count >= maxResults)
            {
                return;
            }
        }

        Flush(builder, start, minimumLength, maxResults, results, "ASCII");
    }

    private static void ExtractUtf16(ReadOnlySpan<byte> data, int minimumLength, int maxResults, List<ExtractedString> results)
    {
        var builder = new StringBuilder();
        long start = 0;

        for (var i = 0; i + 1 < data.Length; i += 2)
        {
            if (data[i + 1] == 0 && IsPrintable(data[i]))
            {
                if (builder.Length == 0)
                {
                    start = i;
                }

                builder.Append((char)data[i]);
                continue;
            }

            Flush(builder, start, minimumLength, maxResults, results, "UTF-16LE");
            if (results.Count >= maxResults)
            {
                return;
            }
        }

        Flush(builder, start, minimumLength, maxResults, results, "UTF-16LE");
    }

    private static void Flush(
        StringBuilder builder,
        long start,
        int minimumLength,
        int maxResults,
        List<ExtractedString> results,
        string kind)
    {
        if (builder.Length >= minimumLength && results.Count < maxResults)
        {
            results.Add(new ExtractedString(start, builder.ToString(), kind));
        }

        builder.Clear();
    }

    private static bool IsPrintable(byte b) => b is >= 0x20 and < 0x7F || b == 0x09;
}
