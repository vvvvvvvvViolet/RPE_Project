namespace RPEReader.Core.Parsing.OpcRouter4;

/// <summary>
/// Element and attribute names observed in OPC Router 4 export documents,
/// together with the small amount of interpretation the reader applies.
/// Everything here is derived from the sample files; see
/// docs/RPE_FORMAT_FINDINGS.md for the evidence behind each entry.
/// </summary>
public static class OpcRouter4Schema
{
    public const string ContainerEntryName = "OpcRouter4.xml";
    public const string RootElement = "OpcRouter4Export";

    /// <summary>Top-level sections, in the order the exporter writes them.</summary>
    public static readonly string[] TopLevelSections =
    {
        "Options",
        "Plugins",
        "Certificates",
        "Connections",
        "ConnectionGroups",
        "TransferObjects",
        "ConnectionLines",
        "TransferObjectTemplateVariables",
        "Files",
        "NotificationEMailSenders",
        "NotificationGroups"
    };

    /// <summary>Root attributes surfaced in the summary pane.</summary>
    public static readonly string[] RootAttributes =
    {
        "ExportType",
        "Version",
        "FileVersion",
        "EncryptedFieldHandling",
        "Type",
        "LicenseId",
        "DisplayName",
        "InstanceId"
    };

    /// <summary>
    /// Elements whose text is a <see cref="DateTime.ToBinary"/> value. The
    /// <c>Type="System.DateTime"</c> attribute is the authoritative signal; this
    /// list only helps when the attribute is absent.
    /// </summary>
    public static readonly HashSet<string> KnownDateTimeElements = new(StringComparer.Ordinal)
    {
        "Changed",
        "ChangedUTCTimestamp",
        "ChangedUtcTimestamp",
        "PublishedUtcTimestamp",
        "ValidTo",
        "ValidFrom"
    };

    /// <summary>Elements that carry a node's display name.</summary>
    public static readonly string[] NameElements =
    {
        "Name",
        "InstanceName",
        "DisplayName",
        "Id"
    };

    /// <summary>
    /// Bookkeeping fields the OPC Router persistence layer writes on nearly
    /// every element. They are still shown in the detail grid, but they are not
    /// promoted into the tree label.
    /// </summary>
    public static readonly HashSet<string> PersistenceNoise = new(StringComparer.Ordinal)
    {
        "ForceIdentityInsert",
        "ForceDBInsert",
        "SupportsDBNull",
        "LoadedFromDB",
        "LoadedSchemaFromDB",
        "Loaded"
    };

    /// <summary>
    /// Elements that name a credential held in the OPC Router credential store.
    /// The export contains the *reference*, never the secret itself.
    /// </summary>
    public static readonly HashSet<string> CredentialReferenceElements = new(StringComparer.Ordinal)
    {
        "PasswordKey"
    };

    /// <summary>Plug-in type identifiers seen in <c>PlugInTypeID</c>/<c>PlugInType</c>.</summary>
    public static string DescribePlugInType(string? value) => value switch
    {
        "1000" => "Base plug-in",
        "2000" => "OPC plug-in",
        "3100" => "Variables/flow plug-in",
        "5000" => "Variables plug-in",
        "20000" => "MQTT plug-in",
        "36000" => "Database plug-in",
        _ => string.Empty
    };

    /// <summary>
    /// Maps a transfer-object element name to the role it plays in a connection.
    /// The element name is used, never the .NET type in the Type attribute.
    /// </summary>
    public static string DescribeTransferObject(string elementName)
    {
        if (elementName.EndsWith("TriggerConfig", StringComparison.Ordinal))
        {
            return "Trigger";
        }

        if (elementName.EndsWith("TransferObjectConfig", StringComparison.Ordinal))
        {
            return "Transfer object";
        }

        if (elementName.EndsWith("TransferObjectItem", StringComparison.Ordinal))
        {
            return "Item";
        }

        if (elementName.EndsWith("ItemGroup", StringComparison.Ordinal) || elementName.EndsWith("Group", StringComparison.Ordinal))
        {
            return "Item group";
        }

        return string.Empty;
    }
}
