#nullable enable

using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

using Xunit;

namespace test.backlog;

public static class ZipFileHelpers
{
    public static string GetFileFromZippedContent(byte[] content, string fileRegexPattern)
    {
        using MemoryStream memoryStream = new(content);
        using GZipStream gz = new(memoryStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gz, true);

        while (tarReader.GetNextEntry() is { } entry)
        {
            if (Regex.IsMatch(entry.Name, fileRegexPattern))
            {
                var entryDataStream = entry.DataStream;
                Assert.True(entryDataStream is not null, $"File found matching {fileRegexPattern} but it had no data");

                using var streamReader = new StreamReader(entryDataStream);
                return streamReader.ReadToEnd();
            }
        }

        Assert.Fail($"Could not find file in content matching {fileRegexPattern}");
        return null;
    }
}
