#nullable enable

using Backlog.Src;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace test.backlog.MetadataTests;

public class TestCreateFullTreMetadata
{
    [Fact]
    public void CreateFullTreMetadata_ContainsSourceContent()
    {
        // Arrange
        var responseMeta = new Api.Meta
        {
            DocumentType = "decision",
            Court = "test-court",
            Date = "2025-07-30"
        };

        // Act
        var result =
            MetadataTransformer.CreateFullTreMetadata("test.pdf", "application/pdf", "1234-456-789", true, [],
                responseMeta);

        // Assert
        Assert.NotNull(result.Parameters.IngestorOptions.Source);
        Assert.Equal("application/pdf", result.Parameters.IngestorOptions.Source.Format);
        Assert.Equal("test.pdf", result.Parameters.TRE.Payload.Filename);
    }

    [Fact]
    public void CreateFullTreMetadata_ContainsApiResponse()
    {
        // Arrange
        var responseMeta = new Api.Meta
        {
            DocumentType = "decision",
            Court = "test-court",
            Date = "2025-07-30"
        };

        // Act
        var result =
            MetadataTransformer.CreateFullTreMetadata("test.pdf", "application/pdf", "1234-456-789", true, [],
                responseMeta);

        // Assert
        Assert.NotNull(result.Parameters.PARSER);
        Assert.Equal("decision", result.Parameters.PARSER.DocumentType);
        Assert.Equal("test-court", result.Parameters.PARSER.Court);
    }
}
