using System.Globalization;
using System.Text;
using RPEReader.Core.Models;

namespace RPEReader.Core.Export;

/// <summary>
/// Flattens the tree to one row per field: Path, Node, Category, Field, Value,
/// DeclaredType, Note.
/// </summary>
public sealed class CsvExporter : IRpeExporter
{
    public string Name => "CSV";

    public string Extension => ".csv";

    public string FileFilter => "CSV file (*.csv)|*.csv";

    public void Export(RpeDocument document, RpeFileInfo fileInfo, TextWriter writer, RpeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(limits);

        writer.WriteLine("Path,Node,Category,Field,Value,DeclaredType,Note");

        var rows = 0;
        Walk(document.Root, string.Empty, writer, limits, ref rows);

        if (rows >= limits.MaxDisplayRows)
        {
            writer.WriteLine(Escape($"Export stopped at the {limits.MaxDisplayRows:N0}-row limit.") + ",,,,,,");
        }
    }

    private static void Walk(RpeNode node, string path, TextWriter writer, RpeLimits limits, ref int rows)
    {
        if (rows >= limits.MaxDisplayRows)
        {
            return;
        }

        var here = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";

        if (node.Fields.Count == 0)
        {
            // Still emit a row so structural nodes appear in the output.
            writer.WriteLine(string.Join(',',
                Escape(path),
                Escape(node.Name),
                Escape(node.Category),
                string.Empty,
                Escape(node.Value),
                string.Empty,
                string.Empty));
            rows++;
        }

        foreach (var field in node.Fields)
        {
            if (rows >= limits.MaxDisplayRows)
            {
                return;
            }

            writer.WriteLine(string.Join(',',
                Escape(here),
                Escape(node.Name),
                Escape(node.Category),
                Escape(field.Name),
                Escape(field.Value),
                Escape(field.TypeHint),
                Escape(field.Note)));
            rows++;
        }

        foreach (var child in node.Children)
        {
            Walk(child, here, writer, limits, ref rows);
        }
    }

    /// <summary>
    /// Quotes a CSV value. A leading =, +, - or @ is prefixed with a single
    /// quote so spreadsheet applications treat the cell as text rather than a
    /// formula.
    /// </summary>
    internal static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var text = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');

        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t')
        {
            text = "'" + text;
        }

        var builder = new StringBuilder(text.Length + 2);
        builder.Append('"');
        foreach (var c in text)
        {
            if (c == '"')
            {
                builder.Append('"');
            }

            builder.Append(c);
        }

        builder.Append('"');
        return builder.ToString();
    }
}
