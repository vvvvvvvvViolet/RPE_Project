using RPEReader.Core.Services;

namespace RPEReader.Core.Services;

/// <summary>
/// Random-access window onto a file for the hex viewer. The file is opened
/// read-only with sharing, and only the requested page is ever held in memory,
/// so a multi-gigabyte file costs the same as a small one.
/// </summary>
public sealed class HexViewSource : IDisposable
{
    private readonly FileStream _stream;
    private bool _disposed;

    private HexViewSource(FileStream stream, long length)
    {
        _stream = stream;
        Length = length;
    }

    public long Length { get; }

    public long TotalLines => Length == 0 ? 0 : (Length + HexDumpService.BytesPerLine - 1) / HexDumpService.BytesPerLine;

    /// <summary>Opens a file for read-only random access. The caller owns the result.</summary>
    public static HexViewSource Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            FileOptions.RandomAccess);

        return new HexViewSource(stream, stream.Length);
    }

    /// <summary>Reads <paramref name="count"/> bytes from <paramref name="offset"/>, clamped to the file.</summary>
    public byte[] ReadRange(long offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (offset < 0 || offset >= Length || count <= 0)
        {
            return Array.Empty<byte>();
        }

        var toRead = (int)Math.Min(count, Length - offset);
        var buffer = new byte[toRead];
        _stream.Seek(offset, SeekOrigin.Begin);
        _stream.ReadExactly(buffer, 0, toRead);
        return buffer;
    }

    /// <summary>Renders one page of hex-dump lines beginning at <paramref name="firstLine"/>.</summary>
    public IReadOnlyList<HexDumpLine> ReadPage(long firstLine, int lineCount)
    {
        if (firstLine < 0 || lineCount <= 0)
        {
            return Array.Empty<HexDumpLine>();
        }

        var offset = firstLine * HexDumpService.BytesPerLine;
        var bytes = ReadRange(offset, lineCount * HexDumpService.BytesPerLine);
        return bytes.Length == 0 ? Array.Empty<HexDumpLine>() : HexDumpService.Render(bytes, offset);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }
}
