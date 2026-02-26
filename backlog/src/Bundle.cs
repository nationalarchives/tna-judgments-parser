#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using Backlog.TreMetadata;

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

using UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src
{
    internal class Bundle
    {
        internal string Uuid { get; init; }
        internal byte[] TarGz { get; init; }

        internal static Bundle Make(Response response, FullTreMetadata fullTreMetadata, byte[] sourceContent,
            string sourceFilename, IEnumerable<Image> images)
        {
            var treReference = fullTreMetadata.Parameters.TRE.Reference;

            using var memStream = new MemoryStream();
            var gz = new GZipOutputStream(memStream);
            var tar = new TarOutputStream(gz, Encoding.UTF8);

            Write(sourceContent, $"{treReference}/{sourceFilename}", tar);
            WriteXml(response.Xml, $"{treReference}/{fullTreMetadata.Parameters.TRE.Payload.Xml}", tar);
            WriteMetadata(fullTreMetadata, $"{treReference}/{fullTreMetadata.Parameters.TRE.Payload.Metadata}", tar);

            foreach (var image in images)
            {
                Write(image.Content, $"{treReference}/{image.Name}", tar);
            }

            tar.Close();
            gz.Close();

            var tarGz = memStream.ToArray();

            return new Bundle
            {
                Uuid = treReference,
                TarGz = tarGz
            };
        }

        private static void WriteXml(string xml, string name, TarOutputStream tar)
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            Write(bytes, name, tar);
        }

        private static void WriteMetadata(FullTreMetadata metadata, string name, TarOutputStream tar)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(metadata, metadata.Options);
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
    }
}
