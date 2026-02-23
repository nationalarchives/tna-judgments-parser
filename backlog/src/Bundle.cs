
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Backlog.TreMetadata;

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

using Judgments = UK.Gov.NationalArchives.Judgments;

namespace Backlog.Src
{

    class Bundle
    {

        internal string Uuid { get; init; }

        internal FullTreMetadata Data { get; init; }

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

        internal static Bundle Make(Source source, Judgments.Api.Response response, bool autoPublish = false)
        {
            string uuid = Guid.NewGuid().ToString();

            FullTreMetadata fullTreMetadata = new()
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
            WriteSource(source.Content, uuid, source.Filename, tar);
            WriteXml(response.Xml, uuid, fullTreMetadata.Parameters.TRE.Payload.Xml, tar);
            WriteMetadata(fullTreMetadata, uuid, tar);
            WriteImages(response.Images, uuid, tar);

            tar.Close();
            gz.Close();
            byte[] tarGz = memStream.ToArray();
            return new() {

                Uuid = uuid,
                Data = fullTreMetadata,
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

        private static void WriteMetadata(FullTreMetadata metadata, string uuid, TarOutputStream tar)
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
    }
}
