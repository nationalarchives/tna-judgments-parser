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
            var bundle = Bundle.Make(source, response);

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
            var bundle = Bundle.Make(source, response);

            // Assert
            Assert.NotNull(bundle.Data.Parameters.PARSER);
            Assert.Equal("decision", bundle.Data.Parameters.PARSER.DocumentType);
            Assert.Equal("test-court", bundle.Data.Parameters.PARSER.Court);
        }
    }
}
