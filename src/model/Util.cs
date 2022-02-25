using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments {

class Util {

    private static Func<IBlock, IEnumerable<ILine>> GetLines = (block) => {
        if (block is ILine line)
            return new List<ILine>(1) { line };
        if (block is IOldNumberedParagraph np)
            return new List<ILine>(1) { np };
        if (block is ITable table) {
            var cells = table.Rows.SelectMany(row => row.Cells);
            var blocks = cells.SelectMany(cell => cell.Contents);
            return blocks.SelectMany(GetLines);
        }
        throw new Exception();
    };

    public static bool IsSectionOrPageBreak(OpenXmlElement e) {
        return IsSectionBreak(e) || IsPageBreak(e);
    }
    public static bool IsSectionBreak(OpenXmlElement e) {
        return e.Descendants<SectionProperties>().Any();
    }
    public static bool IsPageBreak(OpenXmlElement e) {
        return e.Descendants<Break>().Where(br => br.Type?.Value == BreakValues.Page).Any();
    }

    public static IEnumerable<T> Descendants<T>(IEnumerable<IBlock> blocks) {
        return blocks.SelectMany(GetLines).SelectMany(line => line.Contents).OfType<T>();
    }

    public static string NormalizeSpace(string s) {
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    internal static List<Tuple<Tuple<T, AttachmentType>, int>> NumberAttachments<T>(IEnumerable<Tuple<T, AttachmentType>> attachments) {
        Dictionary<AttachmentType, int> dict = new Dictionary<AttachmentType, int>();
        List<Tuple<Tuple<T, AttachmentType>, int>> list = new List<Tuple<Tuple<T, AttachmentType>, int>>();
        foreach (var attach in attachments) {
            if (!dict.ContainsKey(attach.Item2))
                dict.Add(attach.Item2, 0);
            dict[attach.Item2] = dict[attach.Item2] + 1;
            var tuple = new Tuple<Tuple<T, AttachmentType>, int>(attach, dict[attach.Item2]);
            list.Add(tuple);
        }
        return list;
    }

}

}
