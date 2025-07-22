
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

using Judgments = UK.Gov.NationalArchives.Judgments;

namespace Backlog.Src
{

    class Bundle
    {

        internal string Uuid { get; init; }

        internal Metadata Data { get; init; }

        internal byte[] TarGz { get; init; }

        internal class Source
        {
            public string Filename { get; init; }
            public byte[] Content { get; init; }
            public string MimeType { get; init; }
        }

        private static string Hash(byte[] content)
        {
            byte[] hash = SHA256.HashData(content);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        }

        internal static Bundle Make(Source source, Judgments.Api.Response response, List<CustomField> customMetadata, bool autoPublish = false)
        {
            string uuid = Guid.NewGuid().ToString();
            Metadata metadata = new()
            {
                Parameters = new Parameters
                {
                    TRE = new TRE.Metadata
                    {
                        Reference = uuid,
                        Payload = new TRE.Payload
                        {
                            Filename = source.Filename,
                            Images = response.Images is null ? [] : [.. response.Images.Select(i => i.Name)],
                            Log = null
                        }
                    },
                    PARSER = response.Meta,
                    IngestorOptions = new IngestorOptions()
                    {
                        AutoPublish = autoPublish,
                        Source = new()
                        {
                            Format = source.MimeType,
                            Hash = Hash(source.Content)
                        }
                    },
                    CustomFields = customMetadata
                }
            };
            using var memStream = new MemoryStream();
            var gz = new GZipOutputStream(memStream);
            var tar = new TarOutputStream(gz, Encoding.UTF8);
            WriteSource(source.Content, uuid, source.Filename, tar);
            WriteXml(response.Xml, uuid, metadata.Parameters.TRE.Payload.Xml, tar);
            WriteMetadata(metadata, uuid, tar);
            WriteImages(response.Images, uuid, tar);
            tar.Close();
            gz.Close();
            byte[] tarGz = memStream.ToArray();
            return new()
            {
                Uuid = uuid,
                Data = metadata,
                TarGz = tarGz
            };
        }

        private static void WriteSource(byte[] file, string uuid, string filename, TarOutputStream tar)
        {
            var name = uuid + "/" + filename;
            Write(file, name, tar);
        }

        private static void WriteXml(string xml, string uuid, string filename, TarOutputStream tar)
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            var name = uuid + "/" + filename;
            Write(bytes, name, tar);
        }

        private static void WriteMetadata(Metadata metadata, string uuid, TarOutputStream tar)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(metadata, metadata.Options);
            var name = uuid + "/" + metadata.Parameters.TRE.Payload.Metadata;
            Write(json, name, tar);
        }

        private static void WriteImages(IEnumerable<Judgments.Api.Image> images, string uuid, TarOutputStream tar)
        {
            if (images is null)
                return;
            foreach (var image in images)
            {
                var name = uuid + "/" + image.Name;
                Write(image.Content, name, tar);
            }
        }

        private static void Write(byte[] data, string name, TarOutputStream tar)
        {
            var entry = TarEntry.CreateTarEntry(name);
            entry.Size = data.Length;
            tar.PutNextEntry(entry);
            tar.Write(data, 0, data.Length);
            tar.CloseEntry();
        }

        internal class Metadata
        {

            public readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            public Parameters Parameters { get; set; }

        }

        internal class Parameters
        {

            [JsonPropertyName("TRE")]
            public TRE.Metadata TRE { get; set; }

            [JsonPropertyName("PARSER")]
            public UK.Gov.NationalArchives.Judgments.Api.Meta PARSER { get; set; }

            [JsonPropertyName("INGESTER_OPTIONS")]
            public IngestorOptions IngestorOptions { get; set; }

            [JsonPropertyName("CUSTOM_METADATA")]
            public List<CustomField> CustomFields  { get; set; }

        }

        public class IngestorOptions
        {

            [JsonPropertyName("auto_publish")]
            public bool AutoPublish { get; set; }


            [JsonPropertyName("source_document")]
            public SourceDocument Source { get; set; }

            public class SourceDocument
            {

                [JsonPropertyName("format")]
                public string Format { get; set; }

                [JsonPropertyName("file_hash")]
                public string Hash { get; set; }

            }

        }

        public class CustomField
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("source")]
            public string Source { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }

        }

    }

}
