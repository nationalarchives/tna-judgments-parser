
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments.DOCX;

using Xunit;

namespace test.numbering;

public class NumberingTest
{
    [Fact]
    public void Numbering1()
    {
        byte[] docBytes = ReadResourceBytes("test.numbering.numbering1.docx");
        int[] expected = ReadResourceJson<int[]>("test.numbering.numbering1.json") ?? [];

        using MemoryStream ms = new(docBytes);
        using WordprocessingDocument doc = WordprocessingDocument.Open(ms, false);
        MainDocumentPart main = doc.MainDocumentPart!;

        List<int> actual = new();
        foreach (Paragraph paragraph in main.Document.Body.Elements<Paragraph>())
        {
            var tuple = UK.Gov.Legislation.Judgments.DOCX.Numbering.GetNumberingIdAndIlvl(main, paragraph);
            int? numId = tuple.Item1;
            int ilvl = tuple.Item2;
            if (!numId.HasValue)
                continue;
            actual.Add(Numbering3.CalculateN(paragraph, ilvl));
        }

        Assert.Equal(expected, actual);
    }

    private static byte[] ReadResourceBytes(string name)
    {
        Assembly assembly = typeof(NumberingTest).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(name)!;
        using MemoryStream ms = new();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static T? ReadResourceJson<T>(string name)
    {
        Assembly assembly = typeof(NumberingTest).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<T>(stream);
    }
}
