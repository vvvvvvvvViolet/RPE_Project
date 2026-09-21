using RPEReader.Core.Models;

namespace RPEReader.Core.Export;

/// <summary>Writes a parsed document out in one of the supported formats.</summary>
public interface IRpeExporter
{
    /// <summary>Display name, e.g. "JSON".</summary>
    string Name { get; }

    /// <summary>Extension including the dot, e.g. ".json".</summary>
    string Extension { get; }

    /// <summary>File-dialog filter fragment, e.g. "JSON file (*.json)|*.json".</summary>
    string FileFilter { get; }

    void Export(RpeDocument document, RpeFileInfo fileInfo, TextWriter writer, RpeLimits limits);
}
