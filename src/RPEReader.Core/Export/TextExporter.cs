using RPEReader.Core.Models;

namespace RPEReader.Core.Export;

/// <summary>Writes an indented, human-readable outline of the document.</summary>
public sealed class TextExporter : IRpeExporter
{
    public string Name => "Text";

    public string Extension => ".txt";

    public string FileFilter => "Text file (*.txt)|*.txt";

    public void Export(RpeDocument document, RpeFileInfo fileInfo, TextWriter writer, RpeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(limits);

        writer.WriteLine("RPE Reader 1.0.0 — export");
        writer.WriteLine($"Generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Format:          {document.FormatDisplayName}");

        if (fileInfo is not null)
        {
            writer.WriteLine($"File:            {fileInfo.FileName}");
            writer.WriteLine($"Path:            {fileInfo.FullPath}");
            writer.WriteLine($"Size:            {fileInfo.SizeDisplay}");
            writer.WriteLine($"Modified (UTC):  {fileInfo.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"SHA-256:         {fileInfo.Sha256}");
        }

        if (document.Incomplete)
        {
            writer.WriteLine("Note:            this document was not read in full; see the messages pane.");
        }

        writer.WriteLine();

        if (document.Summary.Count > 0)
        {
            writer.WriteLine("SUMMARY");
            writer.WriteLine(new string('-', 60));
            foreach (var field in document.Summary)
            {
                writer.WriteLine($"  {field.Name,-28} {field.Value}");
                if (field.Note is not null)
                {
                    writer.WriteLine($"  {string.Empty,-28} ({field.Note})");
                }
            }

            writer.WriteLine();
        }

        writer.WriteLine("STRUCTURE");
        writer.WriteLine(new string('-', 60));

        var written = 0;
        Walk(document.Root, 0, writer, limits, ref written);

        if (written >= limits.MaxNodes)
        {
            writer.WriteLine();
            writer.WriteLine($"[Export stopped at the {limits.MaxNodes:N0}-node limit.]");
        }
    }

    private static void Walk(RpeNode node, int depth, TextWriter writer, RpeLimits limits, ref int written)
    {
        if (written >= limits.MaxNodes)
        {
            return;
        }

        var indent = new string(' ', depth * 2);
        writer.WriteLine(node.Value is null
            ? $"{indent}+ {node.Name}"
            : $"{indent}+ {node.Name}  [{node.Value}]");

        written++;

        foreach (var field in node.Fields)
        {
            writer.WriteLine($"{indent}    {field.Name}: {field.Value}");
            if (field.Note is not null)
            {
                writer.WriteLine($"{indent}      -> {field.Note}");
            }
        }

        if (node.Truncated)
        {
            writer.WriteLine($"{indent}    [truncated by a display limit]");
        }

        foreach (var child in node.Children)
        {
            Walk(child, depth + 1, writer, limits, ref written);
        }
    }
}
