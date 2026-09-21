using System.Text;
using System.Xml;
using RPEReader.Core.Parsing.OpcRouter4;
using Xunit;

namespace RPEReader.Core.Tests.Editing;

/// <summary>
/// Pins the exact serialisation OPC Router uses, as measured from the supplied
/// samples (see docs/RPE_FORMAT_FINDINGS.md §8). These assertions are written
/// against literal expected bytes rather than against the writer, so a change
/// to the writer's settings fails here instead of silently producing files the
/// product would rewrite wholesale on import.
/// </summary>
public sealed class WriterShapeTests
{
    private static XmlDocument Parse(string xml)
    {
        // Same settings the application reads with, so these tests exercise the
        // real pairing rather than a convenient one.
        return OpcRouter4Writer.ReadXml(new UTF8Encoding(false).GetBytes(xml), 1024 * 1024);
    }

    /// <summary>
    /// The product's own layout, fed in and expected back unchanged: LF
    /// endings, two-space indentation, a space before the self-closing slash,
    /// and no newline after the closing root tag.
    /// </summary>
    [Fact]
    public void The_products_layout_is_reproduced_exactly()
    {
        const string productForm =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<OpcRouter4Export ExportType=\"Templates\">\n" +
            "  <Options />\n" +
            "  <Plugins>\n" +
            "    <PlugIn Type=\"BasePlugInConfig\">\n" +
            "      <InstanceName>BasePlugIn</InstanceName>\n" +
            "    </PlugIn>\n" +
            "  </Plugins>\n" +
            "</OpcRouter4Export>";

        var original = new UTF8Encoding(false).GetBytes(productForm);

        var written = OpcRouter4Writer.SerialiseXml(OpcRouter4Writer.ReadXml(original, 1024 * 1024));

        Assert.Equal(productForm, Encoding.UTF8.GetString(written));
        Assert.Equal(original, written);
    }

    [Fact]
    public void No_byte_order_mark_is_written()
    {
        var bytes = OpcRouter4Writer.SerialiseXml(Parse("<OpcRouter4Export />"));

        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal((byte)'<', bytes[0]);
    }

    [Fact]
    public void No_carriage_return_is_written()
    {
        var document = Parse("<OpcRouter4Export>\n  <A>1</A>\n  <B>2</B>\n</OpcRouter4Export>");

        Assert.DoesNotContain((byte)'\r', OpcRouter4Writer.SerialiseXml(document));
    }

    [Fact]
    public void There_is_no_trailing_newline()
    {
        var bytes = OpcRouter4Writer.SerialiseXml(Parse("<OpcRouter4Export><A>1</A></OpcRouter4Export>"));

        Assert.Equal((byte)'>', bytes[^1]);
    }

    [Fact]
    public void An_element_with_no_children_is_written_self_closing()
    {
        var document = Parse("<OpcRouter4Export><Name>something</Name></OpcRouter4Export>");
        ((XmlElement)document.DocumentElement!.FirstChild!).IsEmpty = true;

        var text = Encoding.UTF8.GetString(OpcRouter4Writer.SerialiseXml(document));

        Assert.Contains("<Name />", text);
    }

    /// <summary>
    /// Documents the pitfall the editor has to avoid: assigning an empty string
    /// to InnerText leaves an empty text node behind, which serialises as a
    /// start/end pair rather than the self-closing form the product writes.
    /// OpcRouter4Editor sets IsEmpty instead, which the test above pins.
    /// </summary>
    [Fact]
    public void Assigning_an_empty_InnerText_does_not_produce_the_self_closing_form()
    {
        var document = Parse("<OpcRouter4Export><Name>something</Name></OpcRouter4Export>");
        document.DocumentElement!.FirstChild!.InnerText = string.Empty;

        var text = Encoding.UTF8.GetString(OpcRouter4Writer.SerialiseXml(document));

        Assert.Contains("<Name></Name>", text);
        Assert.DoesNotContain("<Name />", text);
    }

