using RPEReader.Core.Abstractions;
using RPEReader.Core.Models;

namespace RPEReader.Core.Parsing;

/// <summary>
/// Chooses the parser for a file. Structural parsers are tried in descending
/// priority; <see cref="GenericRpeParser"/> always matches last, so a file is
/// never rejected outright for being unrecognised.
/// </summary>
public sealed class RpeParserRegistry
{
    private readonly List<IRpeParser> _parsers;

    public RpeParserRegistry(IEnumerable<IRpeParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);
        _parsers = parsers.OrderByDescending(p => p.Priority).ToList();

        if (_parsers.Count == 0)
        {
            throw new ArgumentException("At least one parser must be registered.", nameof(parsers));
        }
    }

    /// <summary>The default set shipped with this build.</summary>
    public static RpeParserRegistry CreateDefault() => new(new IRpeParser[]
    {
        new OpcRouter4Parser(),
        new GenericRpeParser()
    });

    public IReadOnlyList<IRpeParser> Parsers => _parsers;

    /// <summary>Returns the highest-priority parser that accepts the probe.</summary>
    public IRpeParser Select(RpeProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        foreach (var parser in _parsers)
        {
            bool accepted;
            try
            {
                accepted = parser.CanParse(probe);
            }
            catch (Exception)
            {
                // A misbehaving parser must not stop the others from being tried.
                accepted = false;
            }

            if (accepted)
            {
                return parser;
            }
        }

        return _parsers[^1];
    }
}
