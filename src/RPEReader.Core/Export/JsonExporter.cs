using System.Text.Json;
using RPEReader.Core.Models;

namespace RPEReader.Core.Export;

/// <summary>
/// Writes the document as JSON using <see cref="Utf8JsonWriter"/>. Nothing is
/// deserialised anywhere in this application, and no polymorphic type handling
/// is enabled.
/// </summary>
public sealed class JsonExporter : IRpeExporter
{
    public string Name => "JSON";

    public string Extension => ".json";

    public string FileFilter => "JSON file (*.json)|*.json";

    public void Export(RpeDocument document, RpeFileInfo fileInfo, TextWriter writer, RpeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(limits);

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();

            json.WriteString("exportedByUtc", DateTime.UtcNow.ToString("O"));
            json.WriteString("tool", "RPE Reader 1.0.0");
            json.WriteString("formatId", document.FormatId);
            json.WriteString("formatName", document.FormatDisplayName);
            json.WriteBoolean("incomplete", document.Incomplete);

            if (fileInfo is not null)
            {
                json.WriteStartObject("file");
                json.WriteString("name", fileInfo.FileName);
                json.WriteString("path", fileInfo.FullPath);
                json.WriteNumber("sizeBytes", fileInfo.SizeBytes);
                json.WriteString("lastWriteTimeUtc", fileInfo.LastWriteTimeUtc.ToString("O"));
                json.WriteString("sha256", fileInfo.Sha256);
                json.WriteEndObject();
            }

            json.WriteStartArray("summary");
            foreach (var field in document.Summary)
            {
                WriteField(json, field);
            }

            json.WriteEndArray();

            json.WritePropertyName("tree");
            var written = 0;
            WriteNode(json, document.Root, limits, ref written);

            json.WriteEndObject();
        }

        writer.Write(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    private static void WriteField(Utf8JsonWriter json, RpeField field)
    {
        json.WriteStartObject();
        json.WriteString("name", field.Name);
        json.WriteString("value", field.Value);
        if (field.TypeHint is not null)
        {
            json.WriteString("declaredType", field.TypeHint);
        }

        if (field.Note is not null)
        {
            json.WriteString("note", field.Note);
        }

        json.WriteEndObject();
    }

    private static void WriteNode(Utf8JsonWriter json, RpeNode node, RpeLimits limits, ref int written)
    {
        json.WriteStartObject();
        json.WriteString("name", node.Name);

        if (node.Category is not null)
        {
            json.WriteString("category", node.Category);
        }

        if (node.Value is not null)
        {
            json.WriteString("value", node.Value);
        }

        if (node.Truncated)
        {
            json.WriteBoolean("truncated", true);
        }

        if (node.Fields.Count > 0)
        {
            json.WriteStartArray("fields");
            foreach (var field in node.Fields)
            {
                WriteField(json, field);
            }

            json.WriteEndArray();
        }

        written++;

        if (node.Children.Count > 0)
        {
            json.WriteStartArray("children");
            foreach (var child in node.Children)
            {
                if (written >= limits.MaxNodes)
                {
                    json.WriteStartObject();
                    json.WriteString("note", $"Export stopped at the {limits.MaxNodes:N0}-node limit.");
                    json.WriteEndObject();
                    break;
                }

                WriteNode(json, child, limits, ref written);
            }

            json.WriteEndArray();
        }

        json.WriteEndObject();
    }
}
