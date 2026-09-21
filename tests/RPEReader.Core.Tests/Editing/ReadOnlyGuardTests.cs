using RPEReader.Core.Models;
using RPEReader.Core.Parsing;
using RPEReader.Core.Services;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests.Editing;

/// <summary>
/// Editing is offered only where this build can prove it knows how to write the
/// file back. Everything else stays read-only.
/// </summary>
public sealed class ReadOnlyGuardTests : IDisposable
{
    private readonly SampleBuilder _samples = new();
    private readonly RpeFileService _service = new();

    [Fact]
    public async Task A_generically_inspected_file_offers_no_editor()
    {
        var result = await _service.OpenAsync(_samples.CreateBinaryRpe());

        Assert.True(result.Success);
        Assert.Equal("generic", result.Document!.FormatId);
        Assert.False(result.Document.IsEditable);
        Assert.Null(result.Document.Editor);
    }

    [Fact]
    public async Task A_corrupt_container_offers_no_editor()
    {
        var result = await _service.OpenAsync(_samples.CreateCorruptZipRpe());

        Assert.True(result.Success);
        Assert.False(result.Document!.IsEditable);
    }

    [Fact]
    public async Task A_document_truncated_by_a_limit_offers_no_editor()
    {
        // A node cap makes the read incomplete, so writing it back could drop
        // whatever the cap cut off.
        var service = new RpeFileService(limits: new RpeLimits { MaxNodes = 3 });
        var result = await service.OpenAsync(_samples.CreateOpcRouterRpe());

        Assert.True(result.Success);
        Assert.Equal("opcrouter4.zip", result.Document!.FormatId);
        Assert.True(result.Document.Incomplete);
        Assert.False(result.Document.IsEditable);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("open read-only"));
    }

    [Fact]
    public void Only_the_opc_router_parser_declares_write_support()
    {
        var registry = RpeParserRegistry.CreateDefault();

        Assert.True(registry.Parsers.Single(p => p.FormatId == "opcrouter4.zip").SupportsWriting);
        Assert.False(registry.Parsers.Single(p => p.FormatId == "generic").SupportsWriting);
    }

    [Fact]
    public async Task A_value_shortened_for_display_is_not_editable()
    {
        // With a tiny value cap the long assembly-qualified Type strings and the
        // longer element values get clamped, and clamped values must not be
        // written back or the edit would truncate the file.
        var service = new RpeFileService(limits: new RpeLimits { MaxFieldValueChars = 8 });
        var result = await service.OpenAsync(_samples.CreateOpcRouterRpe());

        var clamped = result.Document!.Root.DescendantsAndSelf()
            .SelectMany(n => n.Fields)
            .Where(f => f.Value is not null && f.Value.Contains("truncated at", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(clamped);
        Assert.All(clamped, f => Assert.False(f.Editable));
    }

    public void Dispose() => _samples.Dispose();
}
