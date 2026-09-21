namespace RPEReader.Core.Models;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// A message raised while reading a file. Diagnostics never carry payload data
/// from the file itself, so they are safe to write to the log.
/// </summary>
public sealed class ParseDiagnostic
{
    public ParseDiagnostic(DiagnosticSeverity severity, string message)
    {
        Severity = severity;
        Message = message ?? string.Empty;
    }

    public DiagnosticSeverity Severity { get; }

    public string Message { get; }

    public static ParseDiagnostic Info(string message) => new(DiagnosticSeverity.Info, message);

    public static ParseDiagnostic Warning(string message) => new(DiagnosticSeverity.Warning, message);

    public static ParseDiagnostic Error(string message) => new(DiagnosticSeverity.Error, message);

    public override string ToString() => $"[{Severity}] {Message}";
}
