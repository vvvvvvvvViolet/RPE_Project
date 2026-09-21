namespace RPEReader.Core.Services;

/// <summary>Shannon entropy over byte values, used to hint at compression or encryption.</summary>
public static class EntropyService
{
    /// <summary>Returns bits-per-byte in the range 0.0 to 8.0.</summary>
    public static double Calculate(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in data)
        {
            counts[b]++;
        }

        double entropy = 0;
        foreach (var count in counts)
        {
            if (count == 0)
            {
                continue;
            }

            var p = (double)count / data.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    /// <summary>Plain-language reading of an entropy value.</summary>
    public static string Describe(double entropy) => entropy switch
    {
        < 1.0 => "very low — mostly repeated or padded bytes",
        < 4.0 => "low — structured text or sparse binary",
        < 6.5 => "moderate — typical mixed text and binary",
        < 7.5 => "high — packed or already-compressed data",
        _ => "very high — compressed, encrypted or random data"
    };
}
