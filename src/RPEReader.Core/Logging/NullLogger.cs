using RPEReader.Core.Abstractions;

namespace RPEReader.Core.Logging;

/// <summary>A logger that discards everything. Used by tests and headless code.</summary>
public sealed class NullLogger : IAppLogger
{
    public static NullLogger Instance { get; } = new();

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
