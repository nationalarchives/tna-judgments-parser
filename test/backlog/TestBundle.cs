using System.Collections.Generic;
using System.Text.Json;

using Backlog.Src;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace test.backlog
{
    public class TestBundle
    {
        private Bundle.Source CreateTestSource()
        {
            return new Bundle.Source
            {
                Filename = "test.pdf",
                Content = new byte[] { 0x1, 0x2, 0x3 },
                MimeType = "application/pdf"
            };
        }

        private Api.Response CreateTestResponse()
        {
            return new Api.Response
            {
                Xml = "<test></test>",
                Meta = new Api.Meta
                {
                    DocumentType = "decision",
                    Court = "test-court",
                    Date = "2025-07-30"
                }
            };
        }

        [Fact]
        public void TestBundleContainsSourceContent()
        {
            // Arrange
            var source = CreateTestSource();
            var response = CreateTestResponse();

            // Act
            var bundle = Bundle.Make(source, response, null);

            // Assert
            Assert.NotNull(bundle.Data.Parameters.IngestorOptions.Source);
            Assert.Equal("application/pdf", bundle.Data.Parameters.IngestorOptions.Source.Format);
            Assert.Equal("test.pdf", bundle.Data.Parameters.TRE.Payload.Filename);
        }

        [Fact]
        public void TestBundleContainsApiResponse()
        {
            // Arrange
            var source = CreateTestSource();
            var response = CreateTestResponse();

            // Act
            var bundle = Bundle.Make(source, response, null);

            // Assert
            Assert.NotNull(bundle.Data.Parameters.PARSER);
            Assert.Equal("decision", bundle.Data.Parameters.PARSER.DocumentType);
            Assert.Equal("test-court", bundle.Data.Parameters.PARSER.Court);
        }

        [Fact]
        public void TestCustomFieldsAreSerializedInTarGzAndBundleData()
        {
            // Arrange
            var source = CreateTestSource();
            var response = CreateTestResponse();
            var customFields = new List<Bundle.CustomField>
            {
                new Bundle.CustomField
                {
                    Name = "headnote_summary",
                    Source = "test-court",
                    Value = "Test headnote"
                }
            };

            // Act
            var bundle = Bundle.Make(source, response, customFields);

            // Assert - Extract and verify metadata.json from TarGz
            var jsonContent = ZipFileHelpers.GetFileFromZippedContent(bundle.TarGz, @"metadata\.json$");
            var metadata = JsonSerializer.Deserialize<Bundle.Metadata>(jsonContent,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });


            // Assert
            Assert.NotNull(metadata.Parameters.CustomFields);
            var metadataCustomFields = Assert.Single(metadata.Parameters.CustomFields);
            Assert.Equal("headnote_summary", metadataCustomFields.Name);
            Assert.Equal("Test headnote", metadataCustomFields.Value);
            

            Assert.NotNull(bundle.Data.Parameters.CustomFields);
            var bundleCustomFields = Assert.Single(bundle.Data.Parameters.CustomFields);
            Assert.Equal("headnote_summary", bundleCustomFields.Name);
            Assert.Equal("Test headnote", bundleCustomFields.Value);
        }
    }
}
