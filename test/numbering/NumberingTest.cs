
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments.DOCX;

using Xunit;

namespace test.numbering;

public class NumberingTest
{
    [Theory]
    [InlineData("numbering1")]
    [InlineData("numbering2")]
    public void NumberingSamples(string name)
    {
        byte[] docBytes = ReadResourceBytes($"test.numbering.{name}.docx");
        int[][] expected = ReadResourceJson<int[][]>($"test.numbering.{name}.json") ?? [];

        using MemoryStream ms = new(docBytes);
        using WordprocessingDocument doc = WordprocessingDocument.Open(ms, false);
        MainDocumentPart main = doc.MainDocumentPart!;

        List<List<int>> actual = new();
        foreach (Paragraph paragraph in main.Document.Body.Elements<Paragraph>())
        {
            var tuple = UK.Gov.Legislation.Judgments.DOCX.Numbering.GetNumberingIdAndIlvl(main, paragraph);
            int? numId = tuple.Item1;
            int ilvl = tuple.Item2;
            if (!numId.HasValue)
                continue;
            int value = Numbering3.CalculateN(paragraph, ilvl);
            actual.Add(BuildStack(main, paragraph, ilvl, value));
        }

        Assert.Equal(expected, actual.Select(a => a.ToArray()).ToArray());
    }

    private static List<int> BuildStack(MainDocumentPart main, Paragraph paragraph, int level, int current)
    {
        List<int> stack = new() { current };
        Paragraph? cursor = paragraph;
        while (level > 0)
        {
            cursor = cursor?.PreviousSibling<Paragraph>();
            while (cursor is not null)
            {
                var tuple = UK.Gov.Legislation.Judgments.DOCX.Numbering.GetNumberingIdAndIlvl(main, cursor);
                int? candidateLevel = cursor.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
                if (tuple.Item1.HasValue && candidateLevel.HasValue && candidateLevel.Value == level - 1)
                {
                    int val = Numbering3.CalculateN(cursor, candidateLevel.Value);
                    stack.Insert(0, val);
                    level = candidateLevel.Value;
                    cursor = cursor.PreviousSibling<Paragraph>();
                    break;
                }
                cursor = cursor.PreviousSibling<Paragraph>();
            }
            if (cursor is null)
                break;
        }
        return stack;
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
