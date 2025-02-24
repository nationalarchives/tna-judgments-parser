
using System;
using System.IO;
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

        internal class Source {
            public string Filename { get; init; }
            public byte[] Content { get; init; }
            public string MimeType { get; init; }
        }

        private static string Hash(byte[] content) {
            byte[] hash = SHA256.HashData(content);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        }

        internal static Bundle Make(Source source, Judgments.Api.Response response)
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
                            Log = null
                        }
                    },
                    PARSER = response.Meta,
                    IngestorOptions = new IngestorOptions()
                    {
                        AutoPublish = false,
                        Source = new() {
                            Format = source.MimeType,
                            Hash = Hash(source.Content)
                        }
                    }
                }
            };
            using var memStream = new MemoryStream();
            var gz = new GZipOutputStream(memStream);
            var tar = new TarOutputStream(gz, Encoding.UTF8);
            WriteSource(source.Content, source.Filename, tar);
            WriteXml(response.Xml, metadata.Parameters.TRE.Payload.Xml, tar);
            WriteMetadata(metadata, tar);
            tar.Close();
            gz.Close();
            byte[] tarGz = memStream.ToArray();
            return new() {
                Uuid = uuid,
                Data = metadata,
                TarGz = tarGz
            };
        }

        private static void WriteSource(byte[] file, string filename, TarOutputStream tar)
        {
            Write(file, filename, tar);
        }

        private static void WriteXml(string xml, string filename, TarOutputStream tar)
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            Write(bytes, filename, tar);
        }

        private static void WriteMetadata(Metadata metadata, TarOutputStream tar)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(metadata, metadata.Options);
            var name = metadata.Parameters.TRE.Payload.Metadata;
            Write(json, name, tar);
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

        }

        public class IngestorOptions
        {

            [JsonPropertyName("auto_publish")]
            public bool AutoPublish { get; set; }


            [JsonPropertyName("source_document")]
            public SourceDocument Source { get; set; }

            public class SourceDocument {

                [JsonPropertyName("format")]
                public string Format { get; set; }

                [JsonPropertyName("file_hash")]
                public string Hash { get; set; }

            }

        }

    }

}
