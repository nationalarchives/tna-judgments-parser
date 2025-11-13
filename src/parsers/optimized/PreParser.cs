
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Vml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;
using Microsoft.Extensions.Logging;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class WordDocument {

    public WordprocessingDocument Docx { get; init; }

    public IEnumerable<IBlock> Header { get; init; }

    public List<BlockWithBreak> Body { get; init; }

}

class BlockWithBreak {

    internal IBlock Block { get; init; }

    internal bool LineBreakBefore { get; init; }

}

class MergedBlockWithBreak : BlockWithBreak {

    internal bool MergedWithNext { get; init; }

}

class PreParser {

    private ILogger Logger = Logging.Factory.CreateLogger<PreParser>();

    internal WordDocument Parse(WordprocessingDocument doc) {
        Logger.LogTrace("pre-parsing { }", doc.DocumentType.ToString());
        return new WordDocument {
            Docx = doc,
            Header = Header(doc.MainDocumentPart),
            Body = Body(doc.MainDocumentPart)
        };
    }

    RemoveTrailingWhitespace removeTrailingWhitespace = new RemoveTrailingWhitespace();
    Merger mergeRuns = new Merger();
    readonly Func<IBlock, IBlock> trimLeadingLineBreaks = (block) => {
        if (block is not WLine line)
            return block;
        var trimmed = line.Contents.SkipWhile(i => i is WLineBreak);
        if (trimmed.Count() == line.Contents.Count())
            return line;
        return WLine.Make(line, trimmed);
    };

    private IEnumerable<IBlock> Header(MainDocumentPart main) {
        Header header = DOCX.Headers.GetFirst(main);
        if (header is null)
            return Enumerable.Empty<IBlock>();
        IEnumerable<IBlock> contents = Blocks.ParseBlocks(main, header.ChildElements)
            .Where(block => block is not ILine line || !line.IsEmpty());
        contents = removeTrailingWhitespace.Enrich(contents);
        contents = contents.Select(trimLeadingLineBreaks);
        contents = mergeRuns.Enrich(contents);
        return contents.ToList();  // toList() is needed for the ImageRef.Src replacement
    }

    internal static bool IsSkippable(OpenXmlElement e) {
        if (e is SectionProperties)
            return true;
        if (e is BookmarkStart || e is BookmarkEnd)
            return true;
        if (e is Paragraph p) {
            if (DOCX.Paragraphs.IsDeleted(p))
                return true;
            if (DOCX.Paragraphs.IsEmptySectionBreak(p))
                return true;
            if (DOCX.Paragraphs.IsEmptyPageBreak(p))
                return true;
            if (DOCX.Numbering2.HasOwnNumber(p))
                return false;
            if (DOCX.Numbering2.HasEffectiveStyleNumber(p))
                return false;
            if (e.Descendants().OfType<Drawing>().Any())
                return false;
            if (e.Descendants().OfType<Picture>().Any())
                return false;
            if (e.Descendants().OfType<Shape>().Any())
                return false;
            if (string.IsNullOrWhiteSpace(p.InnerText))
                return true;
        }
        if (e is PermEnd)
            return true;
        return false;
    }

    private List<BlockWithBreak> Body(MainDocumentPart main) {
        List<MergedBlockWithBreak> unmerged = FirstPass1.X(main);
        List<BlockWithBreak> merged = Merge(unmerged)
            .Select(TrimWhitespaceAndMergeRuns)
            .Where(LineIsNotEmpty)
            .ToList();
        merged = HardNumbers.Extract(merged);
        return merged;
    }

    private static List<IBlock> ParseElement(MainDocumentPart main, OpenXmlElement e) {
        if (e is Paragraph p)
            return new List<IBlock>(1) { ParseParagraph(main, p) };
        if (e is Table table)
            return new List<IBlock>(1) { new WTable(main, table) };
        if (e is SdtBlock sdt)
            return Blocks.ParseStdBlock(main, sdt).ToList();
        throw new System.Exception(e.GetType().ToString());
    }

