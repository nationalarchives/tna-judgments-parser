#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Backlog.Src;

using TRE.Metadata;
using TRE.Metadata.Enums;
using TRE.Metadata.MetadataFieldTypes;

using UK.Gov.Legislation.Judgments;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using Party = UK.Gov.NationalArchives.CaseLaw.Model.Party;

namespace test.backlog.MetadataTests;

public class TestMetadataTransformer
{
    [Fact]
    public void CreateFullTreMetadata_SetsIngestorOptions()
    {
        const bool autoPublish = true;
        const string contentHash = "123-456-789";
        const string sourceMimeType = "application/pdf";
        var responseMeta = new Api.Meta { DocumentType = "decision" };

        // Act
        var result = MetadataTransformer.CreateFullTreMetadata("test.pdf", sourceMimeType, contentHash, autoPublish, [],
            responseMeta, [], false);

        // Assert
        Assert.Equal(autoPublish, result.Parameters.IngestorOptions.AutoPublish);
        Assert.Equal(sourceMimeType, result.Parameters.IngestorOptions.Source.Format);
        Assert.Equal(contentHash, result.Parameters.IngestorOptions.Source.Hash);
    }

    [Fact]
    public void CreateFullTreMetadata_SetsParserMetadata()
    {
        // Arrange
        const string court = "test-court";
        const string cite = "[2026] IMTU 3312";
        const string date = "2025-07-30";
        const string name = "a v b";
        var extensions = new Api.Extensions
        {
            Parties =
            [
                new Party { Name = "a", Role = PartyRole.Appellant },
                new Party { Name = "b", Role = PartyRole.Respondent }
            ],
            WebArchivingLink = "https://webarchivinglink"
        };
        const bool xmlContainsDocumentText = true;

        var responseMeta = new Api.Meta
        {
            DocumentType = "decision",
            Cite = cite,
            Court = court,
            Date = date,
            Extensions = extensions,
            Name = name
        };

        // Act
        List<IMetadataField> externalMetadataFields = [];

        var result = MetadataTransformer.CreateFullTreMetadata("test.docx", "application/pdf", "1234-456-789", true, [],
            responseMeta, externalMetadataFields, xmlContainsDocumentText);

        // Assert
        Assert.Null(result.Parameters.PARSER.Uri);
        Assert.Equal(court, result.Parameters.PARSER.Court);
        Assert.Equal(cite, result.Parameters.PARSER.Cite);
        Assert.Equal(date, result.Parameters.PARSER.Date);
        Assert.Equal(name, result.Parameters.PARSER.Name);
        Assert.Equal(extensions, result.Parameters.PARSER.Extensions);
        Assert.Empty(result.Parameters.PARSER.Attachments);
        Assert.Equal(DocumentType.Decision, result.Parameters.PARSER.DocumentType);
        Assert.Empty(result.Parameters.PARSER.ErrorMessages);
        Assert.Equal(externalMetadataFields, result.Parameters.PARSER.MetadataFields);
        Assert.Equal(xmlContainsDocumentText, result.Parameters.PARSER.XmlContainsDocumentText);
    }

    [Fact]
    public void CreateFullTreMetadata_SetsTrePayload()
    {
        // Arrange
        const string sourceFilename = "test-file.docx";
        Api.Image[] images =
        [
            new() { Name = "img1.png" },
            new() { Name = "img2.png" }
        ];

        // Act
        var result = MetadataTransformer.CreateFullTreMetadata(
            sourceFilename,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "sha256:abc",
            false,
            images,
            new Api.Meta { DocumentType = "decision" },
            [],
            false
        );

        // Assert
        Assert.Equal(sourceFilename, result.Parameters.TRE.Payload.Filename);
        Assert.Null(result.Parameters.TRE.Payload.Log);
        Assert.Equal(["img1.png", "img2.png"], result.Parameters.TRE.Payload.Images);
    }

    [Fact]
    public void CreateFullTreMetadata_Generates_UniqueReference()
    {
        var firstFullTreMetadata = MetadataTransformer.CreateFullTreMetadata(
            "test-file.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "sha256:abc",
            false,
            [],
            new Api.Meta { DocumentType = "decision" },
            [],
            false
        );
        var secondFullTreMetadata = MetadataTransformer.CreateFullTreMetadata(
            "test-file.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "sha256:abc",
            false,
            [],
            new Api.Meta { DocumentType = "decision" },
            [],
            false
        );

        // Assert - unique reference is generated
        Assert.NotEqual(firstFullTreMetadata.Parameters.TRE.Reference, secondFullTreMetadata.Parameters.TRE.Reference);

        //Assert - all other properties are the same
        Assert.EquivalentWithExclusions(firstFullTreMetadata, secondFullTreMetadata,
            fullTreMetadata => fullTreMetadata.Parameters.TRE.Reference);
    }


    [Fact]
    public void CsvLineToMetadataFields_AllFields_HaveExternalSource()
    {
        var metadataLine = CsvMetadataLineHelper.DummyLineWithClaimants;

        var result = MetadataTransformer.CsvLineToMetadataFields(metadataLine);

        Assert.All(result, field => Assert.Equal(MetadataSource.External, field.Source));
    }

