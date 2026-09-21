using System.IO.Compression;
using System.Text;

namespace RPEReader.Core.Tests.TestData;

/// <summary>
/// Builds synthetic .rpe fixtures in a temporary folder.
/// </summary>
/// <remarks>
/// Everything here is invented. No sample carries a real endpoint, host name,
/// account, licence identifier or tag path, so the suite can run and its
/// fixtures can be published without disclosing anything about a real plant.
/// </remarks>
public sealed class SampleBuilder : IDisposable
{
    private readonly string _root;

    public SampleBuilder()
    {
        _root = Path.Combine(Path.GetTempPath(), "rpereader-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public string NewPath(string name) => Path.Combine(_root, name);

    /// <summary>A minimal but structurally faithful OPC Router 4 export.</summary>
    public static string MinimalOpcRouterXml { get; } = """
        <?xml version="1.0" encoding="utf-8"?>
        <OpcRouter4Export ExportType="Templates" Version="5.6.5002.211" FileVersion="Version_3"
                          EncryptedFieldHandling="Skip" Type="OPCRouter"
                          LicenseId="TEST-TEST-TEST-TEST-TEST-TEST-00"
                          DisplayName="unit-test-instance" InstanceId="UNIT_TEST_INSTANCE_ID">
          <Options />
          <Plugins>
            <PlugIn Type="BasePlugInConfig">
              <InstanceName>BasePlugIn</InstanceName>
              <Changed Type="System.DateTime">5250645522852368705</Changed>
              <PlugInTypeID>1000</PlugInTypeID>
              <Id>BasePlugInConfig</Id>
            </PlugIn>
            <PlugIn Type="MqttPlugInConfig">
              <BrokerAddress>broker.invalid</BrokerAddress>
              <Port>1883</Port>
              <Username>example-user</Username>
              <PasswordKey>
                <Key>EXAMPLE_SECRET_NAME</Key>
                <Store>internal</Store>
              </PasswordKey>
              <InstanceName>ExampleBroker</InstanceName>
              <PlugInTypeID>20000</PlugInTypeID>
            </PlugIn>
          </Plugins>
          <Certificates />
          <Connections />
          <ConnectionGroups>
            <ConnectionGroup>
              <Name>Templates</Name>
              <GroupType>Template</GroupType>
              <Id>Templates</Id>
              <Connections>
                <Connection>
                  <ConnectionLines>
                    <ConnectionLine>
                      <ItemStart>111111</ItemStart>
                      <ItemStop>222222</ItemStop>
                    </ConnectionLine>
                  </ConnectionLines>
                  <TransferObjects>
                    <StaticTransferObjectConfig Type="inray.OPCRouter.BasePlugIn.Config.Static.StaticTransferObjectConfig, inray.OPCRouter4.BasePlugIn, Version=5.6.0.0, Culture=neutral, PublicKeyToken=0000000000000000">
                      <LocalId>111111</LocalId>
                      <PosX>100</PosX>
                      <PosY>200</PosY>
                      <TransferStep>1</TransferStep>
                      <PlugInType>1000</PlugInType>
                      <ChangedUTCTimestamp Type="System.DateTime">5250883024082017904</ChangedUTCTimestamp>
                      <Groups>
                        <TransferObjectItemGroup>
                          <Name>Example group</Name>
                          <LocalId>333333</LocalId>
                          <Items>
                            <StaticTransferObjectItem Type="inray.OPCRouter.BasePlugIn.Config.Static.StaticTransferObjectItem, inray.OPCRouter4.BasePlugIn">
                              <VarType>String</VarType>
                              <VariantValue Type="System.String">example-value</VariantValue>
                              <Name>ExampleItem</Name>
                              <LocalId>444444</LocalId>
                              <Direction>output</Direction>
                            </StaticTransferObjectItem>
                          </Items>
                        </TransferObjectItemGroup>
                      </Groups>
                    </StaticTransferObjectConfig>
                  </TransferObjects>
                  <TemplateVariables>
                    <TemplateVariable>
                      <Name>ExampleVariable</Name>
                      <VariableTyp>1</VariableTyp>
                      <LocalId>555555</LocalId>
                    </TemplateVariable>
                  </TemplateVariables>
                  <TriggerConjunction>OR</TriggerConjunction>
                  <Name>ExampleConnection</Name>
                  <Group_Id>Templates</Group_Id>
                  <Enabled>True</Enabled>
                  <ConnectionType>Template</ConnectionType>
                  <Id>Templates/ExampleConnection.yaml</Id>
                </Connection>
              </Connections>
              <Groups />
            </ConnectionGroup>
          </ConnectionGroups>
          <TransferObjects />
          <ConnectionLines />
          <TransferObjectTemplateVariables />
          <Files />
          <NotificationEMailSenders />
          <NotificationGroups />
        </OpcRouter4Export>
        """;

    /// <summary>Writes a valid OPC Router 4 .rpe (ZIP containing OpcRouter4.xml).</summary>
    public string CreateOpcRouterRpe(string fileName = "valid.rpe", string? xml = null)
    {
        var path = NewPath(fileName);
        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("OpcRouter4.xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(xml ?? MinimalOpcRouterXml);
        return path;
    }

    /// <summary>A ZIP whose single entry is not an OPC Router export.</summary>
    public string CreateUnknownZipRpe(string fileName = "unknown.rpe")
    {
        var path = NewPath(fileName);
        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("something-else.bin", CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        return path;
    }

    /// <summary>A ZIP whose entry name attempts directory traversal.</summary>
    public string CreateTraversalRpe(string fileName = "traversal.rpe")
    {
        var path = NewPath(fileName);
        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("../../evil/OpcRouter4.xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(MinimalOpcRouterXml);
        return path;
    }

    /// <summary>A highly compressible ZIP, used to exercise the bomb heuristics.</summary>
    public string CreateHighlyCompressibleRpe(string fileName = "bomb.rpe", int megabytes = 24)
    {
        var path = NewPath(fileName);
        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("OpcRouter4.xml", CompressionLevel.SmallestSize);
        using var stream = entry.Open();

        var block = new byte[1024 * 1024];
        Array.Fill(block, (byte)'A');
        for (var i = 0; i < megabytes; i++)
        {
            stream.Write(block);
        }

        return path;
    }

    /// <summary>A .rpe that is a ZIP header followed by garbage.</summary>
    public string CreateCorruptZipRpe(string fileName = "corrupt.rpe")
    {
        var path = NewPath(fileName);
        var bytes = new byte[512];
        bytes[0] = 0x50;
        bytes[1] = 0x4B;
        bytes[2] = 0x03;
        bytes[3] = 0x04;
        Random.Shared.NextBytes(bytes.AsSpan(4));
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>A valid container whose XML is not well formed.</summary>
    public string CreateMalformedXmlRpe(string fileName = "malformed.rpe")
        => CreateOpcRouterRpe(fileName, "<?xml version=\"1.0\"?><OpcRouter4Export><Plugins></OpcRouter4Export>");

    /// <summary>A container whose XML declares an external DTD entity.</summary>
    public string CreateXxeRpe(string fileName = "xxe.rpe")
        => CreateOpcRouterRpe(fileName, """
            <?xml version="1.0"?>
            <!DOCTYPE OpcRouter4Export [ <!ENTITY xxe SYSTEM "file:///etc/passwd"> ]>
            <OpcRouter4Export DisplayName="&xxe;"><Plugins /></OpcRouter4Export>
            """);

    public string CreateEmptyRpe(string fileName = "empty.rpe")
    {
        var path = NewPath(fileName);
        File.WriteAllBytes(path, Array.Empty<byte>());
        return path;
    }

    /// <summary>Plain binary content with no recognisable signature.</summary>
    public string CreateBinaryRpe(string fileName = "binary.rpe", int length = 4096)
    {
        var path = NewPath(fileName);
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        bytes[0] = 0xDE;
        bytes[1] = 0xAD;
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test run over.
        }
    }
}
