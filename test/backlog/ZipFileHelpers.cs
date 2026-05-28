#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace test.backlog;

public static class ZipFileHelpers
{
    public static byte[] GetFileFromZippedContentAsBytes(this byte[] zipFileContent, string fileName)
    {
        using MemoryStream gZipMemoryStream = new(zipFileContent);
        using GZipStream decompressedGZipStream = new(gZipMemoryStream, CompressionMode.Decompress);
        using TarReader tarReader = new(decompressedGZipStream, true);

        var filesFound = new List<string>();
        while (tarReader.GetNextEntry() is { } entry)
        {
            // Tar file names always start with a random GUID - e.g. 3ce8efd6-7524-4ab9-9686-3a0dbd3e5e8e/CCA20120008_20130118_order_appeal_discontinued.pdf
            // Remove this prefix because it's non-deterministic
            var cleansedTarFileEntryName = entry.Name[(entry.Name.IndexOf('/') + 1) ..];
            if (cleansedTarFileEntryName.Equals(fileName, StringComparison.InvariantCultureIgnoreCase))
            {
                var entryDataStream = entry.DataStream;
                if (entryDataStream is null)
                {
                    throw new FileNotFoundException($"File {entry.Name} found but it had no data");
                }

                using var fileMemoryStream = new MemoryStream();
                entryDataStream.CopyTo(fileMemoryStream);
                return fileMemoryStream.ToArray();
            }

            filesFound.Add(cleansedTarFileEntryName);
        }

        throw new FileNotFoundException($"Could not find file \"{fileName}\" in zip file: [{string.Join(", ", filesFound)}]");
    }

    public static string GetFileFromZippedContentAsString(this byte[] content, string fileName)
    {
        var fileContentBytes = content.GetFileFromZippedContentAsBytes(fileName);

        return Encoding.UTF8.GetString(fileContentBytes);
    }
}