    [Fact]
    public void CsvLineToMetadataFields_AllFields_HaveCurrentTimestamp()
    {
        var metadataLine = CsvMetadataLineHelper.DummyLineWithClaimants;

        var beforeCall = DateTime.UtcNow;
        var result = MetadataTransformer.CsvLineToMetadataFields(metadataLine);
        var afterCall = DateTime.UtcNow;

        Assert.All(result, field => Assert.InRange(field.Timestamp, beforeCall, afterCall));
    }

    [Fact]
    public void CsvLineToMetadataFields_CaseNumber_IsMapped()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { CaseNo = "XYZ/123" };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasSingleMetadataFieldWithValue("XYZ/123", MetadataFieldName.CaseNumber, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_Court_IsMapped()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { court = "UKFTT-GRC" };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasSingleMetadataFieldWithValue("UKFTT-GRC", MetadataFieldName.Court, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_Date_IsMapped()
    {
        var decisionDatetime = new DateTime(2024, 2, 1, 10, 30, 0, DateTimeKind.Utc);
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { decision_datetime = decisionDatetime };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasSingleMetadataFieldWithValue(decisionDatetime, MetadataFieldName.Date, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_Jurisdictions_AreMapped()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { Jurisdictions = ["Transport", "Tax"] };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasMetadataFieldsWithValues(["Transport", "Tax"], MetadataFieldName.Jurisdiction, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_Parties_AreMapped()
    {
        var csvLine = CsvMetadataLineHelper.DummyLine with { appellants = "Alice", respondent = "HMRC" };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasMetadataFieldsWithValues(csvLine.Parties, MetadataFieldName.Party, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_Categories_AreMapped()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            main_category = "CatA",
            main_subcategory = "SubA",
            sec_category = "CatB"
        };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasMetadataFieldsWithValues(csvLine.Categories, MetadataFieldName.Category, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_CsvMetadataFileProperties_IsMapped()
    {
        var expectedCsvProperties = new CsvProperties(
            "name.csv",
            "hash",
            new Dictionary<string, string> { { "a", "1" }, { "b", "2" } }
        );
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            CsvProperties = (expectedCsvProperties.Name, expectedCsvProperties.Hash),
            FullCsvLineContents = expectedCsvProperties.FullLineContents
        };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasSingleMetadataFieldWithValue(expectedCsvProperties, MetadataFieldName.CsvMetadataFileProperties,
            fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_Ncn_IsIncludedWhenPresent()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { ncn = "NCN123" };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasSingleMetadataFieldWithValue("NCN123", MetadataFieldName.Ncn, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_HeadnoteSummary_IsIncludedWhenPresent()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { headnote_summary = "A summary" };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasSingleMetadataFieldWithValue("A summary", MetadataFieldName.HeadnoteSummary, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_WebArchivingLink_IsIncludedWhenPresent()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with { webarchiving = "http://example.com" };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        AssertHasSingleMetadataFieldWithValue("http://example.com", MetadataFieldName.WebArchivingLink, fields);
    }

    [Fact]
    public void CsvLineToMetadataFields_OptionalFields_AreExcludedWhenNull()
    {
        var csvLine = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            headnote_summary = null,
            ncn = null,
            webarchiving = null
        };

        var fields = MetadataTransformer.CsvLineToMetadataFields(csvLine);

        Assert.DoesNotContain(fields, f => f.Name == MetadataFieldName.HeadnoteSummary);
        Assert.DoesNotContain(fields, f => f.Name == MetadataFieldName.Ncn);
        Assert.DoesNotContain(fields, f => f.Name == MetadataFieldName.WebArchivingLink);
    }

    [Theory]
    [InlineData("appeals\\j2\\R(IS)7-02ws.doc", "R(IS)7-02ws.doc")]
    [InlineData("finance-and-tax/j7/e00417.doc", "e00417.doc")]
    [InlineData("documents\\ICRI Ltd - Out of time decision .pdf",  "ICRI Ltd - Out of time decision .pdf")]
    [InlineData("asylum-support/j12750/Reaosns Statement.24894..doc", "Reaosns Statement.24894..doc")]
    [InlineData("EAT64299, 64399, 64499, 64599, 64699 & 649991372000.doc", "EAT64299, 64399, 64499, 64599, 64699 & 649991372000.doc")]
    [InlineData("file with no extension", "file with no extension")]
    public void GetFileName_ReturnsFileName(string input, string expected)
    {
        var result = MetadataTransformer.GetFileName(input);
        Assert.Equal(expected, result);
    }

    private static void AssertHasSingleMetadataFieldWithValue<T>(T expectedValue, MetadataFieldName metadataFieldName,
        List<IMetadataField> fields)
    {
        var metadataField = Assert.Single(fields, f => f.Name == metadataFieldName);
        var typedMetadataField = Assert.IsType<MetadataField<T>>(metadataField);
        Assert.Equal(expectedValue, typedMetadataField.Value);
    }

    private static void AssertHasMetadataFieldsWithValues<T>(T[] expectedValues, MetadataFieldName metadataFieldName,
        List<IMetadataField> fields)
    {
        var metadataFields = fields.Where(f => f.Name == metadataFieldName);

        Assert.Collection(metadataFields, expectedValues.Select<T, Action<IMetadataField>>(v => metadataField =>
        {
            var typedMetadataField = Assert.IsType<MetadataField<T>>(metadataField);
            Assert.Equivalent(v, typedMetadataField.Value);
        }).ToArray());
    }
}
