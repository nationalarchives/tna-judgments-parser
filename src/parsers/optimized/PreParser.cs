
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Vml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;


namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class WordDocument {

    public IEnumerable<IBlock> Header { get; init; }

    public List<BlockWithBreak> Body { get; init; }

}

class BlockWithBreak {

    internal IBlock Block { get; init; }

    internal bool LineBreakBefore { get; init; }

}

class PreParser {

    internal WordDocument Parse(WordprocessingDocument doc) {
        return new WordDocument {
            Header = Header(doc.MainDocumentPart),
            Body = Body(doc.MainDocumentPart)
        };
    }

    RemoveTrailingWhitespace removeTrailingWhitespace = new RemoveTrailingWhitespace();
    Merger mergeRuns = new Merger();

    private IEnumerable<IBlock> Header(MainDocumentPart main) {
        Header header = DOCX.Headers.GetFirst(main);
        if (header is null)
            return Enumerable.Empty<IBlock>();
        IEnumerable<IBlock> contents = Blocks.ParseBlocks(main, header.ChildElements)
            .Where(block => block is not ILine line || !line.IsEmpty());
        contents = removeTrailingWhitespace.Enrich(contents);
        contents = mergeRuns.Enrich(contents);
        return contents.ToList();  // toList() is needed for the ImageRef.Src replacement
    }

    internal static bool IsSkippable(OpenXmlElement e) {
        if (e is SectionProperties)
            return true;
        if (e is BookmarkStart || e is BookmarkEnd)
            return true;
        if (e.Descendants().OfType<Drawing>().Any())
            return false;
        if (e.Descendants().OfType<Picture>().Any())
            return false;
        if (e.Descendants().OfType<Shape>().Any())
            return false;
        if (e is Paragraph p) {
            if (DOCX.Paragraphs.IsEmptySectionBreak(p))
                return true;
            if (DOCX.Numbering2.HasOwnNumber(p))
                return false;
            if (DOCX.Numbering2.HasEffectiveStyleNumber(p))
                return false;
            if (string.IsNullOrWhiteSpace(p.InnerText))
                return true;
        }
        if (e is PermEnd)
            return true;
        return false;
    }

    private List<BlockWithBreak> Body(MainDocumentPart main) {
        List<BlockWithBreak> contents = new List<BlockWithBreak>();
        bool lineBreakBefore = false;
        foreach (var e in main.Document.Body.ChildElements) {
            lineBreakBefore = lineBreakBefore || Util.IsSectionOrPageBreak(e);
            if (IsSkippable(e))
                continue;
            IEnumerable<IBlock> blocks = ParseElement(main, e);
            foreach (IBlock block in blocks) {
                var enriched = removeTrailingWhitespace.Enrich1(block);
                enriched = mergeRuns.Enrich1(enriched);
                if (block is ILine line && block is not IOldNumberedParagraph && !line.Contents.Any())
                    continue;
                contents.Add(new BlockWithBreak { Block = enriched, LineBreakBefore = lineBreakBefore });
                lineBreakBefore = false;
            }
        }
        contents = ExtractHardNumbers(contents).ToList();
        return contents;
    }

    private static IEnumerable<IBlock> ParseElement(MainDocumentPart main, OpenXmlElement e) {
        if (e is Paragraph p)
            return new List<IBlock>(1) { ParseParagraph(main, p) };
        if (e is Table table)
            return new List<IBlock>(1) { new WTable(main, table) };
        if (e is SdtBlock sdt)
            return Blocks.ParseStdBlock(main, sdt);
        throw new System.Exception(e.GetType().ToString());
    }

    private static WLine ParseParagraph(MainDocumentPart main, Paragraph p) {
        DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, p);
        if (info.HasValue)
            return new WOldNumberedParagraph(info.Value, main, p);
        WLine line = new WLine(main, p);
        INumber num2 = Fields.RemoveListNum(line);  // mutates line if successful!
        if (num2 is not null)
            return new WOldNumberedParagraph(num2, line);
        return line;
    }

    /* hard numbers */

    private static readonly string PlainNumberFormat = @"^[“""]?\d+$";

    internal static readonly string[] NumberFormats = new string[] {
        @"^([“""]?\d+\.)",    @"^([“""]?\(\d+\))",
        @"^([“""]?[A-Z]\.)",  @"^([“""]?\([A-Z]\))",
        @"^([“""]?[a-z]\.)",  @"^([“""]?\([a-z]\))",
        @"^([“""]?[ivx]+\.)", @"^([“""]?\([ivx]+\))"
    }.Select(s => s + "( |$)").ToArray();

    private static IEnumerable<BlockWithBreak> ExtractHardNumbers(IEnumerable<BlockWithBreak> contents) {
        return contents.Select(bb => ExtractHardNumber(bb));
    }

    private static BlockWithBreak ExtractHardNumber(BlockWithBreak bb) {
        if (bb.Block is not WLine line)
            return bb;
        WOldNumberedParagraph removed = ExtractHardNumber(line);
        if (removed is null)
            return bb;
        return new BlockWithBreak { Block = removed, LineBreakBefore = bb.LineBreakBefore };
    }

    private static WOldNumberedParagraph ExtractHardNumber(WLine line) {
        if (line is WOldNumberedParagraph)
            return null;
        WOldNumberedParagraph removed = ExtractPlainNumber(line);
        if (removed is not null)
            return removed;
        foreach (string format in NumberFormats) {
            removed = ExtractNumberWithFormat(line, format);
            if (removed is not null)
                return removed;
        }
        return null;
    }

    internal static WOldNumberedParagraph ExtractPlainNumber(WLine line) {
        if (line is WOldNumberedParagraph)
            return null;
        if (line.Contents.FirstOrDefault() is not WText first)
            return null;
        if (line.Contents.Skip(1).FirstOrDefault() is not WTab)
            return null;
        if (!Regex.IsMatch(first.Text, PlainNumberFormat))
            return null;
        return new WOldNumberedParagraph(first, line.Contents.Skip(2), line);
    }

    internal static WOldNumberedParagraph ExtractNumberWithFormat(WLine line, string format) {
        if (line is WOldNumberedParagraph)
            return null;
        IEnumerable<IInline> contents = line.Contents;
        contents = contents.SkipWhile(first => first is WTab || first is WLineBreak);
        if (contents.FirstOrDefault() is not WText first)
            return null;
        IEnumerable<IInline> rest = contents.Skip(1);
        string trimmed = first.Text.TrimStart();
        RunProperties firstProps = first.properties;
        RunProperties lastProps = first.properties;
        Match match = Regex.Match(trimmed, format);
        if (!match.Success && trimmed.Length < 5 && rest.FirstOrDefault() is WText second) {
            trimmed = trimmed + second.Text;
            lastProps = second.properties;
            rest = rest.Skip(1);
            match = Regex.Match(trimmed, format);
        }
        if (!match.Success)
            return null;
        WText num = new WText(match.Groups[1].Value, firstProps);
        string after = trimmed.Substring(num.Text.Length).TrimStart();
        if (!string.IsNullOrEmpty(after)) {
            WText after2 = new WText(after, lastProps);
            rest = rest.Prepend(after2);
        } else if (rest.FirstOrDefault() is WTab) {
            rest = rest.Skip(1);
        }
        if (rest.FirstOrDefault() is WText next) {
            string nextTrimmed = next.Text.TrimStart();
            if (nextTrimmed != next.Text) {
                WText replacement = new WText(next.Text.TrimStart(), next.properties);
                rest = rest.Skip(1).Prepend(replacement);
            }
        }
        return new WOldNumberedParagraph(num, rest, line);
    }

}

}
