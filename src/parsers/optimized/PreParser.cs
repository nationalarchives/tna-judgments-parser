
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
        Logger.LogTrace($"pre-parsing { doc.DocumentType.ToString() }");
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
        if (e is Paragraph p) {
            if (DOCX.Paragraphs.IsDeleted(p))
                return true;
            if (DOCX.Paragraphs.IsEmptySectionBreak(p))
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
        List<MergedBlockWithBreak> unmerged = FirstPass(main);
        List<BlockWithBreak> merged = Merge(unmerged)
            .Select(RemoveTrailingWhitespaceAndMergeRuns)
            .Where(LineIsNotEmpty)
            .ToList();
        merged = HardNumbers.Extract(merged);
        return merged;
    }

    private List<MergedBlockWithBreak> FirstPass(MainDocumentPart main) {
        List<MergedBlockWithBreak> unmerged = new List<MergedBlockWithBreak>();
        bool lineBreakBefore = false;
        foreach (var e in main.Document.Body.ChildElements) {
            lineBreakBefore = lineBreakBefore || Util.IsSectionOrPageBreak(e);
            if (IsSkippable(e))
                continue;
            List<IBlock> blocks = ParseElement(main, e);
            foreach (IBlock block in blocks.SkipLast(1)) {
                unmerged.Add(new MergedBlockWithBreak { Block = block, LineBreakBefore = lineBreakBefore });
                lineBreakBefore = false;
            }
            foreach (IBlock block in blocks.TakeLast(1)) {
                bool merged1 = e is Paragraph p && DOCX.Paragraphs.IsMergedWithFollowing(p);
                unmerged.Add(new MergedBlockWithBreak { Block = block, LineBreakBefore = lineBreakBefore, MergedWithNext = merged1 });
                lineBreakBefore = false;
            }
        }
        return unmerged;
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


    private BlockWithBreak RemoveTrailingWhitespaceAndMergeRuns(BlockWithBreak bb) {
        var enriched = removeTrailingWhitespace.Enrich1(bb.Block);
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

}

}
