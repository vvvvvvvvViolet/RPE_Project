using RPEReader.Core.Detection;
using RPEReader.Core.Models;
using RPEReader.Core.Parsing;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests.Detection;

public sealed class DetectionTests : IDisposable
{
    private readonly SampleBuilder _samples = new();

    [Fact]
    public void Probe_identifies_an_opc_router_container()
    {
        var path = _samples.CreateOpcRouterRpe();
        using var stream = File.OpenRead(path);

        var probe = RpeProbeFactory.Create(stream, Path.GetFileName(path), stream.Length, RpeLimits.Default);

        Assert.True(probe.IsZipContainer);
        Assert.True(probe.HasEntry("OpcRouter4.xml"));
        Assert.False(probe.LooksLikeXml);
    }

    [Fact]
    public void Registry_selects_the_opc_router_parser_for_a_matching_file()
    {
        var path = _samples.CreateOpcRouterRpe();
        using var stream = File.OpenRead(path);
        var probe = RpeProbeFactory.Create(stream, Path.GetFileName(path), stream.Length, RpeLimits.Default);

        var parser = RpeParserRegistry.CreateDefault().Select(probe);

        Assert.Equal("opcrouter4.zip", parser.FormatId);
    }

    [Fact]
    public void Registry_falls_back_to_the_generic_parser_for_an_unknown_zip()
    {
        var path = _samples.CreateUnknownZipRpe();
        using var stream = File.OpenRead(path);
        var probe = RpeProbeFactory.Create(stream, Path.GetFileName(path), stream.Length, RpeLimits.Default);

        var parser = RpeParserRegistry.CreateDefault().Select(probe);

        Assert.Equal("generic", parser.FormatId);
    }

    [Fact]
    public void Registry_falls_back_to_the_generic_parser_for_unrecognised_binary()
    {
        var path = _samples.CreateBinaryRpe();
        using var stream = File.OpenRead(path);
        var probe = RpeProbeFactory.Create(stream, Path.GetFileName(path), stream.Length, RpeLimits.Default);

        Assert.False(probe.IsZipContainer);
        Assert.Equal("generic", RpeParserRegistry.CreateDefault().Select(probe).FormatId);
    }

    [Fact]
    public void Probe_of_a_corrupt_container_does_not_throw_and_lists_no_entries()
    {
        var path = _samples.CreateCorruptZipRpe();
        using var stream = File.OpenRead(path);

        var probe = RpeProbeFactory.Create(stream, Path.GetFileName(path), stream.Length, RpeLimits.Default);

        Assert.True(probe.IsZipContainer);
        Assert.Empty(probe.ContainerEntryNames);
    }

    [Theory]
    [InlineData("OpcRouter4.xml", true)]
    [InlineData("nested/file.xml", true)]
    [InlineData("../escape.xml", false)]
    [InlineData("a/../../escape.xml", false)]
    [InlineData("/absolute.xml", false)]
    [InlineData("\\absolute.xml", false)]
    [InlineData("C:/windows/system32/evil.dll", false)]
    [InlineData("", false)]
    public void Entry_names_are_screened_for_traversal(string name, bool expected)
    {
        Assert.Equal(expected, SafeZipReader.IsSafeEntryName(name));
    }

    public void Dispose() => _samples.Dispose();
}
