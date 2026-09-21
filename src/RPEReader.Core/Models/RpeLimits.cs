namespace RPEReader.Core.Models;

/// <summary>
/// Hard caps applied to every parse. They exist to keep a hostile or corrupt
/// file from exhausting memory or CPU; none of them can be disabled from within
/// a file being read.
/// </summary>
public sealed class RpeLimits
{
    /// <summary>Largest file the application will open at all.</summary>
    public long MaxFileBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>Largest total uncompressed size accepted from a container.</summary>
    public long MaxTotalUncompressedBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>Largest single entry accepted from a container.</summary>
    public long MaxEntryUncompressedBytes { get; init; } = 128L * 1024 * 1024;

    /// <summary>Maximum entries accepted in a container.</summary>
    public int MaxEntryCount { get; init; } = 2_000;

    /// <summary>
    /// Maximum tolerated uncompressed:compressed ratio before a container is
    /// treated as a decompression bomb.
    /// </summary>
    public int MaxCompressionRatio { get; init; } = 200;

    /// <summary>Maximum XML element nesting depth.</summary>
    public int MaxXmlDepth { get; init; } = 200;

    /// <summary>Maximum number of tree nodes materialised from one document.</summary>
    public int MaxNodes { get; init; } = 500_000;

    /// <summary>Maximum fields kept on a single node.</summary>
    public int MaxFieldsPerNode { get; init; } = 1_000;

    /// <summary>Maximum characters retained for a single field value.</summary>
    public int MaxFieldValueChars { get; init; } = 32 * 1024;

    /// <summary>Maximum characters retained for the Raw Text viewer.</summary>
    public int MaxRawTextChars { get; init; } = 8 * 1024 * 1024;

    /// <summary>Maximum rows rendered into a grid or exported per batch.</summary>
    public int MaxDisplayRows { get; init; } = 100_000;

    /// <summary>Maximum search hits collected before the search stops early.</summary>
    public int MaxSearchResults { get; init; } = 5_000;

    public static RpeLimits Default { get; } = new();
}
