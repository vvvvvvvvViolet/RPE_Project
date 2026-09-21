using System.Globalization;
using RPEReader.Core.Models;

namespace RPEReader.Core.Services;

public enum SearchMode
{
    /// <summary>Match node names, field names and field values.</summary>
    Text,

    /// <summary>Match field names only.</summary>
    FieldName,

    /// <summary>Match a byte pattern against the raw file.</summary>
    Hex
}

public sealed record SearchHit(
    string Location,
    string Name,
    string? Value,
    string Kind,
    long? Offset,
    RpeNode? Node);

/// <summary>Text, field-name and hex searching over a parsed document or the raw file.</summary>
public sealed class SearchService
{
    private readonly RpeLimits _limits;

    public SearchService(RpeLimits? limits = null) => _limits = limits ?? RpeLimits.Default;

    /// <summary>Searches the parsed tree. Returns at most <see cref="RpeLimits.MaxSearchResults"/> hits.</summary>
    public IReadOnlyList<SearchHit> SearchTree(RpeNode root, string query, SearchMode mode, bool caseSensitive)
    {
        ArgumentNullException.ThrowIfNull(root);

        var hits = new List<SearchHit>();
        if (string.IsNullOrEmpty(query) || mode == SearchMode.Hex)
        {
            return hits;
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        Walk(root, string.Empty, query, mode, comparison, hits);
        return hits;
    }

    private void Walk(RpeNode node, string path, string query, SearchMode mode, StringComparison comparison, List<SearchHit> hits)
    {
        if (hits.Count >= _limits.MaxSearchResults)
        {
            return;
        }

        var here = string.IsNullOrEmpty(path) ? node.Name : $"{path} / {node.Name}";

        if (mode == SearchMode.Text)
        {
            if (node.Name.Contains(query, comparison) || (node.Value?.Contains(query, comparison) ?? false))
            {
                hits.Add(new SearchHit(path, node.Name, node.Value, "Node", null, node));
            }
        }

        foreach (var field in node.Fields)
        {
            if (hits.Count >= _limits.MaxSearchResults)
            {
                return;
            }

            var match = mode switch
            {
                SearchMode.FieldName => field.Name.Contains(query, comparison),
                _ => field.Name.Contains(query, comparison) || (field.Value?.Contains(query, comparison) ?? false)
            };

            if (match)
            {
                hits.Add(new SearchHit(here, field.Name, field.Value, "Field", null, node));
            }
        }

        foreach (var child in node.Children)
        {
            Walk(child, here, query, mode, comparison, hits);
        }
    }

    /// <summary>
    /// Searches the raw file for a byte pattern. <paramref name="hexQuery"/> may
    /// use spaces or dashes as separators; "??" matches any byte.
    /// </summary>
    public IReadOnlyList<SearchHit> SearchHex(HexViewSource source, string hexQuery)
    {
        ArgumentNullException.ThrowIfNull(source);

        var hits = new List<SearchHit>();
        if (!TryParsePattern(hexQuery, out var pattern, out var mask) || pattern.Length == 0)
        {
            return hits;
        }

        const int chunkSize = 1024 * 1024;
        var overlap = pattern.Length - 1;
        long offset = 0;

        while (offset < source.Length && hits.Count < _limits.MaxSearchResults)
        {
            var buffer = source.ReadRange(offset, chunkSize + overlap);
            if (buffer.Length < pattern.Length)
            {
                break;
            }

            for (var i = 0; i + pattern.Length <= buffer.Length; i++)
            {
                if (!Matches(buffer, i, pattern, mask))
                {
                    continue;
                }

                var absolute = offset + i;
                hits.Add(new SearchHit(
                    $"0x{absolute:X8}",
                    "byte match",
                    HexDumpService.ToHexString(buffer.AsSpan(i, pattern.Length)),
                    "Hex",
                    absolute,
                    null));

                if (hits.Count >= _limits.MaxSearchResults)
                {
                    break;
                }
            }

            offset += chunkSize;
        }

        return hits;
    }

    private static bool Matches(byte[] buffer, int index, byte[] pattern, bool[] mask)
    {
        for (var k = 0; k < pattern.Length; k++)
        {
            if (mask[k] && buffer[index + k] != pattern[k])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Parses "4D 5A ?? 00" style input into bytes plus a per-byte compare mask.</summary>
    public static bool TryParsePattern(string? input, out byte[] pattern, out bool[] mask)
    {
        pattern = Array.Empty<byte>();
        mask = Array.Empty<bool>();

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace(":", string.Empty)
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (cleaned.Length == 0 || cleaned.Length % 2 != 0)
        {
            return false;
        }

        var bytes = new byte[cleaned.Length / 2];
        var flags = new bool[bytes.Length];

        for (var i = 0; i < bytes.Length; i++)
        {
            var pair = cleaned.Substring(i * 2, 2);
            if (pair is "??" or "**")
            {
                bytes[i] = 0;
                flags[i] = false;
                continue;
            }

            if (!byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            bytes[i] = value;
            flags[i] = true;
        }

        pattern = bytes;
        mask = flags;
        return true;
    }
}
