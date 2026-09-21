namespace RPEReader.Core.Detection;

/// <summary>Known magic byte sequences used during format detection.</summary>
public static class FileSignatures
{
    /// <summary>Local file header of a ZIP entry: "PK\x03\x04".</summary>
    public static ReadOnlySpan<byte> ZipLocalFileHeader => new byte[] { 0x50, 0x4B, 0x03, 0x04 };

    /// <summary>Empty-archive end-of-central-directory record: "PK\x05\x06".</summary>
    public static ReadOnlySpan<byte> ZipEmptyArchive => new byte[] { 0x50, 0x4B, 0x05, 0x06 };

    /// <summary>Spanned/split archive marker: "PK\x07\x08".</summary>
    public static ReadOnlySpan<byte> ZipSpanned => new byte[] { 0x50, 0x4B, 0x07, 0x08 };

    public static ReadOnlySpan<byte> Utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };

    public static ReadOnlySpan<byte> Utf16LeBom => new byte[] { 0xFF, 0xFE };

    public static ReadOnlySpan<byte> Utf16BeBom => new byte[] { 0xFE, 0xFF };

    public static ReadOnlySpan<byte> GZip => new byte[] { 0x1F, 0x8B };

    /// <summary>SQLite database header text.</summary>
    public static ReadOnlySpan<byte> SQLite => "SQLite format 3\0"u8;

    /// <summary>Windows/DOS executable.</summary>
    public static ReadOnlySpan<byte> MzExecutable => new byte[] { 0x4D, 0x5A };

    public static bool StartsWith(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) =>
        data.Length >= signature.Length && data[..signature.Length].SequenceEqual(signature);
}
