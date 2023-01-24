
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

    private readonly WordprocessingDocument doc;
    protected readonly MainDocumentPart Main;
    private readonly IOutsideMetadata meta;
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
        IEnumerable<IDecision> body = Body();
        save = i;
        IEnumerable<IBlock> conclusions = Conclusions();
        if (conclusions is null)
            i = save;
        IEnumerable<IAnnex> annexes = Annexes();
        if (i != PreParsed.Body.Count) {
            logger.LogDebug("parsing did not complete: " + i);
            throw new Exception();
        }
        if (header is not null)
            header = EnrichHeader(header);
        body = EnrichBody(body);
        if (conclusions is not null)
            conclusions = EnrichConclusions(conclusions);
        if (annexes is not null)
            annexes = EnrichAnnexes(annexes);

        IEnumerable<IInternalAttachment> attachments = Util.NumberAttachments<WordprocessingDocument>(this.attachments).Select(tup => FlatParagraphsParser.Parse(tup.Item1.Item1, tup.Item1.Item2, tup.Item2));
        return new Judgment(doc, meta) {
            Type = type,
            CoverPage = PreParsed.Header,
            Header = header,
            Body = body,
            Conclusions = conclusions,
            Annexes = annexes,
            InternalAttachments = attachments
        };
    }

    protected abstract List<IBlock> Header();

    protected virtual IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        return header;
    }
    protected virtual IEnumerable<IDecision> EnrichBody(IEnumerable<IDecision> body) {
        return body;
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
        @"^[A-Z][a-z]+ [A-Z][a-z]+ QC, Deputy High Court Judge:"
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
            if (IsFirstLineOfAnnex(block.Block))
                break;
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
        @"^([A-Z]\.)$",
        @"^(\(\d+\))$",
        @"^(\d+\.)$",
        @"^(\([a-z]\))$",
        @"^(\([ivx]+\))$"
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

    // protected WText GetNumberFromParagraph(Paragraph e, string format) {
    //     string text = NormalizeParagraph(e);
    //     Match match = Regex.Match(text, format);
    //     if (!match.Success)
    //         return null;
    //     string number = match.Groups[1].Value;
    //     RunProperties rPr = e.Descendants<RunProperties>().FirstOrDefault();
    //     return new WText(number, rPr);
    // }

    // protected WLine RemoveNumberFromParagraph(Paragraph e, string format) {
    //     IEnumerable<IInline> unfiltered = Inline.ParseRuns(Main, e.ChildElements);
    //     unfiltered = Merger.Merge(unfiltered);
    //     unfiltered = unfiltered.SkipWhile(inline => inline is WLineBreak || inline is WTab || (inline is WText wText && string.IsNullOrWhiteSpace(wText.Text)));
    //     IInline first = unfiltered.First();
    //     if (first is not WText t1)
    //         throw new Exception();
    //     Match match = Regex.Match(t1.Text.TrimStart(), format); // TrimStart for 00356_ukut_iac_2019_ms_belgium.doc
    //     if (match.Success) {
    //         string rest = t1.Text.TrimStart().Substring(match.Length).TrimStart();
    //         if (string.IsNullOrEmpty(rest)) {
    //             return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(1));
    //         } else {
    //             WText prepend = new WText(rest, t1.properties);
    //             return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(1).Prepend(prepend));
    //         }
    //     }
    //     match = Regex.Match(t1.Text + " ", format + @"$");
    //     if (match.Success) {
    //         IInline second = unfiltered.Skip(1).FirstOrDefault();
    //         if (second is WText t2) {
    //             WText prepend = new WText(t2.Text.TrimStart(), t2.properties);
    //             return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(2).Prepend(prepend));
    //         } else if (second is WTab) {
    //             return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(2));
    //         } else {
    //             throw new Exception();
    //         }
    //     }
    //     if (unfiltered.Skip(1).FirstOrDefault() is WText t2bis) {  // 00356_ukut_iac_2019_ms_belgium.doc
    //         string combined = t1.Text + t2bis.Text;
    //         match = Regex.Match(combined, format);
    //         if (match.Success) {
    //             string rest = combined.Substring(match.Length).TrimStart();
    //             if (string.IsNullOrEmpty(rest)) {
    //                 return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(2));
    //             } else {
    //                 WText prepend = new WText(rest, t2bis.properties);
    //                 return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(2).Prepend(prepend));
    //             }
    //         }
    //         match = Regex.Match(combined + " ", format + @"$");
    //         if (match.Success) {
    //             IInline third = unfiltered.Skip(2).FirstOrDefault();
    //             if (third is WTab)  // [2022] UKSC 21
    //                 return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(3));
    //         }
    //         if (unfiltered.Skip(2).FirstOrDefault() is WText t3) {  // [2022] EWHC 2360 (KB)
    //             string combined3 = t1.Text + t2bis.Text + t3.Text;
    //             match = Regex.Match(combined3, format);
    //             string rest = combined3.Substring(match.Length).TrimStart();
    //             if (string.IsNullOrEmpty(rest)) {
    //                 return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(3));
    //             } else {
    //                 WText prepend = new WText(rest, t3.properties);
    //                 return new WLine(Main, e.ParagraphProperties, unfiltered.Skip(3).Prepend(prepend));
    //             }
    //         }
    //     }
    //     return RemoveNumberFromFirstLineOfBigLevel4(format, t1, unfiltered.Skip(1), e.ParagraphProperties);
    // }

    // private WLine RemoveNumberFromFirstLineOfBigLevel4(string format, WText t1, IEnumerable<IInline> rest, ParagraphProperties pProps) {
    //     string t1Text = t1.Text.TrimStart();
    //     if (rest.FirstOrDefault() is WTab) {    // ewhc/ch/2022/2462
    //         t1Text += " ";
    //         rest = rest.Skip(1);
    //     }
    //     if (rest.FirstOrDefault() is WText t2) {
    //         string combined = t1Text + t2.Text;
    //         Match match = Regex.Match(combined, format);
    //         if (match.Success) {
    //             string leftOver = combined.Substring(match.Length).TrimStart();
    //             if (string.IsNullOrEmpty(leftOver)) {
    //                 return new WLine(Main, pProps, rest.Skip(1));
    //             } else {
    //                 WText prepend = new WText(leftOver, t2.properties);
    //                 return new WLine(Main, pProps, rest.Skip(1).Prepend(prepend));
    //             }
    //         }
    //     }
    //     throw new Exception();
    // }

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
        WLine heading = new WLine(np);
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
        // if (block is ITableOfContents toc) {
        //     i += 1;
        //     return new WDummyDivision();
        // }
        // table of contents???
        throw new System.Exception(block.GetType().ToString());
    }

    private static bool IsFlushLeft(WLine line) {
        if (line.Alignment == AlignmentValues.Right)
            return false;
        if (line.Alignment == AlignmentValues.Center)
            return false;
        return GetEffectiveIndent(line) <= 0f;
    }

    private static float GetEffectiveIndent(WLine line) {
        float leftMargin = line.LeftIndentWithNumber ?? 0f;
        float firstLine = line.FirstLineIndentWithNumber ?? 0f;
        float indent = firstLine > 0 ? leftMargin : leftMargin + firstLine;
        // if (new WLine(main, p).Contents.FirstOrDefault() is WTab) {
        //     float? nextTab = DOCX.Paragraphs.GetFirstTab(main, p.ParagraphProperties);
        //     if (nextTab.HasValue)
        //         indent += nextTab.Value;
        // }
        return indent;
    }

    private IDivision ParseParagraphAndSubparagraphs(WLine line, bool sub = false) {
        ILeaf div = ParseSimpleParagraph(line, sub);
        if (i == PreParsed.Body.Count)
            return div;
        if (div.Heading is not null)
            return div;
        if (div.Contents.Count() != 1)
            return div;
        if (div.Contents.First() is not WLine)
            return div;
        
        const float marginOfError = 0.05f;
        float indent1 = GetEffectiveIndent(line);

        List<IBlock> intro = new List<IBlock>();
        intro.AddRange(div.Contents);
        List<IDivision> subparagraphs = new List<IDivision>();

        while (i < PreParsed.Body.Count) {
            IBlock next = PreParsed.Body.ElementAt(i).Block;
            if (next is ITable table) {
                if (subparagraphs.Any())
                    break;
                intro.Add(table);
                i += 1;
                continue;
            }
            if (next is WLine nextLine) {
                float nextIndent1 = GetEffectiveIndent(nextLine);
                if (nextIndent1 - marginOfError <= indent1)
                    break;

                int save = i;
                IDivision subparagraph = ParseParagraphAndSubparagraphs(nextLine, true);
                if (!sub && div.Number is null && subparagraph.Number is not null && Regex.IsMatch(subparagraph.Number.Text, @"^\d+\.$")) {
                    i = save;
                    break;
                }
                if (subparagraph is BranchSubparagraph || subparagraph is LeafSubparagraph) {
                } else if (subparagraph is IBranch subbranch) {
                    subparagraph = new BranchSubparagraph { Number = subparagraph.Number, Intro = subbranch.Intro, Children = subbranch.Children };
                } else {
                    subparagraph = new LeafSubparagraph { Number = subparagraph.Number, Contents = (subparagraph as ILeaf).Contents };
                }
                subparagraphs.Add(subparagraph);
                continue;
            }

            throw new System.Exception(next.GetType().ToString());
        }
        if (subparagraphs.Any())
            return new BranchParagraph { Number = div.Number, Intro = intro, Children = subparagraphs };
        if (intro.Count == div.Contents.Count())
            return div;
        return new WNewNumberedParagraph(div.Number, intro);
    }

    private ILeaf ParseSimpleParagraph(ILine line, bool sub) {
        i += 1;
        if (line is WOldNumberedParagraph np)
            return new WNewNumberedParagraph(np.Number, new WLine(np, np.Contents));
        return new WDummyDivision(line);
    }

    /* annexes */

    protected virtual bool IsFirstLineOfAnnex(IBlock block) {
        if (block is not WLine line)
            return false;
        String text = line.NormalizedContent;
        if (Regex.IsMatch(text, @"^\s*ANNEX\s+\d+\s*$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(text, @"^\s*Appendix\s*$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(text, @"^\s*-\sANNEX\s-\s*$", RegexOptions.IgnoreCase))
            return true;
        // if (Regex.IsMatch(text, @"^\s*Appendix\s+No\.?\s*\d+\s*$", RegexOptions.IgnoreCase))
        //     return true;
        return false;
    }

    private List<IAnnex> Annexes() {
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
