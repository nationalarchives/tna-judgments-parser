
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using AttachmentPair = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

abstract class OptimizedParser {

    private static ILogger logger = Logging.Factory.CreateLogger<OptimizedParser>();

    protected readonly WordprocessingDocument doc;
    protected readonly MainDocumentPart Main;
    protected readonly IOutsideMetadata meta;
    private readonly IEnumerable<AttachmentPair> attachments;
    protected readonly WordDocument PreParsed;
    protected int i = 0;

    protected OptimizedParser(WordprocessingDocument doc, WordDocument preParsed, IOutsideMetadata meta, IEnumerable<AttachmentPair> attachments) {
        this.doc = doc;
        this.Main = doc.MainDocumentPart;
        this.PreParsed = preParsed;
        this.meta = meta;
        this.attachments = attachments ?? Enumerable.Empty<AttachmentPair>();
    }

    protected Judgment ProtectedParse(JudgmentType type) {
        logger.LogDebug($"invoking parser { this.GetType().FullName }");
        int save = i;
        IEnumerable<IBlock> header = Header();
        if (header is null) {
            header = new List<IBlock>();
            i = save;
        }
        List<IDecision> body = Body();
        save = i;
        IEnumerable<IBlock> conclusions = Conclusions();
        if (conclusions is null)
            i = save;
        IEnumerable<IAnnex> annexes = Annexes();
        if (i != PreParsed.Body.Count && !conclusions.Any() && annexes.Any()) {
            var converted = annexes.Select(a => new GroupOfUnnumberedParagraphs(a.Number, a.Contents)).ToList();
            Decision decision = new() { Contents = converted };
            body.Add(decision);
            annexes = [];
            List<IDecision> rest = Decisions();
            body.AddRange(rest);
        }
        if (i != PreParsed.Body.Count) {
            logger.LogDebug("parsing did not complete: " + i);
            throw new Exception();
        }
        IEnumerable<IBlock> coverPage = EnrichCoverPage(PreParsed.Header);
        if (header is not null)
            header = EnrichHeader(header);
        body = EnrichBody(body).ToList();
        if (conclusions is not null)
            conclusions = EnrichConclusions(conclusions);
        if (annexes is not null)
            annexes = EnrichAnnexes(annexes);

        IEnumerable<IInternalAttachment> attachments = Util.NumberAttachments<WordprocessingDocument>(this.attachments).Select(tup => AttachmentParser.Parse(tup.Item1.Item1, tup.Item1.Item2, tup.Item2));
        return new Judgment(doc, meta) {
            Type = type,
            CoverPage = coverPage,
            Header = header,
            Body = body,
            Conclusions = conclusions,
            Annexes = annexes,
            InternalAttachments = attachments
        };
    }

    protected abstract List<IBlock> Header();

