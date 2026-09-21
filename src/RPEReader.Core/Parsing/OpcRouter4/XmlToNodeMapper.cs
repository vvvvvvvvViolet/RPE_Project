using System.Xml;
using RPEReader.Core.Models;

namespace RPEReader.Core.Parsing.OpcRouter4;

/// <summary>
/// Turns the export XML into the generic <see cref="RpeNode"/> tree.
/// </summary>
/// <remarks>
/// Elements with no child elements become fields on their parent; elements with
/// children become child nodes. That keeps the tree close to the way OPC Router
/// itself presents a project, instead of one node per XML element.
/// </remarks>
internal sealed class XmlToNodeMapper
{
    private readonly RpeLimits _limits;
    private readonly List<ParseDiagnostic> _diagnostics;
    private readonly CancellationToken _cancellationToken;
    private int _nodeCount;

    public XmlToNodeMapper(RpeLimits limits, List<ParseDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        _limits = limits;
        _diagnostics = diagnostics;
        _cancellationToken = cancellationToken;
    }

    public bool LimitHit { get; private set; }

    public RpeNode Map(XmlElement element, string? categoryOverride = null)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        var category = categoryOverride ?? OpcRouter4Schema.DescribeTransferObject(element.LocalName);
        var node = new RpeNode(DisplayNameFor(element), string.IsNullOrEmpty(category) ? element.LocalName : category)
        {
            Source = element
        };
        _nodeCount++;

        foreach (XmlAttribute attribute in element.Attributes)
        {
            // Type attributes name a .NET type inside OPC Router's own
            // assemblies. They are shown verbatim, never resolved, and never
            // opened for editing: rewriting one would misdescribe the data to
            // OPC Router on import.
            var isTypeAttribute = attribute.LocalName == "Type";
            var clamped = Clamp(attribute.Value);

            node.AddField(new RpeField(
                $"@{attribute.LocalName}",
                clamped,
                note: isTypeAttribute ? "Declared .NET type (not resolved, not editable)" : null,
                source: attribute,
                editable: !isTypeAttribute && !WasClamped(attribute.Value, clamped)));
        }

        var childElements = new List<XmlElement>();
        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is XmlElement childElement)
            {
                childElements.Add(childElement);
            }
        }

        if (childElements.Count == 0)
        {
            var text = element.InnerText;
            if (!string.IsNullOrEmpty(text))
            {
                node.Value = Clamp(text);
            }

            return node;
        }

        foreach (var child in childElements)
        {
            if (_nodeCount >= _limits.MaxNodes)
            {
                if (!LimitHit)
                {
                    LimitHit = true;
                    _diagnostics.Add(ParseDiagnostic.Warning(
                        $"The document holds more than the {_limits.MaxNodes:N0}-node display limit; the tree was truncated."));
                }

                node.Truncated = true;
                break;
            }

            if (IsLeaf(child))
            {
                if (node.Fields.Count >= _limits.MaxFieldsPerNode)
                {
                    node.Truncated = true;
                    continue;
                }

                AddLeafField(node, child);
            }
            else
            {
                node.AddChild(Map(child));
            }
        }

        var summary = SummaryFor(node);
        if (summary is not null)
        {
            node.Value = summary;
        }

        return node;
    }

    private static bool IsLeaf(XmlElement element)
    {
        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is XmlElement)
            {
                return false;
            }
        }

        return true;
    }

    private void AddLeafField(RpeNode node, XmlElement element)
    {
        var typeHint = element.GetAttribute("Type");
        var raw = element.InnerText;
        var note = DeriveNote(element.LocalName, typeHint, raw);
        var clamped = Clamp(raw);

        node.AddField(new RpeField(
            element.LocalName,
            clamped,
            string.IsNullOrEmpty(typeHint) ? null : typeHint,
            note,
            source: element,
            // A value that had to be shortened for display must not be written
            // back, or the edit would silently truncate the file.
            editable: !WasClamped(raw, clamped)));
    }

    /// <summary>
    /// Builds the explanatory text shown beside a value — a decoded timestamp,
    /// a plug-in type name, a note that a credential is held by reference.
    /// </summary>
    /// <remarks>
    /// Shared with <c>OpcRouter4Editor</c> so that an edited value's note is
    /// recomputed from the new value instead of continuing to describe the old
    /// one. A stale decoded date is worse than no date at all.
    /// </remarks>
    internal static string? DeriveNote(string elementName, string? typeHint, string? rawValue)
    {
        if (IsDateTime(elementName, typeHint)
            && DotNetDateTimeDecoder.TryDecode(rawValue, out _, out var display))
        {
            return display;
        }

        if (OpcRouter4Schema.CredentialReferenceElements.Contains(elementName))
        {
            return "Credential store reference; the export carries no secret value.";
        }

        if (elementName is "PlugInType" or "PlugInTypeID")
        {
            var described = OpcRouter4Schema.DescribePlugInType(rawValue?.Trim());
            return string.IsNullOrEmpty(described) ? null : described;
        }

        return null;
    }

    private static bool IsDateTime(string elementName, string? typeHint) =>
        (typeHint?.StartsWith("System.DateTime", StringComparison.Ordinal) ?? false)
        || (string.IsNullOrEmpty(typeHint) && OpcRouter4Schema.KnownDateTimeElements.Contains(elementName));

    internal static string DisplayNameFor(XmlElement element)
    {
        foreach (var candidate in OpcRouter4Schema.NameElements)
        {
            foreach (XmlNode child in element.ChildNodes)
            {
                if (child is XmlElement e
                    && string.Equals(e.LocalName, candidate, StringComparison.Ordinal)
                    && e.ChildNodes.Count > 0
                    && !string.IsNullOrWhiteSpace(e.InnerText))
                {
                    return $"{element.LocalName}: {Truncate(e.InnerText.Trim(), 120)}";
                }
            }
        }

        return element.LocalName;
    }

    /// <summary>Builds the short grey text shown after a node's name.</summary>
    private static string? SummaryFor(RpeNode node)
    {
        if (node.Children.Count == 0)
        {
            return null;
        }

        return node.Children.Count == 1 ? "1 item" : $"{node.Children.Count:N0} items";
    }

    /// <summary>True when <see cref="Clamp"/> shortened the value for display.</summary>
    private static bool WasClamped(string? original, string? clamped) =>
        !string.Equals(original, clamped, StringComparison.Ordinal);

    private string? Clamp(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length <= _limits.MaxFieldValueChars)
        {
            return value;
        }

        LimitHit = true;
        return value[.._limits.MaxFieldValueChars] + $"… (truncated at {_limits.MaxFieldValueChars:N0} characters)";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
