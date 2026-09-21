using RPEReader.Core.Editing;
using RPEReader.Core.Models;
using RPEReader.Core.Parsing.OpcRouter4;
using RPEReader.Core.Services;
using RPEReader.Core.Tests.TestData;
using Xunit;

namespace RPEReader.Core.Tests.Editing;

public sealed class EditValidationTests : IDisposable
{
    private readonly SampleBuilder _samples = new();
    private readonly RpeFileService _service = new();

    private async Task<(RpeDocument Document, IRpeDocumentEditor Editor)> OpenAsync()
    {
        var result = await _service.OpenAsync(_samples.CreateOpcRouterRpe());
        return (result.Document!, result.Document!.Editor!);
    }

    private static RpeField FieldNamed(RpeDocument document, string name) =>
        document.Root.DescendantsAndSelf().SelectMany(n => n.Fields).First(f => f.Name == name);

    [Fact]
    public async Task A_declared_dotnet_type_cannot_be_edited()
    {
        var (document, editor) = await OpenAsync();

        var typeField = document.Root.DescendantsAndSelf()
            .SelectMany(n => n.Fields)
            .First(f => f.Name == "@Type");

        Assert.False(typeField.Editable);

        var result = editor.SetValue(typeField, "System.Object", "anywhere");

        Assert.Equal(EditValidationLevel.Error, result.Level);
        Assert.Equal(typeField.OriginalValue, typeField.Value);
        Assert.False(editor.IsDirty);
    }

    [Theory]
    [InlineData('\u0000')]
    [InlineData('\u0001')]
    [InlineData('\u001F')]
    [InlineData('￾')]
    public async Task Characters_xml_cannot_carry_are_rejected(char illegal)
    {
        var (document, editor) = await OpenAsync();
        var field = FieldNamed(document, "Topic");
        var before = field.Value;

        var result = editor.SetValue(field, "prefix" + illegal + "suffix", "Connections");

        Assert.Equal(EditValidationLevel.Error, result.Level);
        Assert.Equal(before, field.Value);
        Assert.False(editor.IsDirty);
    }

    [Theory]
    [InlineData("tab\there")]
    [InlineData("newline\nhere")]
    [InlineData("carriage\rreturn")]
    [InlineData("unicode ✓ ก ไทย")]
    public async Task Characters_xml_can_carry_are_accepted(string value)
    {
        var (document, editor) = await OpenAsync();
        var field = FieldNamed(document, "Topic");

        Assert.True(editor.SetValue(field, value, "Connections").IsAccepted);
        Assert.Equal(value, field.Value);
    }

    [Fact]
    public async Task Changing_a_boolean_to_free_text_is_allowed_but_warned_about()
    {
        var (document, editor) = await OpenAsync();
        var field = FieldNamed(document, "Enabled");
        Assert.Equal("True", field.OriginalValue);

        var result = editor.SetValue(field, "sometimes", "Connections");

        Assert.Equal(EditValidationLevel.Warning, result.Level);
        Assert.Contains("True or False", result.Message);
        Assert.Equal("sometimes", field.Value);
    }

    [Fact]
    public async Task Changing_a_boolean_to_another_boolean_raises_nothing()
    {
        var (document, editor) = await OpenAsync();
        var field = FieldNamed(document, "Enabled");

        var result = editor.SetValue(field, "False", "Connections");

        Assert.Equal(EditValidationLevel.Ok, result.Level);
    }

    [Fact]
    public async Task An_over_long_value_is_rejected()
    {
        // The cap has to be above every value already in the fixture: a cap that
        // clamps one makes the read incomplete, and an incomplete read is
        // deliberately offered read-only.
        const int cap = 256;
        var service = new RpeFileService(limits: new RpeLimits { MaxFieldValueChars = cap });
        var result = await service.OpenAsync(_samples.CreateOpcRouterRpe("small-limit.rpe"));

        Assert.False(result.Document!.Incomplete);
        var editor = result.Document.Editor!;
        var field = FieldNamed(result.Document, "Topic");

        var rejected = editor.SetValue(field, new string('x', cap + 1), "Connections");

        Assert.Equal(EditValidationLevel.Error, rejected.Level);
        Assert.Contains("character limit", rejected.Message);
        Assert.False(field.IsModified);
    }

    [Fact]
    public async Task Clearing_a_value_writes_a_self_closing_element()
    {
        var (document, editor) = await OpenAsync();
        var field = FieldNamed(document, "Topic");

        Assert.True(editor.SetValue(field, string.Empty, "Connections").IsAccepted);

        var target = _samples.NewPath("cleared.rpe");
        Assert.True(editor.Save(target).Success);

        using var archive = System.IO.Compression.ZipFile.OpenRead(target);
        using var stream = archive.GetEntry("OpcRouter4.xml")!.Open();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();

        Assert.Contains("<Topic />", text);
        Assert.DoesNotContain("<Topic></Topic>", text);
    }

    [Fact]
    public async Task Editing_the_same_field_twice_records_one_net_change()
    {
        var (document, editor) = await OpenAsync();
        var field = FieldNamed(document, "Topic");
        var original = field.Value;

        editor.SetValue(field, "first", "Connections");
        editor.SetValue(field, "second", "Connections");

        var edit = Assert.Single(editor.PendingEdits);
        Assert.Equal(original, edit.OldValue);
        Assert.Equal("second", edit.NewValue);
    }

    [Fact]
    public async Task Editing_back_to_the_original_clears_the_pending_change()
    {
        var (document, editor) = await OpenAsync();
        var field = FieldNamed(document, "Topic");
        var original = field.Value;

        editor.SetValue(field, "changed", "Connections");
        Assert.True(editor.IsDirty);

        editor.SetValue(field, original, "Connections");

        Assert.False(editor.IsDirty);
        Assert.Empty(editor.PendingEdits);
        Assert.False(field.IsModified);
    }