    protected virtual IEnumerable<IBlock> EnrichCoverPage(IEnumerable<IBlock> coverPage) {
        return coverPage;
    }
    protected virtual IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        return header;
    }
    protected virtual IEnumerable<IDecision> EnrichBody(IEnumerable<IDecision> body) {
        if (body is null)
            return body;
        return body.Select(dec => new Decision { Author = dec.Author, Contents = ConsolidateContiguousDummyDivisions(dec.Contents) });
    }
    protected virtual IEnumerable<IBlock> EnrichConclusions(IEnumerable<IBlock> conclusions) {
        return conclusions;
    }
    protected virtual IEnumerable<IAnnex> EnrichAnnexes(IEnumerable<IAnnex> annexes) {
        return annexes;
    }

    private static readonly string[] titledJudgeNamePatterns = {
        @"^MRS?\.? JUSTICE( [A-Z]+)+$",
        @"^(Lord|Lady|Mrs?|The Honourable Mrs?) Justice ([A-Z][a-z]* )?[A-Z][a-z]+(-[A-Z][a-z]+)?( VP)?$",
        @"^Mrs? ([A-Z]\.){1,3} [A-Z][a-z]+$",
        @"^[A-Z][a-z]+ [A-Z][a-z]+ QC, Deputy High Court Judge$"
    };
    private IEnumerable<Regex> titledJudgeNameRegexes = titledJudgeNamePatterns
        .Select(p => new Regex(p));

    protected virtual bool IsTitledJudgeName(IBlock block) {
        if (block is not WLine line)
            return false;
        return IsTitledJudgeName(line.NormalizedContent);
    }

    protected virtual bool IsTitledJudgeName(string text) {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.EndsWith(":"))
            text = text.Substring(0 , text.Length - 1).Trim();
        foreach (Regex re in titledJudgeNameRegexes)
            if (re.IsMatch(text))
                return true;
        return false;
    }

    private static readonly string[] titledJudgeNamePatterns2 = {
        @"^MRS?\.? JUSTICE( [A-Z]+)+:",
        @"^(LORD|LADY|MRS?) JUSTICE( [A-Z]+)+:",
        @"^(Lord|Lady|Mrs?|The Honourable Mrs?) Justice ([A-Z][a-z]* )?[A-Z][a-z]+(-[A-Z][a-z]+)?( VP)?:",
        @"^Mrs? ([A-Z]\.){1,3} [A-Z][a-z]+:",
        @"^[A-Z][a-z]+ [A-Z][a-z]+ QC, Deputy High Court Judge:",
        "^LORD [A-Z]+ \\(Concurring"
    };
    private IEnumerable<Regex> titledJudgeNameRegexes2 = titledJudgeNamePatterns2
        .Select(p => new Regex(p));

    protected bool StartsWithTitledJudgeName(string text) {
        text = Regex.Replace(text, @"\s+", " ").TrimStart();
        foreach (Regex re in titledJudgeNameRegexes2)
            if (re.IsMatch(text))
                return true;
        return false;
    }
    protected bool StartsWithTitledJudgeName(IBlock block) {
        if (block is not WLine line)
            return false;
        return StartsWithTitledJudgeName(line.NormalizedContent);
    }

    protected abstract List<IDecision> Body();

    protected List<IDecision> Decisions() {
        List<IDecision> decisions = new List<IDecision>();
        while (i < PreParsed.Body.Count) {
            logger.LogTrace("parsing element " + i);
            BlockWithBreak block = PreParsed.Body.ElementAt(i);
//            if (IsFirstLineOfAnnex(block.Block)) // this was removed for [2024] UKSC 36
//                break;
            int save = i;
            IDecision decision = Decision();
            if (decision is null) {
                i = save;
                break;
            }
            decisions.Add(decision);
        }
        return decisions;
    }

    protected bool IsFirstLineOfDecision(IBlock block) {
        return IsTitledJudgeName(block);
    }

    protected IDecision Decision() {
        logger.LogTrace("parsing element " + i);
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (!IsTitledJudgeName(block))
            return null;
        WLine author = block as WLine;
        i += 1;
        if (i == PreParsed.Body.Count)
            return null;
        List<IDivision> contents = Divisions();
        if (contents is null || contents.Count == 0)
            return null;
        contents.AddRange(ParagraphsUntilEndOfDecision());
        return new Decision { Author = author, Contents = contents };
    }

    protected List<IDivision> ConsolidateContiguousDummyDivisions(IEnumerable<IDivision> divs) {
        List<IDivision> consolidated = new List<IDivision>(divs.Count());
        WDummyDivision last = null;
        foreach (IDivision next in divs) {
            if (next is WDummyDivision dummy) {
                if (last is null)
                    last = dummy;
                else
                    last = new WDummyDivision(Enumerable.Concat(last.Contents, dummy.Contents));
            } else {
                if (last is not null) {
                    consolidated.Add(last);
                    last = null;
                }
                consolidated.Add(next);
            }
        }
        if (last is not null)
            consolidated.Add(last);
        return consolidated;
    }

    protected List<IDivision> ParagraphsUntilEndOfDecision() {
        StopParsingParagraphs stop = e => {
            if (IsFirstLineOfDecision(e))
                return true;
            if (IsFirstLineOfConclusions(e))
                return true;
            if (IsFirstLineOfAnnex(e))
                return true;
            return false;
        };
        return ParagraphsUntil(stop);
    }

    protected List<IDivision> Divisions() {
        int save = i;
        List<IDivision> bigLevels = BigLevels();
        if (bigLevels is not null && bigLevels.Count > 1)
            return bigLevels;
        logger.LogDebug("rolling back big-levels at index " + i + ": ");
        i = save;
        List<IDivision> xHeads = CrossHeadings();
        if (xHeads is not null && xHeads.Count > 1)
            return xHeads;
        logger.LogDebug("rolling back cross-headings at index " + i + ": ");
        i = save;
        return ParagraphsUntilEndOfDecision();
    }

    /* numbered big levels */

    string[] bigLevelNumberingFormats = {
        @"^[A-Z]\.$",
        @"^\([A-Z]\)$",
        @"^\(\d+\)$",
        @"^\([a-z]\)$",
        @"^\([ivx]+\)$"
    };

    private List<IDivision> BigLevels() {
        return BigLevels(bigLevelNumberingFormats, new string[] {});
    }
    private List<IDivision> BigLevels(string[] formats, string[] ancestorFormats) {
        int save = i;
        foreach (string format in formats) {
            string[] childFormats = formats.Where(f => f != format).ToArray();
            i = save;
            List<IDivision> bigLevels = BigLevels(format, childFormats, ancestorFormats);
            if (bigLevels is not null && bigLevels.Count > 1)
                return bigLevels;
        }
        return null;
    }
    private List<IDivision> BigLevels(string format, string[] childFormats, string[] ancestorFormats) {
        List<IDivision> bigLevels = new List<IDivision>();
        while (i < PreParsed.Body.Count) {
            int save = i;
            BigLevel big = BigLevel(format, childFormats, ancestorFormats);
            if (big is null) {
                i = save;
                break;
            }
            bigLevels.Add(big);
        }
        return bigLevels;
    }

    protected bool IsFirstLineOfBigLevel(IBlock block) {
        if (block is not WOldNumberedParagraph np)
            return false;
        if (!IsFlushLeft(np))
            return false;
        return IsFirstLineOfBigLevel(np, bigLevelNumberingFormats);
    }
    private bool IsFirstLineOfBigLevel(IBlock block, string[] formats) {
        if (block is not WOldNumberedParagraph np)
            return false;
        if (!IsFlushLeft(np))
            return false;
        foreach (string format in formats)
            if (IsFirstLineOfBigLevel(np, format))
                return true;
        return false;
    }
    private bool IsFirstLineOfBigLevel(IBlock block, string format) {
        if (block is not WOldNumberedParagraph np)
            return false;
        return IsFirstLineOfBigLevel(np, format);
    }
    private bool IsFirstLineOfBigLevel(WOldNumberedParagraph np, string format) {
        if (!IsFlushLeft(np))
            return false;
        return Regex.IsMatch(np.Number.Text, format);
    }

    private BigLevel BigLevel(string format, string[] childFormats, string[] ancestorFormats) {
        logger.LogTrace("parsing element " + i);
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (block is not WOldNumberedParagraph np)
            return null;
        if (!IsFirstLineOfBigLevel(np, format))
            return null;

        IFormattedText number = np.Number;
        WLine heading = WLine.RemoveNumber(np);
        i += 1;
        int save = i;
        List<IDivision> children;
        children = BigLevels(childFormats, ancestorFormats.AsEnumerable().Append(format).ToArray());
        if (children is null || children.Count < 2) {
            i = save;
            children = ParagraphsUntilBigLevelOrAnnex(format, ancestorFormats);
        }
        if (children.Count == 0)
            return null;
        return new BigLevel { Number = number, Heading = heading, Children = children };
    }


    /* cross headings */

    protected List<IDivision> CrossHeadings() {
        List<IDivision> crossHeadings = new List<IDivision>();
        List<IDivision> intro = ParagraphsUntilCrossHeadingOrAnnex();
        if (intro.Count > 0) {
            IDivision wrapper = new GroupOfParagraphs() { Children = intro };
            crossHeadings.Add(wrapper);
        }
        while (i < PreParsed.Body.Count) {
            int save = i;
            CrossHeading xHead = CrossHeading();
            if (xHead is null) {
                i = save;
                break;
            }
            crossHeadings.Add(xHead);
        }
        return crossHeadings;
    }

    protected virtual bool IsFirstLineOfCrossHeading(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line is WOldNumberedParagraph)
            return false;
        if (line.Contents.OfType<WImageRef>().Any())
            return false;
        if (IsFirstLineOfBigLevel(block))
            return false;
        return IsFlushLeft(line);
    }

    private CrossHeading CrossHeading() {
        logger.LogTrace("parsing element " + i);
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (!IsFirstLineOfCrossHeading(block))
            return null;
        WLine heading = (WLine) block;
        i += 1;
        if (i == PreParsed.Body.Count)
            return null;

        List<IDivision> children = ParagraphsUntilCrossHeadingOrAnnex();
        if (children.Count == 0) {
            logger.LogDebug("Abandoning CrossHeading");
            return null;
        }
        return new CrossHeading { Heading = heading, Children = children };
    }

    /* paragraphs */

    public delegate bool StopParsingParagraphs(IBlock block);

    protected List<IDivision> ParagraphsUntil(StopParsingParagraphs predicate) {
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < PreParsed.Body.Count) {
            IBlock block = PreParsed.Body.ElementAt(i).Block;
            if (predicate(block))
                break;
            IDivision para = ParseParagraph();
            paragraphs.Add(para);
        }
        return paragraphs;
    }

    protected virtual bool IsFirstLineOfConclusions(IBlock block) => false;

    protected List<IDivision> ParagraphsUntilEndOfBody() {
        StopParsingParagraphs predicate = b => {
            if (IsFirstLineOfConclusions(b))
                return true;
            if (IsFirstLineOfAnnex(b))
                return true;
            return false;
        };
        return ParagraphsUntil(predicate);
    }

    protected virtual List<IDivision> ParagraphsUntilAnnex() {
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < PreParsed.Body.Count) {
            IBlock block = PreParsed.Body.ElementAt(i).Block;
            if (IsFirstLineOfAnnex(block))
                break;
            if (IsTitledJudgeName(block))
                break;
            IDivision para = ParseParagraph();
            if (para is null)
                continue;
            paragraphs.Add(para);
        }
        return paragraphs;
    }

    protected List<IBlock> Conclusions() {
        List<IBlock> blocks = new List<IBlock>();
        while (i < PreParsed.Body.Count) {
            IBlock block = PreParsed.Body.ElementAt(i).Block;
            if (IsFirstLineOfAnnex(block))
                break;
            blocks.Add(block);
            i += 1;
        }
        return blocks;
    }

    private List<IDivision> ParagraphsUntilCrossHeadingOrAnnex() {
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < PreParsed.Body.Count) {
            IBlock block = PreParsed.Body.ElementAt(i).Block;
            if (IsFirstLineOfCrossHeading(block))
                break;
            if (IsFirstLineOfAnnex(block))
                break;
            if (IsTitledJudgeName(block))
                break;
            IDivision para = ParseParagraph();
            if (para is null)
                continue;
            paragraphs.Add(para);
        }
        return paragraphs;
    }

    private List<IDivision> ParagraphsUntilBigLevelOrAnnex(string format, string[] ancestorFormats) {
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < PreParsed.Body.Count) {
            IBlock block = PreParsed.Body.ElementAt(i).Block;
            if (IsFirstLineOfBigLevel(block, format))
                break;
            if (IsFirstLineOfBigLevel(block, ancestorFormats))
                break;
            if (IsFirstLineOfAnnex(block))
                break;
            if (IsTitledJudgeName(block))
                break;
            IDivision para = ParseParagraph();
            if (para is null)
                continue;
            paragraphs.Add(para);
        }
        return paragraphs;
    }

    protected virtual IDivision ParseParagraph() {
        logger.LogTrace("parsing element " + i);
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (block is WLine line) {
            return ParseParagraphAndSubparagraphs(line);
        }
        if (block is WTable table) {
            i += 1;
            return new WDummyDivision(table);
        }
        if (block is TableOfContents toc) {
            i += 1;
            return new WTableOfContents(toc.Contents);
        }
        throw new System.Exception(block.GetType().ToString());
    }

    internal static bool IsFlushLeft(WLine line) {
        if (line.Alignment == AlignmentValues.Right)
            return false;
        if (line.Alignment == AlignmentValues.Center)
            return false;
        return GetEffectiveIndent(line) <= 0f;
    }

    private static float GetEffectiveFirstLineIndent(WLine line) {
        float left = line.LeftIndentInches ?? 0f;
        float first = left + (line.FirstLineIndentInches ?? 0f);
        foreach (var inline in line.Contents) {
            if (inline is not WTab)
                break;
            float tab = line.GetFirstTabAfter(first) ?? WLine.GetFirstDefaultTabAfter(first);
            if (first < left && tab > left)
                first = left;
            else
                first = tab;
        }
        return first;
    }

    /// this is a rough estimate
    // private static bool IsOnlyOneLine(WLine line, float start) {
    //     var rightIndent = line.RightIndentInches ?? 0f;
    //     var width = 8.27 - start - rightIndent;
    //     return line.NormalizedContent.Length < width * 14;
    // }

    internal static float GetEffectiveIndent(WLine line) {
        if (line is WOldNumberedParagraph old)
            return old.GetEffectiveFirstLineIndent();
        return GetEffectiveFirstLineIndent(line);
        // float left = line.LeftIndentInches ?? 0f;
        // float first = GetEffectiveFirstLineIndent(line);
        // bool isOnlyOneLine = IsOnlyOneLine(line, first);
        // return isOnlyOneLine ? first : left;
    }

    internal static bool IsHanging(WLine line) {
        float first = GetEffectiveIndent(line);
        float rest = line.LeftIndentInches ?? 0f;
        return first < rest - MarginOfError;
    }

    protected virtual bool HasProperParagraphNumber(IDivision div) {
        return div.Number is not null && Regex.IsMatch(div.Number.Text.Trim(), @"^\d+\.?$");
    }

    private static IDivision DemoteToSubparagraph(IDivision p) {
        if (p is BranchSubparagraph || p is LeafSubparagraph)
            return p;
        if (p is IBranch branch)
            return new BranchSubparagraph { Number = branch.Number, Intro = branch.Intro, Children = branch.Children };
        if (p is ILeaf leaf)
            return new LeafSubparagraph { Number = leaf.Number, Contents = leaf.Contents };
        return p;
    }
    private static IDivision PromoteFromSubparagraph(IDivision sp) {
        if (sp is BranchSubparagraph branch)
            return new BranchParagraph { Number = branch.Number, Intro = branch.Intro, Children = branch.Children };
        if (sp is LeafSubparagraph leaf)
            return new WNewNumberedParagraph(leaf.Number, leaf.Contents);
        return sp;
    }

    internal static float MarginOfError = 0.099f;  // test19 wants 0.11

    /// <summary>
    /// Allows subclass to specify that a line is outside of whatever paragraph came before it.
    /// </summary>
    /// <param name="line">The line to test</param>
    /// <returns>Whether the line must be outside of any open paragraph</returns>
    virtual protected bool CannotBeSubparagraph(WLine line) {
        return false;
    }

    /// <summary>
    /// Allows subclass to specify that a line is part of the <wrapUp> of the current <paragraph>.
    /// </summary>
    /// <param name="line">The line to test</param>
    /// <returns>Whether the line should be within the wrapUp</returns>
    virtual protected bool IsWrapUp(WLine line) {
        return false;
    }

    protected IDivision ParseParagraphAndSubparagraphs(WLine line, bool sub = false, bool quote = false) {

        ILeaf div = ParseSimpleParagraph(line, sub);
        if (i == PreParsed.Body.Count)
            return div;
        if (div.Heading is not null)  // currently impossible
            return div;
        if (div.Contents.Count() != 1)  // currently impossible
            return div;
        if (div.Contents.First() is not WLine intro1)  // currently impossible
            return div;
        
        const float marginOfError = 0.099f;
        float indent1 = GetEffectiveIndent(line);
        bool hanging1 = IsHanging(line);

        List<IBlock> intro = [.. div.Contents];
        List<IDivision> subparagraphs = [];

        while (i < PreParsed.Body.Count) {
            IBlock next = PreParsed.Body.ElementAt(i).Block;
            bool thereIsAtLeastOneSubparagraph = subparagraphs.Count > 0;
            if (next is ITable table) {
                if (thereIsAtLeastOneSubparagraph)
                    break;
                intro.Add(table);
                i += 1;
                continue;
            }
            if (next is ITableOfContents2 toc) {
                if (thereIsAtLeastOneSubparagraph)
                    break;
                intro.Add(toc);
                i += 1;
                continue;
            }
            if (next is WLine nextLine) {

                if (CannotBeSubparagraph(nextLine))
                    break;

                if (!quote && !thereIsAtLeastOneSubparagraph && IsFirstLineOfQuotedStructure(nextLine, indent1)) {
                    QuotedStructure qs = ParseQuotedStructure(nextLine);
                    intro.Add(qs);
                    continue;
                }

                if (IsWrapUp(nextLine) && !sub && !quote && div.Number is not null && thereIsAtLeastOneSubparagraph) {
                    List<WLine> wrapUp = [ nextLine ];
                    i += 1;
                    return new BranchParagraph { Number = div.Number, Intro = intro, Children = subparagraphs, WrapUp = wrapUp };
                }
                if (IsWrapUp(nextLine) && !sub && !quote && div.Number is not null && !thereIsAtLeastOneSubparagraph && intro.LastOrDefault() is QuotedStructure) {
                    intro.Add(nextLine);
                    i += 1;
                    break;  // could return new WNewNumberedParagraph(div.Number, intro);
                }

                float nextIndent = GetEffectiveIndent(nextLine);

                /* quoted structures */
                bool indentIsRight;
                if (hanging1)
                    indentIsRight = nextIndent - MarginOfError > indent1;
                else
                    indentIsRight = nextIndent == indent1; // Supreme Court
                // see test 3 before para 64
                bool charFormattingIsRight = !nextLine.IsAllItalicized() && !nextLine.IsAllBold() && !nextLine.IsAllUnderlined();
                if (!sub && !quote && nextLine is not WOldNumberedParagraph && indentIsRight && charFormattingIsRight && !thereIsAtLeastOneSubparagraph && intro.LastOrDefault() is QuotedStructure) {
                    int save2 = i;
                    IDivision nextparagraph = ParseParagraphAndSubparagraphs(nextLine, false);
                    if (nextparagraph.Number is not null) {
                        i = save2;
                        break;
                    }
                    if (nextparagraph.Heading is not null) {
                        i = save2;
                        break;
                    }
                    if (nextparagraph is not ILeaf leaf) {
                        i = save2;
                        break;
                    }
                    intro.AddRange(leaf.Contents);
                    continue;
                }

                if (nextIndent - MarginOfError <= indent1)
                    break;

                int save = i;
                IDivision subparagraph = ParseParagraphAndSubparagraphs(nextLine, true);

                // it would be better not to need this, b/c big levels would already have been recognized
                if (!sub && !HasProperParagraphNumber(div) && HasProperParagraphNumber(subparagraph)) {
                    i = save;
                    break;
                }
                subparagraph = DemoteToSubparagraph(subparagraph);
                subparagraphs.Add(subparagraph);
                continue;
            }
            throw new System.Exception();  // should be impossible
        }

        // ideally all cross-headings would already have been recognized
        if (!sub && !quote && div.Number is null && intro.Count == 1 && subparagraphs.Any())
            return new CrossHeading { Heading = intro1, Children = subparagraphs.Select(PromoteFromSubparagraph) };
        if (subparagraphs.Any())
            return new BranchParagraph { Number = div.Number, Intro = intro, Children = subparagraphs };
        if (intro.Count == div.Contents.Count())
            return div;
        return new WNewNumberedParagraph(div.Number, intro);
    }

    private ILeaf ParseSimpleParagraph(WLine line, bool sub) {
        i += 1;
        if (line is WOldNumberedParagraph np)
            return new WNewNumberedParagraph(np.Number, WLine.RemoveNumber(np));
        return new WDummyDivision(line);
    }


    /* quoted structures */

    private static bool IsFirstLineOfQuotedStructure(WLine line, float parentIndent) {
        if (line.Style != "Quote" && !line.NormalizedContent.StartsWith('“'))
            return false;
        float indent = GetEffectiveIndent(line);
        if (indent - MarginOfError <= parentIndent)
            return false;
        return true;
    }

    private QuotedStructure ParseQuotedStructure(WLine line1) {
        float indent1 = GetEffectiveIndent(line1);
        List<IDivision> contents = [];
        IDivision div1 = ParseParagraphAndSubparagraphs(line1, false, true);
        contents.Add(div1);
        while (i < PreParsed.Body.Count) {
            IBlock nextBlock = PreParsed.Body.ElementAt(i).Block;
            if (nextBlock is not WLine nextLine)
                break;
            float nextIndent = GetEffectiveIndent(nextLine);
            if (Math.Abs(nextIndent - indent1) > MarginOfError)
                break;
            IDivision nextDiv = ParseParagraphAndSubparagraphs(nextLine, false, true);
            // wrapUp
            if (nextDiv is WDummyDivision dummy && dummy.Contents.Count() == 1 && dummy.Contents.Last() is WLine line) {
                if (contents.LastOrDefault() is BranchParagraph branch && branch.WrapUp is null) {
                    List<WLine> wrapUp = [ line ];
                    branch.WrapUp = wrapUp;
                    continue;
                }
            }
            contents.Add(nextDiv);
        }
        return new QuotedStructure() { Contents = contents };
    }


    /* annexes */

    protected virtual bool IsFirstLineOfAnnex(IBlock block) {
        if (block is not WLine line)
            return false;
        String text = line.NormalizedContent;
        if (text.Equals("Annex" , StringComparison.InvariantCultureIgnoreCase))
            return true;
        if (text.Equals("- Annex -" , StringComparison.InvariantCultureIgnoreCase))
            return true;
        // if (Regex.IsMatch(text, @"^Annex \d+$", RegexOptions.IgnoreCase))
        //     return true;
        if (text.Equals("Appendix" , StringComparison.InvariantCultureIgnoreCase))
            return true;
        // if (Regex.IsMatch(text, @"^\s*Appendix\s+No\.?\s*\d+\s*$", RegexOptions.IgnoreCase))
        //     return true;
        return false;
    }

    protected List<IAnnex> Annexes() {
        List<IAnnex> annexes = new List<IAnnex>();
        while (i < PreParsed.Body.Count) {
            int save = i;
            Annex annex = Annex();
            if (annex is null) {
                i = save;
                break;
            }
            annexes.Add(annex);
        }
        return annexes;
    }

    private Annex Annex() {
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (!IsFirstLineOfAnnex(block))
            return null;
        WLine number = block as WLine;
        i += 1;
        if (i == PreParsed.Body.Count)
            return null;
        List<IBlock> contents = new List<IBlock>();
        while (i < PreParsed.Body.Count) {
            block = PreParsed.Body.ElementAt(i).Block;
            if (IsFirstLineOfAnnex(block))
                break;
            if (IsTitledJudgeName(block))
                break;
            contents.Add(block);
            i += 1;
        }
        return new Annex { Number = number, Contents = contents };
    }

}

}
