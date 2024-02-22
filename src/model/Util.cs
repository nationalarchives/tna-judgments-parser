using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using UK.Gov.NationalArchives.CaseLaw;

namespace UK.Gov.Legislation.Judgments {

class Util {

    internal static Func<IBlock, IEnumerable<ILine>> GetLines = (block) => {
        if (block is ILine line)
            return new List<ILine>(1) { line };
        if (block is IOldNumberedParagraph np)
            return new List<ILine>(1) { np };
        if (block is ITable table) {
            var cells = table.Rows.SelectMany(row => row.Cells);
            var blocks = cells.SelectMany(cell => cell.Contents);
            return blocks.SelectMany(GetLines);
        }
        if (block is IQuotedStructure quote)
            return quote.Contents.SelectMany(GetBlocksFromDivision).SelectMany(GetLines);
        if (block is IDivWrapper wrapper)
            return GetBlocksFromDivision(wrapper.Division).SelectMany(GetLines);
        if (block is ITableOfContents2 toc)
            return toc.Contents;
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

    private static IEnumerable<IBlock> GetBlocksFromDecision(IDecision dec) {
        return dec.Contents.SelectMany(GetBlocksFromDivision);
    }
    private static IEnumerable<IBlock> GetBlocksFromDivision(IDivision div) {
        if (div is ILeaf leaf) {
            if (leaf.Heading is null)
                return leaf.Contents;
            return leaf.Contents.Prepend(leaf.Heading);
        }
        if (div is IBranch branch) {
            List<IBlock> blocks = new List<IBlock>();
            if (branch.Heading is not null)
                blocks.Add(branch.Heading);
            if (branch.Intro is not null)
                blocks.AddRange(branch.Intro);
            foreach (var child in branch.Children)
                blocks.AddRange(GetBlocksFromDivision(child));
            if (branch.WrapUp is not null)
                blocks.AddRange(branch.WrapUp);
            return blocks;
        }
        if (div is ITableOfContents toc)
            return toc.Contents;
        throw new Exception();
    }

    public static IEnumerable<T> Descendants<T>(IAknDocument doc) {
        return Enumerable.Concat(
            Descendants<T>(doc.Preface),
            Descendants<T>(doc.Body)
        );
    }

    public static IEnumerable<T> Descendants<T>(IJudgment judgment) {
        var listOfLists = new List<IEnumerable<T>> {
            Descendants<T>(judgment.CoverPage),
            Descendants<T>(judgment.Header),
            Descendants<T>(judgment.Body),
            Descendants<T>(judgment.Conclusions),
            Descendants<T>(judgment.Annexes.SelectMany(a => a.Contents)),
            Descendants<T>(judgment.InternalAttachments.SelectMany(a => a.Contents))
        };
        return listOfLists.SelectMany(x => x);
    }

    public static IEnumerable<T> Descendants<T>(IEnumerable<IDecision> decisions) {
        return decisions.SelectMany(GetBlocksFromDecision)
            .SelectMany(GetLines)
            .SelectMany(line => line.Contents)
            .OfType<T>();
    }
    public static IEnumerable<T> Descendants<T>(IEnumerable<IDivision> divisions) {
        return divisions.SelectMany(GetBlocksFromDivision).SelectMany(GetLines).SelectMany(line => line.Contents).OfType<T>();
    }
    public static IEnumerable<T> Descendants<T>(IEnumerable<IBlock> blocks) {
        if (blocks is null)
            return Enumerable.Empty<T>();
        return blocks.SelectMany(GetLines).SelectMany(line => line.Contents).OfType<T>();
    }
    public static IEnumerable<T> Descendants<T>(IBlock block) {
        return GetLines(block).SelectMany(line => line.Contents).OfType<T>();
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
