using RPEReader.Core.Models;
using RPEReader.Core.Services;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests.Parsing;

/// <summary>
/// Damaged and hostile inputs. In every case the requirement is the same: report
/// the problem, never throw out of the service, and never leave the user without
/// at least the generic inspector.
/// </summary>
public sealed class CorruptFileTests : IDisposable
{
    private readonly SampleBuilder _samples = new();
    private readonly RpeFileService _service = new();

    [Fact]
    public async Task Empty_file_is_reported_not_thrown()
    {
        var result = await _service.OpenAsync(_samples.CreateEmptyRpe());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("empty"));
    }

    [Fact]
    public async Task Missing_file_is_reported_not_thrown()
    {
        var result = await _service.OpenAsync(Path.Combine(_samples.Root, "does-not-exist.rpe"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Corrupt_container_still_yields_a_generic_inspection()
    {
        var result = await _service.OpenAsync(_samples.CreateCorruptZipRpe());

        Assert.True(result.Success);
        Assert.Equal("generic", result.Document!.FormatId);
        Assert.True(result.Document.Incomplete);
    }

    [Fact]
    public async Task Malformed_inner_xml_falls_back_to_the_generic_inspector()
    {
        var result = await _service.OpenAsync(_samples.CreateMalformedXmlRpe());

        Assert.True(result.Success);
        Assert.Equal("generic", result.Document!.FormatId);
        Assert.Contains(result.Diagnostics, d =>
            d.Severity == DiagnosticSeverity.Error && d.Message.Contains("well formed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task External_entities_are_not_expanded()
    {
        var result = await _service.OpenAsync(_samples.CreateXxeRpe());

        // Whatever the outcome, no content of the referenced file may appear.
        var everything = string.Join('\n', result.Diagnostics.Select(d => d.Message))
                         + (result.Document?.RawText ?? string.Empty)
                         + string.Join('\n', result.Document?.Root.DescendantsAndSelf()
                             .SelectMany(n => n.Fields)
                             .Select(f => f.Value) ?? Array.Empty<string?>());

        Assert.DoesNotContain("root:x:0:0", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("/bin/bash", everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_zip_entry_using_path_traversal_is_not_parsed_as_an_export()
    {
        var result = await _service.OpenAsync(_samples.CreateTraversalRpe());

        // The traversal-named entry must not be accepted as the export document.
        Assert.True(result.Success);
        Assert.Equal("generic", result.Document!.FormatId);
    }

    [Fact]
    public async Task Oversized_files_are_refused_before_parsing()
    {
        var path = _samples.NewPath("big.rpe");
        await using (var file = File.Create(path))
        {
            file.SetLength(4096);
        }

        var service = new RpeFileService(limits: new RpeLimits { MaxFileBytes = 1024 });
        var result = await service.OpenAsync(path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("limit this build will open"));
    }

    [Fact]
    public async Task A_highly_compressible_container_trips_the_bomb_guard()
    {
        var path = _samples.CreateHighlyCompressibleRpe(megabytes: 24);

        var service = new RpeFileService(limits: new RpeLimits
        {
            MaxTotalUncompressedBytes = 8 * 1024 * 1024,
            MaxEntryUncompressedBytes = 8 * 1024 * 1024
        });

        var result = await service.OpenAsync(path);

        Assert.Contains(result.Diagnostics, d =>
            d.Severity == DiagnosticSeverity.Warning &&
            (d.Message.Contains("per-entry limit") || d.Message.Contains("decompression bomb") || d.Message.Contains("total limit")));
    }

    [Fact]
    public async Task Unknown_binary_is_inspected_rather_than_rejected()
    {
        var result = await _service.OpenAsync(_samples.CreateBinaryRpe());

        Assert.True(result.Success);
        Assert.Equal("generic", result.Document!.FormatId);

        var sections = result.Document.Root.Children.Select(c => c.Name).ToList();
        Assert.Contains("File signature", sections);
        Assert.Contains("Entropy", sections);
        Assert.Contains("Encoding", sections);
        Assert.Contains("Printable strings", sections);
    }

    [Fact]
    public async Task Opening_a_file_leaves_it_unmodified()
    {
        var path = _samples.CreateOpcRouterRpe();
        var before = File.GetLastWriteTimeUtc(path);
        var bytesBefore = await File.ReadAllBytesAsync(path);

        await _service.OpenAsync(path);

        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(path));
    }

    public void Dispose() => _samples.Dispose();
}
