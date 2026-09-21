using RPEReader.Core.Services;

namespace RPEReader.App.ViewModels;

/// <summary>One rendered hex-dump row.</summary>
public sealed class HexLineViewModel
{
    public HexLineViewModel(HexDumpLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Offset = line.OffsetText;
        Hex = line.HexText;
        Ascii = line.AsciiText;
        RawOffset = line.Offset;
    }

    public string Offset { get; }

    public string Hex { get; }

    public string Ascii { get; }

    public long RawOffset { get; }
}
