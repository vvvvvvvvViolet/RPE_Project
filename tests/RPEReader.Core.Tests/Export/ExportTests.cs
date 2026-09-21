using System.Text.Json;
using RPEReader.Core.Export;
using RPEReader.Core.Models;
using RPEReader.Core.Services;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests.Export;

public sealed class ExportTests : IDisposable
{
    private readonly SampleBuilder _samples = new();
    private readonly RpeFileService _service = new();

    private async Task<(RpeDocument Document, RpeFileInfo Info)> LoadAsync()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());
        Assert.True(result.Success);
        return (result.Document!, result.FileInfo!);
    }

    [Fact]
    public async Task Json_export_is_well_formed_and_carries_file_identity()
    {
        var (document, info) = await LoadAsync();

        var writer = new StringWriter();
        new JsonExporter().Export(document, info, writer, RpeLimits.Default);

        using var parsed = JsonDocument.Parse(writer.ToString());
        var root = parsed.RootElement;

        Assert.Equal("opcrouter4.zip", root.GetProperty("formatId").GetString());
        Assert.Equal(info.Sha256, root.GetProperty("file").GetProperty("sha256").GetString());
        Assert.True(root.GetProperty("tree").GetProperty("children").GetArrayLength() > 0);
    }

    /// <summary>
    /// Exercised with both newline conventions: <see cref="TextWriter.WriteLine()"/>
    /// emits the platform newline, so a test that assumes one of them passes on
    /// Linux and fails on Windows.
    /// </summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Csv_export_has_a_header_and_one_row_per_field(string newLine)
    {
        var (document, info) = await LoadAsync();

        var writer = new StringWriter { NewLine = newLine };
        new CsvExporter().Export(document, info, writer, RpeLimits.Default);

        var lines = SplitLines(writer.ToString());

        Assert.Equal("Path,Node,Category,Field,Value,DeclaredType,Note", lines[0]);
        Assert.True(lines.Length > 5, $"expected more than 5 rows, got {lines.Length}");

        // Every data row must carry exactly the seven columns the header declares.
        // Commas inside quoted values must not be counted, so the row is parsed
        // rather than having its commas tallied.
        Assert.All(lines.Skip(1), line => Assert.Equal(7, ParseCsvRow(line).Count));
    }

    private static string[] SplitLines(string text) =>
        text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Minimal RFC 4180 row splitter: quoted fields, "" as an escaped quote.</summary>
    private static List<string> ParseCsvRow(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c != '"')
                {
                    current.Append(c);
                }
                else if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    [Theory]
    [InlineData("=cmd|'/c calc'!A1", "\"'=cmd|'/c calc'!A1\"")]
    [InlineData("+1234", "\"'+1234\"")]
    [InlineData("-1234", "\"'-1234\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    [InlineData("plain", "\"plain\"")]
    public void Csv_export_neutralises_formula_injection(string input, string expected)
    {
        Assert.Equal(expected, CsvExporter.Escape(input));
    }

    [Fact]
    public void Csv_export_doubles_embedded_quotes()
    {
        Assert.Equal("\"say \"\"hi\"\"\"", CsvExporter.Escape("say \"hi\""));
    }

    [Fact]
    public async Task Text_export_includes_the_header_block_and_structure()
    {
        var (document, info) = await LoadAsync();

        var writer = new StringWriter();
        new TextExporter().Export(document, info, writer, RpeLimits.Default);
        var text = writer.ToString();

        Assert.Contains("RPE Reader", text);
        Assert.Contains(info.Sha256, text);
        Assert.Contains("SUMMARY", text);
        Assert.Contains("STRUCTURE", text);
        Assert.Contains("Plugins", text);
    }

    [Fact]
    public async Task Exports_respect_the_node_limit()
    {
        var (document, info) = await LoadAsync();
        var limits = new RpeLimits { MaxNodes = 2 };

        var writer = new StringWriter();
        new TextExporter().Export(document, info, writer, limits);

        Assert.Contains("node limit", writer.ToString());
    }

    [Fact]
    public async Task Generic_inspection_also_exports_cleanly()
    {
        var result = await _service.OpenAsync(_samples.CreateBinaryRpe());

        var writer = new StringWriter();
        new JsonExporter().Export(result.Document!, result.FileInfo!, writer, RpeLimits.Default);

        using var parsed = JsonDocument.Parse(writer.ToString());
        Assert.Equal("generic", parsed.RootElement.GetProperty("formatId").GetString());
    }

    public void Dispose() => _samples.Dispose();
}
