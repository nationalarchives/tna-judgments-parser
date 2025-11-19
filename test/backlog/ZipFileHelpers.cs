#nullable enable

using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

using Xunit;

namespace test.backlog;

public static class ZipFileHelpers
{
    public static string GetFileFromZippedContent(byte[] content, string fileRegexPattern)
    {
        using var gzipStream = new GZipInputStream(new MemoryStream(content));
        using var archive = new TarInputStream(gzipStream, Encoding.UTF8);

        var entry = archive.GetNextEntry();
        while (entry != null && !Regex.IsMatch(entry.Name, fileRegexPattern))
        {
            entry = archive.GetNextEntry();
        }

        Assert.True(entry is not null, $"Could not find file in content matching {fileRegexPattern}");

        using var reader = new StreamReader(archive);
        return reader.ReadToEnd();
    }   
}