    [Fact]
    public async Task Revert_all_restores_every_field_and_the_written_bytes()
    {
        var (document, editor) = await OpenAsync();

        var clean = _samples.NewPath("clean.rpe");
        editor.Save(clean);
        var cleanBytes = await File.ReadAllBytesAsync(clean);

        editor.SetValue(FieldNamed(document, "Topic"), "a", "x");
        editor.SetValue(FieldNamed(document, "Enabled"), "False", "x");
        editor.SetValue(FieldNamed(document, "Name"), "b", "x");
        Assert.Equal(3, editor.PendingEdits.Count);

        editor.RevertAll();

        Assert.False(editor.IsDirty);
        Assert.DoesNotContain(document.Root.DescendantsAndSelf().SelectMany(n => n.Fields), f => f.IsModified);

        var reverted = _samples.NewPath("reverted.rpe");
        editor.Save(reverted);
        Assert.Equal(cleanBytes, await File.ReadAllBytesAsync(reverted));
    }

    [Fact]
    public async Task Reverting_one_change_leaves_the_others()
    {
        var (document, editor) = await OpenAsync();

        editor.SetValue(FieldNamed(document, "Topic"), "a", "x");
        editor.SetValue(FieldNamed(document, "Enabled"), "False", "x");

        Assert.True(editor.Revert(editor.PendingEdits[0]));

        var remaining = Assert.Single(editor.PendingEdits);
        Assert.Equal("Enabled", remaining.FieldName);
        Assert.False(FieldNamed(document, "Topic").IsModified);
    }

    [Fact]
    public async Task Root_attributes_are_editable_and_shared_with_the_summary()
    {
        var (document, editor) = await OpenAsync();

        var summaryField = document.Summary.First(f => f.Name == "DisplayName");
        Assert.True(summaryField.Editable);

        Assert.True(editor.SetValue(summaryField, "renamed-instance", "Document summary").IsAccepted);

        // The tree root holds the same instance, so both views agree.
        var treeField = document.Root.Fields.First(f => f.Name == "DisplayName");
        Assert.Same(summaryField, treeField);
        Assert.Equal("renamed-instance", treeField.Value);
    }

    [Fact]
    public async Task An_edited_root_attribute_survives_a_save_and_reload()
    {
        var (document, editor) = await OpenAsync();
        editor.SetValue(document.Summary.First(f => f.Name == "DisplayName"), "renamed-instance", "Document summary");

        var target = _samples.NewPath("attr.rpe");
        Assert.True(editor.Save(target).Success);

        var reopened = await _service.OpenAsync(target);
        Assert.Equal("renamed-instance", reopened.Document!.Summary.First(f => f.Name == "DisplayName").Value);
    }

    [Theory]
    [InlineData("\u0000")]
    [InlineData("ok\u0008")]
    [InlineData("\uFFFF")]
    public void Illegal_code_points_are_found(string value)
    {
        Assert.NotNull(OpcRouter4Editor.FindIllegalXmlCodePoint(value));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("tab\t")]
    [InlineData("ไทย")]
    [InlineData("emoji \U0001F600 here")]
    [InlineData("astral \U00020BB7")]
    public void Legal_code_points_are_not_flagged(string value)
    {
        Assert.Null(OpcRouter4Editor.FindIllegalXmlCodePoint(value));
    }

    [Fact]
    public void An_unpaired_surrogate_is_still_rejected()
    {
        Assert.NotNull(OpcRouter4Editor.FindIllegalXmlCodePoint("lone \uD83D end"));
        Assert.NotNull(OpcRouter4Editor.FindIllegalXmlCodePoint("lone \uDE00 end"));
    }

    [Fact]
    public async Task A_non_bmp_character_can_be_saved_and_read_back()
    {
        var (document, editor) = await OpenAsync();
        const string value = "line \U0001F600 one";

        Assert.True(editor.SetValue(FieldNamed(document, "Topic"), value, "Connections").IsAccepted);

        var target = _samples.NewPath("astral.rpe");
        Assert.True(editor.Save(target).Success);

        var reopened = await _service.OpenAsync(target);
        Assert.Equal(value, FieldNamed(reopened.Document!, "Topic").Value);
    }

    [Fact]
    public async Task Accepting_changes_keeps_the_values_and_clears_the_dirty_state()
    {
        var (document, editor) = await OpenAsync();
        var field = FieldNamed(document, "Topic");

        editor.SetValue(field, "kept", "Connections");
        Assert.True(editor.IsDirty);
        Assert.True(field.IsModified);

        editor.AcceptChanges();

        Assert.False(editor.IsDirty);
        Assert.Empty(editor.PendingEdits);
        Assert.Equal("kept", field.Value);
        Assert.Equal("kept", field.OriginalValue);
        Assert.False(field.IsModified);
    }

    [Fact]
    public async Task A_second_save_after_accepting_still_writes_the_edited_content()
    {
        // Regression: re-baselining must not roll the document back, or the
        // next save would write pre-edit content over a file that already
        // holds the edits.
        var (document, editor) = await OpenAsync();
        editor.SetValue(FieldNamed(document, "Topic"), "kept", "Connections");

        var first = _samples.NewPath("first.rpe");
        Assert.True(editor.Save(first).Success);
        editor.AcceptChanges();

        var second = _samples.NewPath("second.rpe");
        Assert.True(editor.Save(second).Success);

        Assert.Equal(await File.ReadAllBytesAsync(first), await File.ReadAllBytesAsync(second));

        var reopened = await _service.OpenAsync(second);
        Assert.Equal("kept", FieldNamed(reopened.Document!, "Topic").Value);
    }

    public void Dispose() => _samples.Dispose();
}
