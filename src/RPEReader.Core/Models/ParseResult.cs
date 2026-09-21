namespace RPEReader.Core.Models;

/// <summary>Outcome of a parse attempt. Parsers report failure here, not by throwing.</summary>
public sealed class ParseResult
{
    private readonly List<ParseDiagnostic> _diagnostics = new();

    private ParseResult(bool success, RpeDocument? document)
    {
        Success = success;
        Document = document;
    }

    public bool Success { get; }

    public RpeDocument? Document { get; }

    public IReadOnlyList<ParseDiagnostic> Diagnostics => _diagnostics;

    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public bool HasWarnings => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

    public static ParseResult Ok(RpeDocument document) => new(true, document);

    public static ParseResult Failed(string message)
    {
        var r = new ParseResult(false, null);
        r.Add(ParseDiagnostic.Error(message));
        return r;
    }

    public ParseResult Add(ParseDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
        return this;
    }

    public ParseResult AddRange(IEnumerable<ParseDiagnostic> diagnostics)
    {
        foreach (var d in diagnostics)
        {
            _diagnostics.Add(d);
        }

        return this;
    }
}
