#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Backlog.Src;

using TRE.Metadata;
using TRE.Metadata.Enums;

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
}
