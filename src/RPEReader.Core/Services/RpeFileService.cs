using RPEReader.Core.Abstractions;
using RPEReader.Core.Detection;
using RPEReader.Core.Models;
using RPEReader.Core.Parsing;

namespace RPEReader.Core.Services;

/// <summary>Everything produced by opening one file.</summary>
public sealed class RpeOpenResult
{
    public required bool Success { get; init; }

    public required RpeFileInfo? FileInfo { get; init; }

    public required RpeDocument? Document { get; init; }

    public required IReadOnlyList<ParseDiagnostic> Diagnostics { get; init; }

    public string? SelectedParser { get; init; }
}

/// <summary>
/// Opens a .rpe file read-only, verifies it against the safety limits, picks a
/// parser and returns the parsed document. The source file is never modified:
/// every handle is opened <see cref="FileAccess.Read"/>.
/// </summary>
public sealed class RpeFileService
{
    private readonly RpeParserRegistry _registry;
    private readonly RpeLimits _limits;
    private readonly IAppLogger? _logger;

    public RpeFileService(RpeParserRegistry? registry = null, RpeLimits? limits = null, IAppLogger? logger = null)
    {
        _registry = registry ?? RpeParserRegistry.CreateDefault();
        _limits = limits ?? RpeLimits.Default;
        _logger = logger;
    }

    public RpeLimits Limits => _limits;

    public async Task<RpeOpenResult> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var diagnostics = new List<ParseDiagnostic>();

        FileInfo info;
        try
        {
            info = new FileInfo(path);
            if (!info.Exists)
            {
                return Fail(diagnostics, $"The file '{Path.GetFileName(path)}' does not exist.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _logger?.Error($"Could not stat '{Path.GetFileName(path)}'.", ex);
            return Fail(diagnostics, $"The file could not be inspected: {ex.Message}");
        }

        if (info.Length == 0)
        {
            return Fail(diagnostics, "The file is empty (0 bytes).");
        }

        if (info.Length > _limits.MaxFileBytes)
        {
            return Fail(diagnostics,
                $"The file is {RpeFileInfo.FormatSize(info.Length)}, above the " +
                $"{RpeFileInfo.FormatSize(_limits.MaxFileBytes)} limit this build will open.");
        }

        try
        {
            var sha = await FileHashService.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 256 * 1024,
                FileOptions.SequentialScan);

            var probe = RpeProbeFactory.Create(stream, info.Name, info.Length, _limits);
            var parser = _registry.Select(probe);

            _logger?.Info($"Opening '{info.Name}' ({RpeFileInfo.FormatSize(info.Length)}) with parser '{parser.FormatId}'.");

            var context = new RpeParseContext
            {
                Probe = probe,
                Limits = _limits,
                CancellationToken = cancellationToken
            };

            ParseResult result;
            try
            {
                result = parser.Parse(stream, context);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A parser fault must not take the application down, and it must
                // not hide the file from the user: fall back to the inspector.
                _logger?.Error($"Parser '{parser.FormatId}' failed on '{info.Name}'.", ex);
                diagnostics.Add(ParseDiagnostic.Error(
                    $"The '{parser.DisplayName}' parser failed ({ex.GetType().Name}). Falling back to the generic inspector."));

                result = new GenericRpeParser().Parse(stream, context);
                parser = _registry.Parsers.Last();
            }

            diagnostics.AddRange(result.Diagnostics);

            if (!result.Success || result.Document is null)
            {
                // A structural parser that refuses the file still leaves the
                // generic inspector as a usable view.
                diagnostics.Add(ParseDiagnostic.Info("Falling back to the generic inspector."));
                var fallback = new GenericRpeParser().Parse(stream, context);
                diagnostics.AddRange(fallback.Diagnostics);

                if (!fallback.Success || fallback.Document is null)
                {
                    return Fail(diagnostics, "The file could not be read by any parser in this build.");
                }

                return new RpeOpenResult
                {
                    Success = true,
                    FileInfo = BuildFileInfo(info, sha),
                    Document = fallback.Document,
                    Diagnostics = diagnostics,
                    SelectedParser = fallback.Document.FormatDisplayName
                };
            }

            return new RpeOpenResult
            {
                Success = true,
                FileInfo = BuildFileInfo(info, sha),
                Document = result.Document,
                Diagnostics = diagnostics,
                SelectedParser = parser.DisplayName
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error($"Access denied reading '{info.Name}'.", ex);
            return Fail(diagnostics, "Access to the file was denied. Check the file permissions and try again.");
        }
        catch (IOException ex)
        {
            _logger?.Error($"I/O error reading '{info.Name}'.", ex);
            return Fail(diagnostics, $"The file could not be read: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.Error($"Unexpected failure reading '{info.Name}'.", ex);
            return Fail(diagnostics, $"An unexpected error occurred while reading the file: {ex.GetType().Name}.");
        }
    }

    private static RpeFileInfo BuildFileInfo(FileInfo info, string sha) => new()
    {
        FileName = info.Name,
        FullPath = info.FullName,
        SizeBytes = info.Length,
        LastWriteTimeUtc = info.LastWriteTimeUtc,
        CreationTimeUtc = info.CreationTimeUtc,
        Sha256 = sha
    };

    private static RpeOpenResult Fail(List<ParseDiagnostic> diagnostics, string message)
    {
        diagnostics.Add(ParseDiagnostic.Error(message));
        return new RpeOpenResult
        {
            Success = false,
            FileInfo = null,
            Document = null,
            Diagnostics = diagnostics
        };
    }
}