    [Theory]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("a < b", "a &lt; b")]
    [InlineData("say \"hi\"", "say \"hi\"")]
    public void Markup_characters_in_a_value_are_escaped(string value, string expectedFragment)
    {
        var document = Parse("<OpcRouter4Export><Name>placeholder</Name></OpcRouter4Export>");
        document.DocumentElement!.FirstChild!.InnerText = value;

        var text = Encoding.UTF8.GetString(OpcRouter4Writer.SerialiseXml(document));

        Assert.Contains(expectedFragment, text);
    }

    /// <summary>
    /// A whitespace-only value is real data — a delimiter field holding a space
    /// or a tab — and must survive the round trip.
    /// </summary>
    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void A_whitespace_only_value_is_preserved(string whitespace)
    {
        var xml = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<OpcRouter4Export>\n  <Delimiter>{whitespace}</Delimiter>\n</OpcRouter4Export>";
        var original = new UTF8Encoding(false).GetBytes(xml);

        var reloaded = OpcRouter4Writer.ReadXml(original, 1024 * 1024);
        var written = OpcRouter4Writer.SerialiseXml(reloaded);

        Assert.Equal(original, written);

        // Whitespace between elements is preserved too, so the delimiter has to
        // be located by name rather than by position.
        var delimiter = reloaded.GetElementsByTagName("Delimiter")[0]!;
        Assert.Equal(whitespace, delimiter.InnerText);
    }

    /// <summary>
    /// A carriage return inside a value has to come back as a carriage return.
    /// Rewriting it to a line feed would alter a multi-line value on a save
    /// that made no edits, and the altered form is itself stable, so no
    /// round-trip check would notice.
    /// </summary>
    [Fact]
    public void A_carriage_return_inside_a_value_is_preserved()
    {
        var document = Parse("<OpcRouter4Export><Sql>placeholder</Sql></OpcRouter4Export>");
        document.DocumentElement!.FirstChild!.InnerText = "SELECT 1\r\nFROM t";

        var bytes = OpcRouter4Writer.SerialiseXml(document);

        Assert.Contains("&#xD;", Encoding.UTF8.GetString(bytes));
        Assert.Equal("SELECT 1\r\nFROM t", OpcRouter4Writer.ReadXml(bytes, 1024 * 1024).DocumentElement!.FirstChild!.InnerText);
    }

    /// <summary>
    /// The byte-fidelity claim has to hold for a document this tool did not
    /// produce, or it proves nothing. The XML here is written by hand in the
    /// product's measured layout and is never passed through the writer first.
    /// </summary>
    [Fact]
    public void A_document_the_writer_did_not_produce_round_trips_byte_for_byte()
    {
        const string handWritten =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<OpcRouter4Export ExportType=\"Templates\" Version=\"5.6.5002.211\" FileVersion=\"Version_3\">\n" +
            "  <Options />\n" +
            "  <Plugins>\n" +
            "    <PlugIn Type=\"MqttPlugInConfig\">\n" +
            "      <BrokerAddress>broker.invalid</BrokerAddress>\n" +
            "      <Port>1883</Port>\n" +
            "      <ClientID />\n" +
            "      <PasswordKey>\n" +
            "        <Key>EXAMPLE</Key>\n" +
            "        <Store>internal</Store>\n" +
            "      </PasswordKey>\n" +
            "      <Changed Type=\"System.DateTime\">5250645522852368705</Changed>\n" +
            "    </PlugIn>\n" +
            "  </Plugins>\n" +
            "  <Certificates />\n" +
            "</OpcRouter4Export>";

        var original = new UTF8Encoding(false).GetBytes(handWritten);

        var written = OpcRouter4Writer.SerialiseXml(OpcRouter4Writer.ReadXml(original, 1024 * 1024));

        Assert.Equal(original, written);
    }

    /// <summary>
    /// Formatting this tool would not have chosen must still survive, because
    /// the writer preserves the document's own layout rather than imposing one.
    /// </summary>
    [Fact]
    public void Unusual_formatting_is_preserved_rather_than_normalised()
    {
        const string oddlyFormatted =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<OpcRouter4Export>\n" +
            "\t<Deeply>\n" +
            "\t\t\t<Nested>value</Nested>\n" +
            "\t</Deeply>\n" +
            "  <OnOneLine><A>1</A><B>2</B></OnOneLine>\n" +
            "</OpcRouter4Export>";

        var original = new UTF8Encoding(false).GetBytes(oddlyFormatted);

        Assert.Equal(original, OpcRouter4Writer.SerialiseXml(OpcRouter4Writer.ReadXml(original, 1024 * 1024)));
    }

    [Fact]
    public void A_value_survives_a_write_then_read_cycle_unchanged()
    {
        const string awkward = "path/with spaces & <brackets> \"quotes\" ไทย ✓";

        var document = Parse("<OpcRouter4Export><Name>placeholder</Name></OpcRouter4Export>");
        document.DocumentElement!.FirstChild!.InnerText = awkward;

        var bytes = OpcRouter4Writer.SerialiseXml(document);
        var reloaded = OpcRouter4Writer.ReadXml(bytes, 1024 * 1024);

        Assert.Equal(awkward, reloaded.DocumentElement!.FirstChild!.InnerText);
        Assert.Equal(bytes, OpcRouter4Writer.SerialiseXml(reloaded));
    }
}
