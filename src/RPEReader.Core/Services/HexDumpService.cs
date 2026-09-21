using System.Text;

namespace RPEReader.Core.Services;

/// <summary>One rendered line of a hex dump.</summary>
public sealed record HexDumpLine(long Offset, string OffsetText, string HexText, string AsciiText);

/// <summary>Formats byte ranges as offset / hexadecimal / ASCII lines.</summary>
public static class HexDumpService
{
    public const int BytesPerLine = 16;

    public static string ToHexString(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder(data.Length * 3);
        foreach (var b in data)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(b.ToString("X2"));
        }

        return builder.ToString();
    }

    /// <summary>Renders <paramref name="data"/> as dump lines starting at <paramref name="baseOffset"/>.</summary>
    public static IReadOnlyList<HexDumpLine> Render(ReadOnlySpan<byte> data, long baseOffset)
    {
        var lines = new List<HexDumpLine>((data.Length / BytesPerLine) + 1);
        var hex = new StringBuilder(BytesPerLine * 3);
        var ascii = new StringBuilder(BytesPerLine);

        for (var start = 0; start < data.Length; start += BytesPerLine)
        {
            hex.Clear();
            ascii.Clear();

            var count = Math.Min(BytesPerLine, data.Length - start);
            for (var i = 0; i < BytesPerLine; i++)
            {
                if (i > 0)
                {
                    hex.Append(' ');
                    if (i == BytesPerLine / 2)
                    {
                        hex.Append(' ');
                    }
                }

                if (i < count)
                {
                    var b = data[start + i];
                    hex.Append(b.ToString("X2"));
                    ascii.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
                }
                else
                {
                    hex.Append("  ");
                }
            }

            var offset = baseOffset + start;
            lines.Add(new HexDumpLine(offset, offset.ToString("X8"), hex.ToString(), ascii.ToString()));
        }

        return lines;
    }
}