    internal static WLine ParseParagraph(MainDocumentPart main, Paragraph p) {
        DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, p);
        if (info.HasValue)
            return new WOldNumberedParagraph(info.Value, main, p);
        WLine line = new WLine(main, p);
        INumber num2 = Fields.RemoveListNum(line);  // mutates line if successful!
        if (num2 is not null)
            return new WOldNumberedParagraph(num2, line);
        return line;
    }

    private List<BlockWithBreak> Merge(List<MergedBlockWithBreak> unmerged) {
        List<BlockWithBreak> merged = new List<BlockWithBreak>();
        bool prevMerged = false;
        foreach (var mbb in unmerged) {
            if (prevMerged) {
                var prev = merged.Last();
                if (prev.Block is WLine prevLine && mbb.Block is WLine thisLine) {
                    WLine line2 = WLine.Make(thisLine, Enumerable.Concat(prevLine.Contents, thisLine.Contents));
                    var prev2 = new BlockWithBreak { LineBreakBefore = prev.LineBreakBefore, Block = line2 };
                    merged.RemoveAt(merged.Count - 1);
                    merged.Add(prev2);
                } else {
                    Logger.LogWarning("can't merge");
                    merged.Add(mbb);
                }
            } else {
                merged.Add(mbb);
            }
            prevMerged = mbb.MergedWithNext;
        }
        return merged;
    }


    private BlockWithBreak TrimWhitespaceAndMergeRuns(BlockWithBreak bb) {
        var enriched = removeTrailingWhitespace.Enrich1(bb.Block);
        enriched = trimLeadingLineBreaks(enriched);
        enriched = mergeRuns.Enrich1(enriched);
        if (object.ReferenceEquals(enriched, bb))
            return bb;
        return new BlockWithBreak { Block = enriched, LineBreakBefore = bb.LineBreakBefore };
    }

    private bool LineIsNotEmpty(BlockWithBreak bb) {
        if (bb.Block is not ILine line)
            return true;
        if (line is IOldNumberedParagraph)
            return true;
        return line.Contents.Any();
    }

    class FirstPass1 {

        internal static List<MergedBlockWithBreak> X(MainDocumentPart main) => new FirstPass1(main).DoPass();

        private MainDocumentPart Main { get; init; }

        private int i = 0;

        private FirstPass1(MainDocumentPart main) { Main = main; }

        private readonly ILogger Logger = Logging.Factory.CreateLogger<FirstPass1>();

        private List<MergedBlockWithBreak> DoPass() {
            List<MergedBlockWithBreak> unmerged = new List<MergedBlockWithBreak>();
            bool lineBreakBefore = false;
            List<OpenXmlElement> skpdBkmrks = new();
            while (i < Main.Document.Body.ChildElements.Count) {
                OpenXmlElement e = Main.Document.Body.ChildElements.ElementAt(i);
                lineBreakBefore = lineBreakBefore || Util.IsSectionOrPageBreak(e);
                if (IsSkippable(e)) {
                    if (Bookmarks.IsBookmark(e))
                        skpdBkmrks.Add(e);
                    else
                        skpdBkmrks.AddRange(e.Descendants().Where(Bookmarks.IsBookmark));
                    i += 1;
                    continue;
                }

                int save = i;
                TableOfContents toc = ParseTableOfContents(e);
                if (toc is not null) {
                    unmerged.Add(new MergedBlockWithBreak { Block = toc, LineBreakBefore = lineBreakBefore });
                    lineBreakBefore = false;
                    AddSkippedBookmarksToFirstLine(skpdBkmrks, new List<IBlock>(1) { toc });
                    skpdBkmrks = new();
                    continue;
                } else {
                    i = save;
                }

                List<IBlock> blocks = ParseElement(Main, e);
                AddSkippedBookmarksToFirstLine(skpdBkmrks, blocks);
                skpdBkmrks = new();
                foreach (IBlock block in blocks.SkipLast(1)) {
                    unmerged.Add(new MergedBlockWithBreak { Block = block, LineBreakBefore = lineBreakBefore });
                    lineBreakBefore = false;
                }
                foreach (IBlock block in blocks.TakeLast(1)) {
                    bool mergedWithNext = e is Paragraph p && DOCX.Paragraphs.IsMergedWithFollowing(p);
                    unmerged.Add(new MergedBlockWithBreak { Block = block, LineBreakBefore = lineBreakBefore, MergedWithNext = mergedWithNext });
                    lineBreakBefore = false;
                }
                i += 1;
            }
            return unmerged;
        }

        private void AddSkippedBookmarksToFirstLine(List<OpenXmlElement> skpdBkmrks, List<IBlock> blocks) {
            if (!skpdBkmrks.Any())
                return;
            List<WBookmark> parsed = Bookmarks.Parse(skpdBkmrks);
            ILine iLine = blocks.SelectMany(Util.GetLines).FirstOrDefault();
            if (iLine is not WLine wLine) {
                foreach (var bkmk in parsed)
                    Logger.LogWarning("cannot move bookmark from skipped line: {}", bkmk.Name);
                return;
            }
            wLine.PrependBookmarksFromPrecedingSkippedLines(parsed);
        }

        private TableOfContents ParseTableOfContents(OpenXmlElement e1) {
            if (e1 is not Paragraph p1)
                return null;
            if (!IsTOCStart(p1))
                return null;
            List<WLine> contents = new List<WLine>();

            WLine line1 = ParseParagraph(Main, p1);
            contents.Add(line1);
            i += 1;

            while (i < Main.Document.Body.ChildElements.Count) {
                OpenXmlElement e = Main.Document.Body.ChildElements.ElementAt(i);
                if (e is not Paragraph p)
                    return null;
                i += 1;
                if (HasAnExtraFieldEnd(p)) // needs to be before IsSkippable
                    return new TableOfContents { Contents = contents };
                if (IsSkippable(e))
                    continue;
                WLine line = ParseParagraph(Main, p);
                contents.Add(line);
            }
            return null;
        }

        private static bool IsTOCStart(Paragraph p) {
            // if (e is not Paragraph p)
            //     return false;
            var runs = p.ChildElements.Where(child => child is not ParagraphProperties);
            if (runs.FirstOrDefault() is not Run run1)
                return false;
            if (!DOCX.Fields.IsFieldStart(run1))
                return false;
            if (runs.Skip(1).FirstOrDefault() is not Run run2)
                return false;
            if (!Fields.IsFieldCode(run2))
                return false;
            string code = Fields.GetFieldCode(run2);
            return DOCX.Fields.NormalizeFieldCode(code).StartsWith(" TOC ");
        }

        private static IEnumerable<Run> GetFieldStarts(OpenXmlElement e) {
            if (e is Run r && DOCX.Fields.IsFieldStart(r))
                return new List<Run>(1) { r };
            if (e is Hyperlink)
                return e.ChildElements.SelectMany(GetFieldStarts);
            return Enumerable.Empty<Run>();
        }
        private static IEnumerable<Run> GetFieldEnds(OpenXmlElement e) {
            if (e is Run r && DOCX.Fields.IsFieldEnd(r))
                return new List<Run>(1) { r };
            if (e is Hyperlink)
                return e.ChildElements.SelectMany(GetFieldEnds);
            return Enumerable.Empty<Run>();
        }

        private static bool HasAnExtraFieldEnd(Paragraph p) {
            var ends = p.ChildElements.SelectMany(GetFieldEnds);
            if (!ends.Any())
                return false;
            var starts = p.ChildElements.SelectMany(GetFieldStarts);
            return ends.Count() > starts.Count();
        }

    }

    class SecondPass {

        private List<BlockWithBreak> Body(List<MergedBlockWithBreak> unmerged) {
            return null;
        }

    }

}

}
