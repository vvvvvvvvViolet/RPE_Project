using RPEReader.Core.Models;
using RPEReader.Core.Services;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests;

public sealed class ServiceTests : IDisposable
{
    private readonly SampleBuilder _samples = new();

    [Fact]
    public async Task Sha256_matches_a_known_value()
    {
        var path = _samples.NewPath("hash.bin");
        await File.WriteAllTextAsync(path, "abc");

        var hash = await FileHashService.ComputeSha256Async(path);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void Hex_dump_renders_offsets_hex_and_ascii()
    {
        var data = "Hello, RPE!"u8.ToArray();

        var lines = HexDumpService.Render(data, 0);

        Assert.Single(lines);
        Assert.Equal("00000000", lines[0].OffsetText);
        Assert.StartsWith("48 65 6C 6C 6F", lines[0].HexText);
        Assert.Equal("Hello, RPE!", lines[0].AsciiText);
    }

    [Fact]
    public void Hex_view_source_pages_without_loading_the_whole_file()
    {
        var path = _samples.NewPath("paged.bin");
        var data = new byte[64 * 1024];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        File.WriteAllBytes(path, data);

        using var source = HexViewSource.Open(path);

        Assert.Equal(data.Length, source.Length);
        Assert.Equal(4096, source.TotalLines);

        var page = source.ReadPage(firstLine: 100, lineCount: 4);
        Assert.Equal(4, page.Count);
        Assert.Equal(100 * 16, page[0].Offset);

        var range = source.ReadRange(100 * 16, 16);
        Assert.Equal(data.Skip(100 * 16).Take(16), range);
    }

    [Fact]
    public void Entropy_separates_uniform_from_random_data()
    {
        var uniform = new byte[4096];
        var random = new byte[4096];
        Random.Shared.NextBytes(random);

        Assert.True(EntropyService.Calculate(uniform) < 0.1);
        Assert.True(EntropyService.Calculate(random) > 7.5);
    }

    [Theory]
    [InlineData("50 4B 03 04", 4)]
    [InlineData("504B0304", 4)]
    [InlineData("50-4B-03-04", 4)]
    [InlineData("50 ?? 03", 3)]
    public void Hex_patterns_parse_in_the_accepted_notations(string input, int expectedLength)
    {
        Assert.True(SearchService.TryParsePattern(input, out var pattern, out var mask));
        Assert.Equal(expectedLength, pattern.Length);
        Assert.Equal(expectedLength, mask.Length);
    }

    [Theory]
    [InlineData("50 4")]
    [InlineData("zz")]
    [InlineData("")]
    public void Malformed_hex_patterns_are_rejected(string input)
    {
        Assert.False(SearchService.TryParsePattern(input, out _, out _));
    }

    [Fact]
    public async Task Hex_search_finds_the_zip_signature_at_offset_zero()
    {
        var path = _samples.CreateOpcRouterRpe();
        using var source = HexViewSource.Open(path);

        var hits = new SearchService().SearchHex(source, "50 4B 03 04");

        Assert.NotEmpty(hits);
        Assert.Equal(0, hits[0].Offset);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Tree_search_matches_field_names_and_values()
    {
        var result = await new RpeFileService().OpenAsync(_samples.CreateOpcRouterRpe());
        var search = new SearchService();

        var byValue = search.SearchTree(result.Document!.Root, "ExampleConnection", SearchMode.Text, caseSensitive: false);
        Assert.NotEmpty(byValue);

        var byFieldName = search.SearchTree(result.Document.Root, "BrokerAddress", SearchMode.FieldName, caseSensitive: false);
        Assert.NotEmpty(byFieldName);
        Assert.All(byFieldName, h => Assert.Equal("Field", h.Kind));

        var noMatch = search.SearchTree(result.Document.Root, "zzz-not-present-zzz", SearchMode.Text, caseSensitive: false);
        Assert.Empty(noMatch);
    }

    [Fact]
    public async Task Search_results_are_capped()
    {
        var result = await new RpeFileService().OpenAsync(_samples.CreateOpcRouterRpe());
        var search = new SearchService(new RpeLimits { MaxSearchResults = 2 });

        var hits = search.SearchTree(result.Document!.Root, "e", SearchMode.Text, caseSensitive: false);

        Assert.True(hits.Count <= 2);
    }

    [Fact]
    public void Encoding_sniffer_recognises_a_utf8_bom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a' };
        Assert.Equal("UTF-8", EncodingSniffer.Sniff(bytes).Name);
    }

    [Fact]
    public void Strings_extractor_finds_printable_runs()
    {
        var bytes = new byte[] { 0x00, 0x01 }
            .Concat("RPEReader"u8.ToArray())
            .Concat(new byte[] { 0x00 })
            .ToArray();

        var strings = StringsExtractor.Extract(bytes, minimumLength: 4);

        Assert.Contains(strings, s => s.Value == "RPEReader");
    }

    public void Dispose() => _samples.Dispose();
}
