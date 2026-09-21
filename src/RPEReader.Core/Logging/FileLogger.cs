using System.Text;
using RPEReader.Core.Abstractions;

namespace RPEReader.Core.Logging;

/// <summary>
/// Appends diagnostics to a dated file under a <c>Logs</c> folder next to the
/// executable, falling back to %LOCALAPPDATA% when that folder is not writable.
/// </summary>
/// <remarks>
/// The logger records file names, sizes and error text only. It never writes
/// values read out of a .rpe file, so a log can be shared without leaking the
/// endpoints, user names or tag paths a project export contains.
/// </remarks>
public sealed class FileLogger : IAppLogger, IDisposable
{
    private readonly object _gate = new();
    private readonly string? _logDirectory;
    private readonly long _maxBytes;
    private bool _disabled;

    public FileLogger(string? logDirectory = null, long maxBytes = 5 * 1024 * 1024)
    {
        _maxBytes = maxBytes;
        _logDirectory = ResolveDirectory(logDirectory);
        _disabled = _logDirectory is null;
    }

    /// <summary>The folder logs are written to, or <c>null</c> when logging is disabled.</summary>
    public string? LogDirectory => _logDirectory;

    public string? CurrentLogFile => _logDirectory is null
        ? null
        : Path.Combine(_logDirectory, $"rpereader-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string message) => Write("INFO ", message, null);

    public void Warn(string message) => Write("WARN ", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        if (_disabled || _logDirectory is null)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(' ')
            .Append(level)
            .Append(' ')
            .Append(Sanitise(message));

        if (exception is not null)
        {
            // Type and message only; a stack trace is kept because it names our
            // own code, not file content.
            builder.AppendLine()
                .Append("    ")
                .Append(exception.GetType().FullName)
                .Append(": ")
                .Append(Sanitise(exception.Message));

            if (exception.StackTrace is not null)
            {
                builder.AppendLine().Append(exception.StackTrace);
            }
        }

        builder.AppendLine();

        lock (_gate)
        {
            try
            {
                var path = CurrentLogFile!;
                RollIfNeeded(path);
                File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception)
            {
                // Logging must never become the reason the application fails.
                _disabled = true;
            }
        }
    }

    private void RollIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < _maxBytes)
        {
            return;
        }

        var rolled = Path.Combine(
            Path.GetDirectoryName(path)!,
            $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.Now:HHmmss}.old.log");

        File.Move(path, rolled, overwrite: true);
    }

    /// <summary>Collapses newlines so one event stays on one line, and caps length.</summary>
    private static string Sanitise(string message)
    {
        const int max = 2000;
        var text = message.Replace("\r", " ").Replace("\n", " ");
        return text.Length <= max ? text : text[..max] + "…";
    }

    private static string? ResolveDirectory(string? requested)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(requested))
        {
            candidates.Add(requested);
        }
        else
        {
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Logs"));
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RPEReader",
                "Logs"));
        }

        foreach (var candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);

                // Prove it is actually writable before committing to it.
                var probe = Path.Combine(candidate, $".write-probe-{Guid.NewGuid():N}");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return candidate;
            }
            catch (Exception)
            {
                // Try the next candidate.
            }
        }

        return null;
    }

    public void Dispose()
    {
        // Nothing buffered; present so callers can treat the logger uniformly.
    }
}
