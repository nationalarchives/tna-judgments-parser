using System.Xml;

using Backlog.Src;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestStub
{
    [Fact]
    public void Stub_WithNCN_AppearsInXmlAsCite()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            ncn = "[2023] UKUT 123 (IAC)"
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        Assert.Contains("<uk:cite>[2023] UKUT 123 (IAC)</uk:cite>", xml);
    }

    [Fact]
    public void Stub_WithEmptyNCN_DoesNotAppearInXml()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            ncn = ""
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        Assert.DoesNotContain("<uk:cite", xml);
        Assert.DoesNotContain("</uk:cite>", xml);
    }

    [Fact]
    public void Stub_WithNullNCN_DoesNotAppearInXml()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            // ncn is not set (null)
            ncn = null
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        Assert.DoesNotContain("<uk:cite", xml);
        Assert.DoesNotContain("</uk:cite>", xml);
    }

    [Fact]
    public void Stub_WithWhitespaceNCN_DoesNotAppearInXml()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            ncn = "   "
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        Assert.DoesNotContain("<uk:cite", xml);
        Assert.DoesNotContain("</uk:cite>", xml);
    }

    [Fact]
    public void Stub_WithNCNSpecialCharacters_AppearsCorrectlyInXml()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            ncn = "[2023] EWCA Civ 123 & 124"
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        Assert.Contains("<uk:cite", xml);
        Assert.Contains("[2023] EWCA Civ 123 &amp; 124", xml);
        Assert.Contains("</uk:cite>", xml);
    }
    
    [Fact]
    public void Stub_WithSingleJurisdiction_AppearsInXmlAsUkJurisdiction()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Jurisdictions = ["new jurisdiction"]
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.HasSingleNodeWithName("proprietary").Which().HasChildMatching("uk:jurisdiction", "new jurisdiction");
    }
    
    [Fact]
    public void Stub_WithMultipleJurisdictions_AppearInXmlAsUkJurisdictions()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Jurisdictions = ["new jurisdiction", "other new jurisdiction"]
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.HasSingleNodeWithName("proprietary").Which()
           .HasChildMatching("uk:jurisdiction", "new jurisdiction")
           .And().HasChildMatching("uk:jurisdiction", "other new jurisdiction");
    }

    [Fact]
    public void Stub_WithNoJurisdiction_DoesNotAppearInXml()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Jurisdictions = []
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.DoesNotHaveNodeWithName("uk:jurisdiction");
    }

    [Fact]
    public void Stub_WithWebArchivingLink_AppearsInXmlAsUkWebarchiving()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            webarchiving = "a web archive link"
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.HasSingleNodeWithName("proprietary").Which().HasChildMatching("uk:webarchiving", "a web archive link");
    }

    [Fact]
    public void Stub_WithNoWebArchivingLink_DoesNotAppearInXml()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            webarchiving = null
        };
        var metadata = MetadataTransformer.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.DoesNotHaveNodeWithName("uk:webarchiving");
    }
}
