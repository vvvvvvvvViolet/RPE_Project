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
        var node = new RpeNode(DisplayNameFor(element), string.IsNullOrEmpty(category) ? element.LocalName : category);
        _nodeCount++;

        foreach (XmlAttribute attribute in element.Attributes)
        {
            // Type attributes name a .NET type inside OPC Router's own
            // assemblies. They are shown verbatim and never resolved.
            node.AddField($"@{attribute.LocalName}", Clamp(attribute.Value), note: attribute.LocalName == "Type"
                ? "Declared .NET type (not resolved by this reader)"
                : null);
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
        string? note = null;

        if (IsDateTime(element, typeHint)
            && DotNetDateTimeDecoder.TryDecode(raw, out _, out var display))
        {
            note = display;
        }
        else if (OpcRouter4Schema.CredentialReferenceElements.Contains(element.LocalName))
        {
            note = "Credential store reference; the export carries no secret value.";
        }
        else if (element.LocalName is "PlugInType" or "PlugInTypeID")
        {
            var described = OpcRouter4Schema.DescribePlugInType(raw.Trim());
            if (!string.IsNullOrEmpty(described))
            {
                note = described;
            }
        }

        node.AddField(element.LocalName, Clamp(raw), string.IsNullOrEmpty(typeHint) ? null : typeHint, note);
    }

    private static bool IsDateTime(XmlElement element, string typeHint) =>
        typeHint.StartsWith("System.DateTime", StringComparison.Ordinal)
        || (string.IsNullOrEmpty(typeHint) && OpcRouter4Schema.KnownDateTimeElements.Contains(element.LocalName));

    private static string DisplayNameFor(XmlElement element)
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
