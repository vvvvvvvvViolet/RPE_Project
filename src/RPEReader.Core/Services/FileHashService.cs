using System.Security.Cryptography;

namespace RPEReader.Core.Services;

/// <summary>Computes file hashes by streaming, so file size does not drive memory use.</summary>
public static class FileHashService
{
    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 1024,
            useAsync: true);

        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeSha256(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var position = stream.CanSeek ? stream.Position : 0;
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);

        if (stream.CanSeek)
        {
            stream.Seek(position, SeekOrigin.Begin);
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
