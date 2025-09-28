using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using UK.Gov.NationalArchives.Judgments;
using Api = UK.Gov.NationalArchives.Judgments.Api;
using Backlog.Src;

namespace Backlog.Test
{
    [TestFixture]
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

        [Test]
        public void TestBundleContainsSourceContent()
        {
            // Arrange
            var source = CreateTestSource();
            var response = CreateTestResponse();

            // Act
            var bundle = Bundle.Make(source, response, null);

            // Assert
            Assert.That(bundle.Data.Parameters.IngestorOptions.Source, Is.Not.Null);
            Assert.That(bundle.Data.Parameters.IngestorOptions.Source.Format, Is.EqualTo("application/pdf"));
            Assert.That(bundle.Data.Parameters.TRE.Payload.Filename, Is.EqualTo("test.pdf"));
        }

        [Test]
        public void TestBundleContainsApiResponse()
        {
            // Arrange
            var source = CreateTestSource();
            var response = CreateTestResponse();

            // Act
            var bundle = Bundle.Make(source, response, null);

            // Assert
            Assert.That(bundle.Data.Parameters.PARSER, Is.Not.Null);
            Assert.That(bundle.Data.Parameters.PARSER.DocumentType, Is.EqualTo("decision"));
            Assert.That(bundle.Data.Parameters.PARSER.Court, Is.EqualTo("test-court"));
        }

        [Test]
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
            using var memStream = new MemoryStream(bundle.TarGz);
            using var gzStream = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(memStream);
            using var tarStream = new ICSharpCode.SharpZipLib.Tar.TarInputStream(gzStream, System.Text.Encoding.UTF8);

            var entry = tarStream.GetNextEntry();
            while (entry != null && !entry.Name.EndsWith("metadata.json"))
            {
                entry = tarStream.GetNextEntry();
            }

            Assert.That(entry, Is.Not.Null, "metadata.json not found in tar.gz");

            // Read the metadata.json content
            using var reader = new StreamReader(tarStream);
            var jsonContent = reader.ReadToEnd();
            var metadata = JsonSerializer.Deserialize<Bundle.Metadata>(jsonContent,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });


            // Assert
            Assert.That(metadata.Parameters.CustomFields, Is.Not.Null);
            Assert.That(metadata.Parameters.CustomFields.Count, Is.EqualTo(1));
            Assert.That(metadata.Parameters.CustomFields[0].Name, Is.EqualTo("headnote_summary"));
            Assert.That(metadata.Parameters.CustomFields[0].Value, Is.EqualTo("Test headnote"));
            

            Assert.That(bundle.Data.Parameters.CustomFields, Is.Not.Null);
            Assert.That(bundle.Data.Parameters.CustomFields.Count, Is.EqualTo(1));
            Assert.That(bundle.Data.Parameters.CustomFields[0].Name, Is.EqualTo("headnote_summary"));
            Assert.That(bundle.Data.Parameters.CustomFields[0].Value, Is.EqualTo("Test headnote"));
        }
    }
}
