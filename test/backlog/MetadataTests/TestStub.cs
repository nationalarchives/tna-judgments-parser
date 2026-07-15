#nullable enable

using System;
using System.Xml;

using Backlog.Csv;
using Backlog.Src;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestStub
{
    private readonly DateTime dummyDate = new(1000, 01, 01, 00, 00, 00, 00, DateTimeKind.Utc);

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("")]
    public void Stub_WithoutNCN_DoesNotHaveUkCite(string? blankNcn)
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Ncn = blankNcn
        };

        var resultXml = Act(line);

        resultXml.DoesNotHaveNodeWithName("uk:cite");
    }

    [Theory]
    [InlineData("[2023] UKUT 123 (IAC)")]
    [InlineData("[2023] EWCA Civ 123 & 124")]
    public void Stub_WithNCN_AppearsCorrectlyInXml(string ncn)
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Ncn = ncn
        };

        var resultXml = Act(line);

        resultXml.HasSingleNodeWithName("proprietary")
                 .Which().HasChildMatching("uk:cite", ncn);
    }
    
    [Fact]
    public void Stub_WithSingleJurisdiction_AppearsInXmlAsUkJurisdiction()
    {
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Jurisdictions = ["new jurisdiction"]
        };

        var resultXml = Act(line);

        resultXml.HasSingleNodeWithName("proprietary").Which().HasChildMatching("uk:jurisdiction", "new jurisdiction");
    }
    
    [Fact]
    public void Stub_WithMultipleJurisdictions_AppearInXmlAsUkJurisdictions()
    {
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Jurisdictions = ["new jurisdiction", "other new jurisdiction"]
        };

        var resultXml = Act(line);

        resultXml.HasSingleNodeWithName("proprietary").Which()
                 .HasChildMatching("uk:jurisdiction", "new jurisdiction")
                 .And().HasChildMatching("uk:jurisdiction", "other new jurisdiction");
    }

    [Fact]
    public void Stub_WithNoJurisdiction_DoesNotAppearInXml()
    {
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Jurisdictions = []
        };

        var resultXml = Act(line);

        resultXml.DoesNotHaveNodeWithName("uk:jurisdiction");
    }

    [Fact]
    public void Stub_WithWebArchivingLink_AppearsInXmlAsUkWebarchiving()
    {
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            WebArchiving = "a web archive link"
        };

        var resultXml = Act(line);

        resultXml.HasSingleNodeWithName("proprietary").Which()
                 .HasChildMatching("uk:webarchiving", "a web archive link");
    }

    [Fact]
    public void Stub_WithNoWebArchivingLink_DoesNotAppearInXml()
    {
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            WebArchiving = null
        };

        var resultXml = Act(line);

        resultXml.DoesNotHaveNodeWithName("uk:webarchiving");
    }

    [Fact]
    public void Stub_WithDummyDate_AppearsInXmlAsDummyDate()
    {
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            DecisionDateTime = dummyDate
        };

        var resultXml = Act(line);

        resultXml.HasSingleNodeWithName("FRBRWork").Which().HasChildWithName("FRBRdate").Which().HasAttribute("name", "dummy").And().HasAttribute("date", "1000-01-01");
    }

    [Fact]
    public void Stub_WithNormalDate_AppearsInXmlAsNonDummyDate()
    {
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            DecisionDateTime = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc)
        };

        var resultXml = Act(line);

        resultXml.HasSingleNodeWithName("FRBRWork").Which().HasChildWithName("FRBRdate").Which()
                 .HasAttribute("name", "decision").And().HasAttribute("date", "2025-01-01");
    }

    private static XmlDocument Act(CsvLine line)
    {
        var metadata = MetadataTransformer.MakeMetadata(line);

        var stub = Stub.Make(metadata);
        var xml = stub.Serialize();

        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc;
    }
}
