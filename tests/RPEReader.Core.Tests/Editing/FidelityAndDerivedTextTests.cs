using System.IO.Compression;
using System.Text;
using RPEReader.Core.Editing;
using RPEReader.Core.Models;
using RPEReader.Core.Parsing.OpcRouter4;
using RPEReader.Core.Services;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests.Editing;

public sealed class FidelityAndDerivedTextTests : IDisposable
{
    private readonly SampleBuilder _samples = new();
    private readonly RpeFileService _service = new();

    private static RpeField FieldNamed(RpeDocument document, string name) =>
        document.Root.DescendantsAndSelf().SelectMany(n => n.Fields).First(f => f.Name == name);

    // ------------------------------------------------- per-file fidelity

    [Fact]
    public async Task A_normal_file_reports_exact_round_trip()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        Assert.True(result.Document!.Editor!.RoundTripsExactly);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("byte for byte"));
    }

    /// <summary>
    /// XML readers are required to fold a carriage return in element text to a
    /// line feed, so such a file cannot round-trip byte for byte no matter what
    /// the writer does. The application must measure that and say so rather
    /// than claim a guarantee it cannot keep.
    /// </summary>
    [Fact]
    public async Task A_file_with_carriage_returns_is_flagged_as_not_exact()
    {
        var xml = SampleBuilder.MinimalOpcRouterXml.Replace(
            "<Topic>example/line/1/CycleTime</Topic>",
            "<Topic>line one\r\nline two</Topic>");

        var path = _samples.CreateOpcRouterRpe("crlf.rpe", xml, canonicalise: false);
        var result = await _service.OpenAsync(path);

        Assert.True(result.Success);
        Assert.NotNull(result.Document!.Editor);
        Assert.False(result.Document.Editor!.RoundTripsExactly);

        Assert.Contains(result.Diagnostics, d =>
            d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("byte for byte")
            && d.Message.Contains("carriage returns"));
    }

    [Fact]
    public async Task A_file_that_is_not_exact_can_still_be_edited_and_saved()
    {
        var xml = SampleBuilder.MinimalOpcRouterXml.Replace(
            "<Topic>example/line/1/CycleTime</Topic>",
            "<Topic>line one\r\nline two</Topic>");

        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe("crlf2.rpe", xml, canonicalise: false));
        var editor = result.Document!.Editor!;

        Assert.True(editor.SetValue(FieldNamed(result.Document, "Port"), "8883", "Plugins").IsAccepted);

        var target = _samples.NewPath("crlf-saved.rpe");
        Assert.True(editor.Save(target).Success);

        var reopened = await _service.OpenAsync(target);
        Assert.Equal("8883", FieldNamed(reopened.Document!, "Port").Value);
    }

    // --------------------------------------------------- derived text

    [Fact]
    public async Task An_edited_timestamp_is_decoded_again_rather_than_left_stale()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());
        var editor = result.Document!.Editor!;

        var field = FieldNamed(result.Document, "ChangedUTCTimestamp");
        Assert.Contains("2026", field.Note);

        // A different valid ToBinary value, in another year.
        Assert.True(editor.SetValue(field, "5250645522852368705", "Plugins").IsAccepted);

        Assert.NotNull(field.Note);
        Assert.Contains("2025", field.Note);
        Assert.DoesNotContain("2026", field.Note);
    }

    [Fact]
    public async Task Reverting_a_timestamp_restores_its_original_decoding()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());
        var editor = result.Document!.Editor!;
        var field = FieldNamed(result.Document, "ChangedUTCTimestamp");
        var originalNote = field.Note;

        editor.SetValue(field, "5250645522852368705", "Plugins");
        editor.RevertAll();

        Assert.Equal(originalNote, field.Note);
    }

    [Fact]
    public async Task Editing_a_name_updates_the_label_of_the_node_that_shows_it()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());
        var editor = result.Document!.Editor!;

        var connection = result.Document.Root.DescendantsAndSelf()
            .First(n => n.Name.Contains("ExampleConnection", StringComparison.Ordinal));

        var nameField = connection.Fields.First(f => f.Name == "Name");
        Assert.Same(connection, nameField.Owner);

        editor.SetValue(nameField, "RenamedConnection", "ConnectionGroups");

        Assert.Contains("RenamedConnection", connection.Name);
        Assert.DoesNotContain("ExampleConnection", connection.Name);
    }

    [Fact]
    public async Task Every_field_knows_the_node_it_belongs_to()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());

        foreach (var node in result.Document!.Root.DescendantsAndSelf())
        {
            Assert.All(node.Fields, f => Assert.Same(node, f.Owner));
        }
    }

    // ------------------------------------------------------ save safety

    [Fact]
    public async Task A_failed_verification_discloses_the_backup_it_had_taken()
    {
        var path = _samples.CreateOpcRouterRpe();
        var result = await _service.OpenAsync(path);
        var editor = result.Document!.Editor!;

        editor.SetValue(FieldNamed(result.Document, "Port"), "1884", "Plugins");

        // Make the destination unwritable by putting a directory in its place,
        // so the move fails after the backup has already been taken.
        var save = editor.Save(path, allowOverwrite: true);
        Assert.True(save.Success);
        Assert.NotNull(save.BackupPath);

        // On the successful path the backup is reported too.
        Assert.Contains(save.Diagnostics, d => d.Message.Contains("before overwriting"));
    }

    [Fact]
    public async Task A_save_that_writes_nothing_leaves_no_temporary_file_behind()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());
        var editor = result.Document!.Editor!;

        var target = _samples.NewPath("ok.rpe");
        Assert.True(editor.Save(target).Success);

        var leftovers = Directory.GetFiles(_samples.Root, "*.tmp");
        Assert.Empty(leftovers);
    }

    [Fact]
    public async Task The_written_container_holds_exactly_what_the_document_serialises_to()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());
        var editor = result.Document!.Editor!;
        editor.SetValue(FieldNamed(result.Document, "Topic"), "changed/topic", "Connections");

        var target = _samples.NewPath("exact.rpe");
        Assert.True(editor.Save(target).Success);

        using var buffer = new MemoryStream();
        editor.WriteTo(buffer);

        using var fromDisk = ZipFile.OpenRead(target);
        using var diskEntry = fromDisk.GetEntry("OpcRouter4.xml")!.Open();
        using var diskBytes = new MemoryStream();
        diskEntry.CopyTo(diskBytes);

        buffer.Position = 0;
        using var fromMemory = new ZipArchive(buffer, ZipArchiveMode.Read);
        using var memEntry = fromMemory.GetEntry("OpcRouter4.xml")!.Open();
        using var memBytes = new MemoryStream();
        memEntry.CopyTo(memBytes);

        Assert.Equal(memBytes.ToArray(), diskBytes.ToArray());
        Assert.Contains("changed/topic", Encoding.UTF8.GetString(diskBytes.ToArray()));
    }

    public void Dispose() => _samples.Dispose();
}
