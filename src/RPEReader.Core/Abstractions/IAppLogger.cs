namespace RPEReader.Core.Abstractions;

/// <summary>
/// Application log sink. Implementations must never write file payload data:
/// callers pass file names and diagnostic text only.
/// </summary>
public interface IAppLogger
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
