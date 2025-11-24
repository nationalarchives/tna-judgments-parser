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
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            ncn = "[2023] UKUT 123 (IAC)"
        };
        var metadata = Metadata.MakeMetadata(line);

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
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            ncn = ""
        };
        var metadata = Metadata.MakeMetadata(line);

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
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf"
            // ncn is not set (null)
        };
        var metadata = Metadata.MakeMetadata(line);

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
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            ncn = "   "
        };
        var metadata = Metadata.MakeMetadata(line);

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
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            ncn = "[2023] EWCA Civ 123 & 124"
        };
        var metadata = Metadata.MakeMetadata(line);

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
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            Jurisdictions = ["new jurisdiction"]
        };
        var metadata = Metadata.MakeMetadata(line);

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
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            Jurisdictions = ["new jurisdiction", "other new jurisdiction"]
        };
        var metadata = Metadata.MakeMetadata(line);

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
    public void Stub_WithEmptyJurisdiction_DoesNotAppearInXml()
    {
        // Arrange
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            Jurisdictions = [""]
        };
        var metadata = Metadata.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.DoesNotHaveNodeWithName("uk:jurisdiction");
    }

    [Fact]
    public void Stub_WithNoJurisdiction_DoesNotAppearInXml()
    {
        // Arrange
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf"
            // Jurisdiction is not set (empty)
        };
        var metadata = Metadata.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.DoesNotHaveNodeWithName("uk:jurisdiction");
    }

    [Fact]
    public void Stub_WithWhitespaceJurisdiction_DoesNotAppearInXml()
    {
        // Arrange
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            Jurisdictions = ["   "]
        };
        var metadata = Metadata.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.DoesNotHaveNodeWithName("uk:jurisdiction");
    }

    [Fact]
    public void Stub_WithMultipleWhitespaceJurisdictions_DoesNotAppearInXml()
    {
        // Arrange
        var line = new Metadata.Line
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = "2023-01-14 14:30:00",
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            main_category = "Immigration",
            Extension = ".pdf",
            Jurisdictions = ["   ", "", "  "]
        };
        var metadata = Metadata.MakeMetadata(line);

        // Act
        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        // Assert
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        doc.DoesNotHaveNodeWithName("uk:jurisdiction");
    }
}
