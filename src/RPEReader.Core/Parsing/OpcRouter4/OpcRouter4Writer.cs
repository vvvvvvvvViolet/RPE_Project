using System.IO.Compression;
using System.Text;
using System.Xml;

namespace RPEReader.Core.Parsing.OpcRouter4;

/// <summary>
/// Serialises an OPC Router 4 export back to the exact on-disk shape the
/// product wrote, so the result can be imported again.
/// </summary>
/// <remarks>
/// The approach is to preserve rather than reformat (see
/// docs/RPE_FORMAT_FINDINGS.md §8). The document is read with every whitespace
/// node kept, and written back without re-indenting, so the file's own layout
/// is reproduced instead of a layout this tool prefers.
/// <para>
/// That matters for more than tidiness. Re-indenting would depend on the
/// reader having classified every whitespace node the way the writer expects,
/// and a whitespace-only value — a delimiter field holding a space or a tab —
/// would be silently dropped on the way through. Preserving instead means an
/// unmodified document round-trips byte for byte whatever its formatting, not
/// only when it happens to match this tool's own style.
/// </para>
/// <para>
/// <see cref="NewLineHandling.Entitize"/> is required for the same reason: it
/// writes a carriage return inside a value as <c>&amp;#xD;</c>, which survives
/// re-reading. The alternative rewrites it to a line feed, so a multi-line
/// value such as an embedded SQL statement would come back altered — and,
/// because the altered form is itself stable, no round-trip check would catch
/// it.
/// </para>
/// Measured from the samples: UTF-8 with no byte order mark, <c>\n</c> line
/// endings, two-space indentation, no trailing newline after the closing root
/// tag, and a single DEFLATE-compressed ZIP entry named <c>OpcRouter4.xml</c>.
/// Those properties are reproduced by preserving them, and are pinned by tests.
/// </remarks>
public static class OpcRouter4Writer
{
    /// <summary>
    /// Writer settings that reproduce the document exactly as it was read.
    /// Indentation is off because the document carries its own whitespace.
    /// </summary>
    public static XmlWriterSettings CreateXmlWriterSettings() => new()
    {
        Indent = false,
        NewLineHandling = NewLineHandling.Entitize,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        OmitXmlDeclaration = false,
        CloseOutput = false
    };

    /// <summary>
    /// Reader settings paired with <see cref="CreateXmlWriterSettings"/>.
    /// Whitespace is kept, and the document it loads must have
    /// <see cref="XmlDocument.PreserveWhitespace"/> set before loading.
    /// </summary>
    public static XmlReaderSettings CreateXmlReaderSettings(long maxCharacters) => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreWhitespace = false,
        MaxCharactersInDocument = maxCharacters,
        MaxCharactersFromEntities = 0,
        CloseInput = true
    };

    /// <summary>Serialises the document to the bytes of <c>OpcRouter4.xml</c>.</summary>
    public static byte[] SerialiseXml(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, CreateXmlWriterSettings()))
        {
            document.Save(writer);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Writes a complete .rpe container to <paramref name="destination"/>.
    /// The stream is left open.
    /// </summary>
    public static void WriteContainer(XmlDocument document, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        var xml = SerialiseXml(document);
        cancellationToken.ThrowIfCancellationRequested();

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var entry = archive.CreateEntry(OpcRouter4Schema.ContainerEntryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(xml, 0, xml.Length);
    }

    /// <summary>
    /// Reads a document back with the same settings the parser uses. Used to
    /// verify a file that was just written.
    /// </summary>
    public static XmlDocument ReadXml(byte[] xmlBytes, long maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(xmlBytes);

        using var stream = new MemoryStream(xmlBytes, writable: false);
        using var reader = XmlReader.Create(stream, CreateXmlReaderSettings(maxCharacters));

        // PreserveWhitespace has to be set before Load, or the whitespace nodes
        // are discarded as they arrive.
        var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = true };
        document.Load(reader);
        return document;
    }
}
