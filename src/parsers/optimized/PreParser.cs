
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
            if (DOCX.Numbering2.HasOwnNumber(p))
                return false;
            if (DOCX.Numbering2.HasEffectiveStyleNumber(p) && !DOCX.Paragraphs.IsEmptySectionBreak(p))
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
                contents.Add(new BlockWithBreak { Block = enriched, LineBreakBefore = lineBreakBefore });
                lineBreakBefore = false;
            }
        }
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

    private static readonly string PlainNumberFormat = @"^[“""]?\d+$";
    internal static readonly string[] NumberFormats = {
        @"^([“""]?\d+\.) ",    @"^([“""]?\(\d+\)) ",
        @"^([“""]?[A-Z]\.) ",  @"^([“""]?\([A-Z]\)) ",
        @"^([“""]?[a-z]\.) ",  @"^([“""]?\([a-z]\)) ",
        @"^([“""]?[ivx]+\.) ", @"^([“""]?\([ivx]+\)) "
    };

    private static WLine ParseParagraph(MainDocumentPart main, Paragraph p) {
        DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, p);
        if (info is not null)
            return new WOldNumberedParagraph(info.Value, main, p);
        WLine line = new WLine(main, p);
        INumber num2 = Fields.RemoveListNum(line);
        if (num2 is not null)
            return new WOldNumberedParagraph(num2, line);

        WText plainNum = GetPlainNumberFromParagraph(p);
        if (plainNum is not null) {
            try {
                WLine removed = RemovePlainNumberFromParagraph(main, p);
                removed.IsFirstLineOfNumberedParagraph = true;
                return new WOldNumberedParagraph(plainNum, removed);
            } catch (System.Exception) {
                return line;
            }
        }

        string normalized = NormalizeParagraph(p);
        foreach (string format in NumberFormats) {
            WText num = GetNumberFromParagraph(p, format, normalized);
            if (num is null)
                continue;
            try {
                WLine removed = RemoveNumberFromParagraph(main, p, format);
                removed.IsFirstLineOfNumberedParagraph = true;
                return new WOldNumberedParagraph(num, removed);
            } catch (System.Exception) {
                break;
            }
        }
        return line;
    }

    /* */

    // e.InnerText collapses tabs to the empty string
    private static string NormalizeParagraph(Paragraph e) {
        IEnumerable<string> texts = e.Descendants()
            .Where(e => e is Text || e is TabChar)
            .Select(e => { if (e is Text) return e.InnerText; if (e is TabChar) return " "; return ""; });
        return string.Join("", texts).Trim();
    }

    /// text is normalized
    private static WText GetNumberFromParagraph(Paragraph e, string format, string text) {
        Match match = Regex.Match(text, format);
        if (!match.Success)
            return null;
        string number = match.Groups[1].Value;
        RunProperties rPr = e.Descendants<Run>().FirstOrDefault()?.RunProperties;
        return new WText(number, rPr);
    }

    protected static WLine RemoveNumberFromParagraph(MainDocumentPart main, Paragraph e, string format) {
        IEnumerable<IInline> unfiltered = Inline.ParseRuns(main, e.ChildElements);
        unfiltered = Merger.Merge(unfiltered);
        unfiltered = unfiltered.SkipWhile(inline => inline is WLineBreak || inline is WTab || (inline is WText wText && string.IsNullOrWhiteSpace(wText.Text)));
        IInline first = unfiltered.First();
        if (first is not WText t1)
            throw new Exception();
        Match match = Regex.Match(t1.Text.TrimStart(), format); // TrimStart for 00356_ukut_iac_2019_ms_belgium.doc
        if (match.Success) {
            string rest = t1.Text.TrimStart().Substring(match.Length).TrimStart();
            if (string.IsNullOrEmpty(rest)) {
                var rest2 = unfiltered.Skip(1);
                if (rest2.FirstOrDefault() is WTab)
                    rest2 = rest2.Skip(1);
                return new WLine(main, e, rest2);
            } else {
                WText prepend = new WText(rest, t1.properties);
                return new WLine(main, e, unfiltered.Skip(1).Prepend(prepend));
            }
        }
        match = Regex.Match(t1.Text + " ", format + @"$");
        if (match.Success) {
            IInline second = unfiltered.Skip(1).FirstOrDefault();
            if (second is WText t2) {
                WText prepend = new WText(t2.Text.TrimStart(), t2.properties);
                return new WLine(main, e, unfiltered.Skip(2).Prepend(prepend));
            } else if (second is WTab) {
                return new WLine(main, e, unfiltered.Skip(2));
            } else {
                throw new Exception();
            }
        }
        if (unfiltered.Skip(1).FirstOrDefault() is WText t2bis) {  // 00356_ukut_iac_2019_ms_belgium.doc
            string combined = t1.Text + t2bis.Text;
            match = Regex.Match(combined, format);
            if (match.Success) {
                string rest = combined.Substring(match.Length).TrimStart();
                if (string.IsNullOrEmpty(rest)) {
                    return new WLine(main, e, unfiltered.Skip(2));
                } else {
                    WText prepend = new WText(rest, t2bis.properties);
                    return new WLine(main, e, unfiltered.Skip(2).Prepend(prepend));
                }
            }
            match = Regex.Match(combined + " ", format + @"$");
            if (match.Success) {
                IInline third = unfiltered.Skip(2).FirstOrDefault();
                if (third is WTab)  // [2022] UKSC 21
                    return new WLine(main, e, unfiltered.Skip(3));
            }
            if (unfiltered.Skip(2).FirstOrDefault() is WText t3) {  // [2022] EWHC 2360 (KB)
                string combined3 = t1.Text + t2bis.Text + t3.Text;
                match = Regex.Match(combined3, format);
                string rest = combined3.Substring(match.Length).TrimStart();
                if (string.IsNullOrEmpty(rest)) {
                    return new WLine(main, e, unfiltered.Skip(3));
                } else {
                    WText prepend = new WText(rest, t3.properties);
                    return new WLine(main, e, unfiltered.Skip(3).Prepend(prepend));
                }
            }
        }
        return NextFunction(main, format, t1, unfiltered.Skip(1), e);
    }

    private static WLine NextFunction(MainDocumentPart main, string format, WText t1, IEnumerable<IInline> rest, Paragraph p) {
        string t1Text = t1.Text.TrimStart();
        if (rest.FirstOrDefault() is WTab) {    // ewhc/ch/2022/2462
            t1Text += " ";
            rest = rest.Skip(1);
        }
        if (rest.FirstOrDefault() is WText t2) {
            string combined = t1Text + t2.Text;
            Match match = Regex.Match(combined, format);
            if (match.Success) {
                string leftOver = combined.Substring(match.Length).TrimStart();
                if (string.IsNullOrEmpty(leftOver)) {
                    return new WLine(main, p, rest.Skip(1));
                } else {
                    WText prepend = new WText(leftOver, t2.properties);
                    return new WLine(main, p, rest.Skip(1).Prepend(prepend));
                }
            }
        }
        throw new Exception();
    }

    /* */

    private static WText GetPlainNumberFromParagraph(Paragraph e) {
        IEnumerable<OpenXmlElement> texts = e.Descendants().Where(e => e is Text || e is TabChar);
        if (texts.FirstOrDefault() is not Text text)
            return null;
        if (texts.Skip(1).FirstOrDefault() is not TabChar)
            return null;
        Match match = Regex.Match(text.InnerText, PlainNumberFormat);
        if (!match.Success)
            return null;
        string number = match.Value;
        RunProperties rPr = e.Descendants<Run>().FirstOrDefault()?.RunProperties;
        return new WText(number, rPr);
    }

    protected static WLine RemovePlainNumberFromParagraph(MainDocumentPart main, Paragraph p) {
        return RemoveNumberFromParagraph(main, p, PlainNumberFormat);
    }

}

}
