using RPEReader.Core.Models;
using RPEReader.Core.Parsing.OpcRouter4;
using RPEReader.Core.Services;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests.Parsing;

public sealed class OpcRouter4ParserTests : IDisposable
{
    private readonly SampleBuilder _samples = new();
    private readonly RpeFileService _service = new();

    [Fact]
    public async Task Reads_root_metadata_into_the_summary()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal("opcrouter4.zip", result.Document!.FormatId);

        var summary = result.Document.Summary.ToDictionary(f => f.Name, f => f.Value);
        Assert.Equal("Templates", summary["ExportType"]);
        Assert.Equal("5.6.5002.211", summary["Version"]);
        Assert.Equal("Version_3", summary["FileVersion"]);
        Assert.Equal("unit-test-instance", summary["DisplayName"]);
    }

    [Fact]
    public async Task Counts_connections_plugins_and_lines()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        var summary = result.Document!.Summary.ToDictionary(f => f.Name, f => f.Value);
        Assert.Equal("2", summary["Plug-in configurations"]);
        Assert.Equal("1", summary["Connections"]);
        Assert.Equal("1", summary["Connection groups"]);
        Assert.Equal("1", summary["Connection lines"]);
        Assert.Equal("1", summary["Template variables"]);
    }

    [Fact]
    public async Task Builds_a_tree_containing_the_known_top_level_sections()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        var sections = result.Document!.Root.Children.Select(c => c.Name).ToList();

        Assert.Contains("Plugins", sections);
        Assert.Contains("ConnectionGroups", sections);
        Assert.Contains("Files", sections);
        Assert.All(sections, s => Assert.Contains(s, OpcRouter4Schema.TopLevelSections));
    }

    [Fact]
    public async Task Surfaces_a_transfer_object_item_with_its_direction()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        var item = result.Document!.Root
            .DescendantsAndSelf()
            .SelectMany(n => n.Fields)
            .FirstOrDefault(f => f.Name == "Direction");

        Assert.NotNull(item);
        Assert.Equal("output", item!.Value);
    }

    [Fact]
    public async Task Annotates_a_password_key_as_a_store_reference_only()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        var keyField = result.Document!.Root
            .DescendantsAndSelf()
            .SelectMany(n => n.Fields)
            .FirstOrDefault(f => f.Name == "Key" && f.Value == "EXAMPLE_SECRET_NAME");

        Assert.NotNull(keyField);

        // The PasswordKey element itself becomes a node because it has children.
        var node = result.Document.Root.DescendantsAndSelf()
            .FirstOrDefault(n => n.Fields.Any(f => f.Name == "Store" && f.Value == "internal"));
        Assert.NotNull(node);
    }

    [Fact]
    public async Task Does_not_resolve_declared_dotnet_types()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        var typeField = result.Document!.Root
            .DescendantsAndSelf()
            .SelectMany(n => n.Fields)
            .FirstOrDefault(f => f.Name == "@Type" && (f.Value?.Contains("StaticTransferObjectConfig") ?? false));

        Assert.NotNull(typeField);
        Assert.Contains("not resolved", typeField!.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(5250645522852368705L, 2025)]
    [InlineData(5250941535738654137L, 2026)]
    public void Decodes_ToBinary_timestamps(long raw, int expectedYear)
    {
        Assert.True(DotNetDateTimeDecoder.TryDecode(raw.ToString(), out var value, out var display));
        Assert.Equal(expectedYear, value.Year);
        Assert.NotEmpty(display);
    }

    [Fact]
    public void Treats_the_unset_sentinel_as_not_set()
    {
        Assert.True(DotNetDateTimeDecoder.TryDecode(
            DotNetDateTimeDecoder.UnsetSentinel.ToString(), out _, out var display));
        Assert.Equal("(not set)", display);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("1")]
    public void Rejects_values_that_are_not_plausible_timestamps(string raw)
    {
        Assert.False(DotNetDateTimeDecoder.TryDecode(raw, out _, out _));
    }

    [Fact]
    public async Task Raw_text_exposes_the_inner_xml()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        Assert.Contains("OpcRouter4Export", result.Document!.RawText, StringComparison.Ordinal);
        Assert.False(result.Document.RawTextTruncated);
    }

    public void Dispose() => _samples.Dispose();
}
