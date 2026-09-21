namespace RPEReader.Core.Parsing.OpcRouter4;

/// <summary>
/// Decodes the 64-bit integers OPC Router writes for elements carrying
/// <c>Type="System.DateTime"</c>. The values are produced by
/// <see cref="DateTime.ToBinary"/>: 62 bits of ticks plus a two-bit
/// <see cref="DateTimeKind"/> in the top bits.
/// </summary>
public static class DotNetDateTimeDecoder
{
    /// <summary>
    /// Sentinel written for "never"/unset timestamps. It is
    /// <c>DateTime.MinValue</c> with <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    public const long UnsetSentinel = 4611686018427387904L;

    /// <summary>
    /// Attempts to turn a raw element value into a readable timestamp.
    /// Returns <c>false</c> for anything that is not a decodable value, so the
    /// caller can fall back to showing the raw text.
    /// </summary>
    public static bool TryDecode(string? rawValue, out DateTime value, out string display)
    {
        value = default;
        display = string.Empty;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (!long.TryParse(rawValue.Trim(), out var binary))
        {
            // Some builds write an ISO-8601 string instead of the binary form.
            if (DateTime.TryParse(rawValue, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                value = parsed;
                display = Format(parsed);
                return true;
            }

            return false;
        }

        if (binary == UnsetSentinel)
        {
            value = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            display = "(not set)";
            return true;
        }

        try
        {
            value = DateTime.FromBinary(binary);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (value.Year is < 1900 or > 2200)
        {
            // Outside any plausible range for a configuration timestamp, so the
            // value is more likely to be something else that happens to parse.
            return false;
        }

        display = Format(value);
        return true;
    }

    private static string Format(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + " UTC",
            DateTimeKind.Local => value.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + " (local)",
            _ => value.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + " (unspecified)"
        };
}
