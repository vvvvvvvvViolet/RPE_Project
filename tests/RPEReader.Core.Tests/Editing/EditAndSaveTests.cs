using System.IO.Compression;
using RPEReader.Core.Editing;
using RPEReader.Core.Models;
using RPEReader.Core.Parsing.OpcRouter4;
using RPEReader.Core.Services;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests.Editing;

/// <summary>
/// The contract that makes an exported file safe to import back into OPC
/// Router: an untouched save reproduces the payload exactly, and an edited save
/// differs only where the edit was made.
/// </summary>
public sealed class EditAndSaveTests : IDisposable
{
    private readonly SampleBuilder _samples = new();
    private readonly RpeFileService _service = new();

    private static byte[] ReadPayload(string rpePath)
    {
        using var archive = ZipFile.OpenRead(rpePath);
        var entry = archive.GetEntry(OpcRouter4Schema.ContainerEntryName);
        Assert.NotNull(entry);

        using var stream = entry!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static uint ReadPayloadCrc(string rpePath)
    {
        using var archive = ZipFile.OpenRead(rpePath);
        return archive.GetEntry(OpcRouter4Schema.ContainerEntryName)!.Crc32;
    }

    private async Task<(RpeDocument Document, IRpeDocumentEditor Editor, string Path)> OpenEditableAsync()
    {
        var path = _samples.CreateOpcRouterRpe();
        var result = await _service.OpenAsync(path);

        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.True(result.Document!.IsEditable);
        Assert.NotNull(result.Document.Editor);

        return (result.Document, result.Document.Editor!, path);
    }

    private static RpeField FieldNamed(RpeDocument document, string name) =>
        document.Root.DescendantsAndSelf()
            .SelectMany(n => n.Fields)
            .First(f => f.Name == name);

    // ------------------------------------------------------------ round trip

    [Fact]
    public async Task An_untouched_save_reproduces_the_payload_byte_for_byte()
    {
        var (_, editor, sourcePath) = await OpenEditableAsync();
        var target = _samples.NewPath("untouched.rpe");

        var save = editor.Save(target);

        Assert.True(save.Success);
        Assert.True(save.VerifiedByReparse);
        Assert.Equal(ReadPayload(sourcePath), ReadPayload(target));
        Assert.Equal(ReadPayloadCrc(sourcePath), ReadPayloadCrc(target));
    }

    [Fact]
    public async Task An_edited_save_differs_only_where_the_edit_was_made()
    {
        var (document, editor, sourcePath) = await OpenEditableAsync();

        var field = FieldNamed(document, "BrokerAddress");
        Assert.Equal("broker.invalid", field.Value);

        // Same length, so a minimal diff is exactly the replaced run.
        Assert.True(editor.SetValue(field, "broker.example", "Plugins").IsAccepted);

        var target = _samples.NewPath("edited.rpe");
        Assert.True(editor.Save(target).Success);

        var before = ReadPayload(sourcePath);
        var after = ReadPayload(target);

        Assert.Equal(before.Length, after.Length);

        var differing = before.Zip(after).Count(p => p.First != p.Second);
        Assert.InRange(differing, 1, "broker.invalid".Length);
    }

    [Fact]
    public async Task A_saved_file_reopens_with_the_edit_present()
    {
        var (document, editor, _) = await OpenEditableAsync();

        var field = FieldNamed(document, "Topic");
        editor.SetValue(field, "plant/line/1/status", "Connections");

        var target = _samples.NewPath("reopened.rpe");
        Assert.True(editor.Save(target).Success);

        var reopened = await _service.OpenAsync(target);

        Assert.True(reopened.Success);
        Assert.Equal("opcrouter4.zip", reopened.Document!.FormatId);
        Assert.Equal("plant/line/1/status", FieldNamed(reopened.Document, "Topic").Value);
    }

    [Fact]
    public async Task A_saved_file_keeps_the_container_shape_opc_router_expects()
    {
        var (document, editor, _) = await OpenEditableAsync();
        editor.SetValue(FieldNamed(document, "Port"), "8883", "Plugins");

        var target = _samples.NewPath("shape.rpe");
        Assert.True(editor.Save(target).Success);

        using var archive = ZipFile.OpenRead(target);

        var entry = Assert.Single(archive.Entries);
        Assert.Equal(OpcRouter4Schema.ContainerEntryName, entry.FullName);
        Assert.True(entry.Length > 0);
        Assert.NotEqual(entry.Length, entry.CompressedLength);
    }

    [Fact]
    public async Task The_written_payload_has_no_bom_and_uses_line_feed_endings()
    {
        var (_, editor, _) = await OpenEditableAsync();
        var target = _samples.NewPath("encoding.rpe");
        editor.Save(target);

        var payload = ReadPayload(target);

        Assert.False(payload.Length >= 3 && payload[0] == 0xEF && payload[1] == 0xBB && payload[2] == 0xBF);
        Assert.DoesNotContain((byte)'\r', payload);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n", System.Text.Encoding.UTF8.GetString(payload));
    }

    // --------------------------------------------------------------- safety

    [Fact]
    public async Task Saving_never_touches_the_file_that_is_open()
    {
        var (document, editor, sourcePath) = await OpenEditableAsync();
        var before = await File.ReadAllBytesAsync(sourcePath);
        var writtenAt = File.GetLastWriteTimeUtc(sourcePath);

        editor.SetValue(FieldNamed(document, "Name"), "Renamed", "Connections");
        editor.Save(_samples.NewPath("elsewhere.rpe"));

        Assert.Equal(before, await File.ReadAllBytesAsync(sourcePath));
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(sourcePath));
    }

    [Fact]
    public async Task Overwriting_the_source_is_refused_unless_explicitly_allowed()
    {
        var (document, editor, sourcePath) = await OpenEditableAsync();
        editor.SetValue(FieldNamed(document, "Name"), "Renamed", "Connections");

        var refused = editor.Save(sourcePath);

        Assert.False(refused.Success);
        Assert.Contains(refused.Diagnostics, d =>
            d.Severity == DiagnosticSeverity.Error && d.Message.Contains("explicitly requested"));
    }

    [Fact]
    public async Task Overwriting_the_source_takes_a_backup_first()
    {
        var (document, editor, sourcePath) = await OpenEditableAsync();
        var original = await File.ReadAllBytesAsync(sourcePath);

        editor.SetValue(FieldNamed(document, "Name"), "Renamed", "Connections");
        var save = editor.Save(sourcePath, allowOverwrite: true);

        Assert.True(save.Success);
        Assert.NotNull(save.BackupPath);
        Assert.True(File.Exists(save.BackupPath!));
        Assert.Equal(original, await File.ReadAllBytesAsync(save.BackupPath!));

        // The file itself really did change.
        Assert.NotEqual(original, await File.ReadAllBytesAsync(sourcePath));
    }

    [Fact]
    public async Task A_save_reports_the_hash_of_what_it_wrote()
    {
        var (_, editor, _) = await OpenEditableAsync();
        var target = _samples.NewPath("hashed.rpe");

        var save = editor.Save(target);

        Assert.True(save.Success);
        Assert.Equal(await FileHashService.ComputeSha256Async(target), save.Sha256);
        Assert.Equal(new FileInfo(target).Length, save.BytesWritten);
    }

    public void Dispose() => _samples.Dispose();
}
