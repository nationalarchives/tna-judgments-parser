
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Vml = DocumentFormat.OpenXml.Vml;

using Microsoft.Extensions.Logging;

using AttachmentPair = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class AbstractParser {

    private static ILogger logger = Logging.Factory.CreateLogger<Parse.AbstractParser>();

    protected readonly WordprocessingDocument doc;
    protected readonly IOutsideMetadata meta;
    private readonly IEnumerable<AttachmentPair> attachments;
    protected readonly MainDocumentPart main;
    protected readonly OpenXmlElementList elements;
    protected int i = 0;

    protected AbstractParser(WordprocessingDocument doc, IOutsideMetadata meta = null, IEnumerable<AttachmentPair> attachments = null) {
        this.doc = doc;
        this.meta = meta;
        this.attachments = attachments ?? Enumerable.Empty<AttachmentPair>();
        main = doc.MainDocumentPart;
        elements = main.Document.Body.ChildElements;
    }

    protected Judgment Parse(JudgmentType type = JudgmentType.Judgment) {
        logger.LogDebug($"invoking parser { this.GetType().FullName }");
        int save = i;
        IEnumerable<IBlock> coverPage = CoverPage();
        if (coverPage is null)
            i = save;
        save = i;
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
        if (i != elements.Count) {
            logger.LogDebug("parsing did not complete: " + i);
            logger.LogTrace(elements.ElementAt(i).InnerText);
            throw new Exception();
        }
        if (coverPage is not null)
            coverPage = EnrichCoverPage(coverPage);
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
            CoverPage = coverPage,
            Header = header,
            Body = body,
            Conclusions = conclusions,
            Annexes = annexes,
            InternalAttachments = attachments
        };
    }

    private IEnumerable<IBlock> CoverPage() {
        Header header = DOCX.Headers.GetFirst(main);
        if (header is null)
            return null;
        return Blocks.ParseBlocks(main, header.ChildElements);
    }

    protected abstract List<IBlock> Header();

    private readonly List<Enricher> defaultEnrichers = new List<Enricher>(2) {
        new RemoveTrailingWhitespace(),
        new Merger()
    };

    protected virtual IEnumerable<IBlock> EnrichCoverPage(IEnumerable<IBlock> coverPage) {
        return Enricher.Enrich(coverPage, defaultEnrichers);
    }
    protected virtual IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        return Enricher.Enrich(header, defaultEnrichers);
    }
    protected virtual IEnumerable<IDecision> EnrichBody(IEnumerable<IDecision> body) {
        return Enricher.Enrich(body, defaultEnrichers);
    }
    protected virtual IEnumerable<IBlock> EnrichConclusions(IEnumerable<IBlock> conclusions) {
        return Enricher.Enrich(conclusions, defaultEnrichers);
    }
    protected virtual IEnumerable<IAnnex> EnrichAnnexes(IEnumerable<IAnnex> annexes) {
        return Enricher.Enrich(annexes, defaultEnrichers);
    }

    // protected abstract List<IDecision> Body();

    // protected abstract List<IAnnex> Annexes();

    internal static bool IsSkippable(OpenXmlElement e) {
        if (e is SectionProperties)
            return true;
        if (e is BookmarkStart || e is BookmarkEnd)
            return true;
        if (e.Descendants().OfType<Drawing>().Any())
            return false;
        if (e.Descendants().OfType<Picture>().Any())
            return false;
        if (e.Descendants().OfType<Vml.Shape>().Any())
            return false;
        if (e is Paragraph p && string.IsNullOrWhiteSpace(p.InnerText))
            return true;
        if (e is PermEnd)
            return true;
        return false;
    }

    protected void AddBlock(OpenXmlElement e, List<IBlock> collection) {
        if (IsSkippable(e)) {
            ;
        } else if (e is Paragraph para) {
            var np = Blocks.ParseParagraph(doc.MainDocumentPart, para);
            collection.Add(np);
        } else if (e is Table table) {
            var t = new WTable(doc.MainDocumentPart, table);
            collection.Add(t);
        } else if (e is SdtBlock sdt) { // EWCA/Civ/2018/1536.pdf
            var blocks = Blocks.ParseStdBlock(main, sdt);
            collection.AddRange(blocks);
        } else {
            throw new Exception(e.GetType().ToString());
        }
        i += 1;
    }










    /* */

    /* */

    /* */


    private static readonly string[] titledJudgeNamePatterns = {
        @"^MRS?\.? JUSTICE( [A-Z]+)+$",
        @"^(Lord|Lady|Mrs?|The Honourable Mrs?) Justice ([A-Z][a-z]* )?[A-Z][a-z]+(-[A-Z][a-z]+)?( VP)?$",
        @"^Mrs? ([A-Z]\.){1,3} [A-Z][a-z]+$",
        @"^[A-Z][a-z]+ [A-Z][a-z]+ QC, Deputy High Court Judge$"
    };
    private IEnumerable<Regex> titledJudgeNameRegexes = titledJudgeNamePatterns
        .Select(p => new Regex(p));

    protected virtual bool IsTitledJudgeName(OpenXmlElement e) {
        if (e is not Paragraph)
            return false;
        return IsTitledJudgeName(e.InnerText);
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
        @"^MRS?\.? JUSTICE( [A-Z]+)+: ",
        @"^(LORD|LADY|MRS?) JUSTICE( [A-Z]+)+: ",
        @"^(Lord|Lady|Mrs?|The Honourable Mrs?) Justice ([A-Z][a-z]* )?[A-Z][a-z]+(-[A-Z][a-z]+)?( VP)?: ",
        @"^Mrs? ([A-Z]\.){1,3} [A-Z][a-z]+: ",
        @"^[A-Z][a-z]+ [A-Z][a-z]+ QC, Deputy High Court Judge: "
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
    protected bool StartsWithTitledJudgeName(OpenXmlElement e) {
        if (e is not Paragraph p)
            return false;
        string text = DOCX.Fields.ExtractInnerTextExcludingFieldCodes(p);
        return StartsWithTitledJudgeName(text);
    }

    protected abstract List<IDecision> Body();

    protected List<IDecision> Decisions() {
        List<IDecision> decisions = new List<IDecision>();
        while (i < elements.Count) {
            logger.LogTrace("parsing element " + i);
            OpenXmlElement e = elements.ElementAt(i);
            if (IsFirstLineOfAnnex(e))
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

    protected IDecision Decision() {
        logger.LogTrace("parsing element " + i);
        OpenXmlElement e = elements.ElementAt(i);
        if (!IsTitledJudgeName(e))
            return null;
        WLine author = new WLine(main, (Paragraph) e);
        i += 1;
        if (i == elements.Count)
            return null;
        List<IDivision> contents = Divisions();
        if (contents is null || contents.Count == 0)
            return null;
        return new Decision { Author = author, Contents = contents };
    }

    protected List<IDivision> Divisions() {
        int save = i;
        List<IDivision> bigLevels = BigLevels();
        if (bigLevels is not null && bigLevels.Count > 1)
            return bigLevels;
        logger.LogDebug("rolling back big-levels at index " + i + ": ");
        if (i < elements.Count)
            logger.LogTrace(elements.ElementAt(i).OuterXml);
        i = save;
        List<IDivision> xHeads = CrossHeadings();
        if (xHeads is not null && xHeads.Count > 1)
            return xHeads;
        logger.LogDebug("rolling back cross-headings at index " + i + ": ");
        if (i < elements.Count)
            logger.LogTrace(elements.ElementAt(i).OuterXml);
        i = save;
        return ParagraphsUntilAnnex();
    }

    /* numbered big levels */

    string[] bigLevelNumberingFormats = {
        @"^([A-Z]\.) ",
        @"^(\(\d+\)) ",
        @"^(\d+\.) ",
        @"^(\([a-z]\)) ",
        @"^(\([ivx]+\)) "
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
        while (i < elements.Count) {
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

    private string NormalizeFirstLineOfBigLevel(OpenXmlElement e, string format) {
        IEnumerable<string> texts = e.Descendants()
            .Where(e => e is Text || e is TabChar)
            .Select(e => { if (e is Text) return e.InnerText; if (e is TabChar) return " "; return ""; });
        return string.Join("", texts).Trim();
    }

    protected WText GetNumberFromFirstLineOfBigLevel(OpenXmlElement e, string format) {
        string text = NormalizeFirstLineOfBigLevel(e, format);
        Match match = Regex.Match(text, format);
        if (!match.Success)
            return null;
        string number = match.Groups[1].Value;
        RunProperties rPr = e.Descendants<RunProperties>().FirstOrDefault();
        return new WText(number, rPr);
    }

    protected WLine RemoveNumberFromFirstLineOfBigLevel(Paragraph e, string format) {
        IEnumerable<IInline> unfiltered = Inline.ParseRuns(main, e.ChildElements);
        unfiltered = Merger.Merge(unfiltered);
        unfiltered = unfiltered.SkipWhile(inline => inline is WLineBreak || inline is WTab);
        IInline first = unfiltered.First();
        if (first is not WText t1)
            throw new Exception();
        Match match = Regex.Match(t1.Text.TrimStart(), format); // TrimStart for 00356_ukut_iac_2019_ms_belgium.doc
        if (match.Success) {
            string rest = t1.Text.TrimStart().Substring(match.Length).TrimStart();
            if (string.IsNullOrEmpty(rest)) {
                return new WLine(main, e.ParagraphProperties, unfiltered.Skip(1));
            } else {
                WText prepend = new WText(rest, t1.properties);
                return new WLine(main, e.ParagraphProperties, unfiltered.Skip(1).Prepend(prepend));
            }
        }
        match = Regex.Match(t1.Text + " ", format + @"$");
        if (match.Success) {
            IInline second = unfiltered.Skip(1).FirstOrDefault();
            if (second is WText t2) {
                WText prepend = new WText(t2.Text.TrimStart(), t2.properties);
                return new WLine(main, e.ParagraphProperties, unfiltered.Skip(2).Prepend(prepend));
            } else if (second is WTab) {
                return new WLine(main, e.ParagraphProperties, unfiltered.Skip(2));
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
                    return new WLine(main, e.ParagraphProperties, unfiltered.Skip(2));
                } else {
                    WText prepend = new WText(rest, t2bis.properties);
                    return new WLine(main, e.ParagraphProperties, unfiltered.Skip(2).Prepend(prepend));
                }
            }
            match = Regex.Match(combined + " ", format + @"$");
            if (match.Success) {
                IInline third = unfiltered.Skip(2).FirstOrDefault();
                if (third is WTab)  // [2022] UKSC 21
                    return new WLine(main, e.ParagraphProperties, unfiltered.Skip(3));
            }
        }
        throw new Exception();
    }

    private bool IsFirstLineOfBigLevel(OpenXmlElement e, string format) {
        if (e is not Paragraph p)
            return false;
        if (!DOCX.Paragraphs.IsFlushLeft(main, p))
            return false;
        string text = NormalizeFirstLineOfBigLevel(e, format);
        if (Regex.IsMatch(text, format)) {
            logger.LogTrace("This is a BigLevel: ");
            logger.LogTrace(e.InnerText);
            return true;
        }
        return false;
    }
    protected bool IsFirstLineOfBigLevel(OpenXmlElement e) {
        return IsFirstLineOfBigLevel(e, bigLevelNumberingFormats);
    }
    private bool IsFirstLineOfBigLevel(OpenXmlElement e, string[] formats) {
        foreach (string format in formats)
            if (IsFirstLineOfBigLevel(e, format))
                return true;
        return false;
    }

    private BigLevel BigLevel(string format, string[] childFormats, string[] ancestorFormats) {
        logger.LogTrace("parsing element " + i);
        OpenXmlElement e = elements.ElementAt(i);
        while (IsSkippable(e) && i < elements.Count - 1) {
            i += 1;
            logger.LogTrace("parsing element " + i);
            e = elements.ElementAt(i);;
        }
        if (!IsFirstLineOfBigLevel(e, format))
            return null;
        Paragraph p = (Paragraph) e;
        WText number = GetNumberFromFirstLineOfBigLevel(p, format);
        WLine heading = RemoveNumberFromFirstLineOfBigLevel(p, format);
        // WLine heading = new WLine(doc.MainDocumentPart, (Paragraph) e);
        i += 1;
        int save = i;
        List<IDivision> children;
        children = BigLevels(childFormats, ancestorFormats.AsEnumerable().Append(format).ToArray());
        if (children is null || children.Count < 2) {
            i = save;
            children = ParagraphsUntilBigLevelOrAnnex(format, ancestorFormats);
        }
        // List<IDivision> children = ParagraphsUntilBigLevelOrAnnex(format);
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
        while (i < elements.Count) {
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

    protected virtual bool IsFirstLineOfCrossHeading(OpenXmlElement e) {
        if (e is not Paragraph p)
            return false;
        bool hasNumberOrMarker = DOCX.Numbering.HasNumberOrMarker(doc.MainDocumentPart, p);
        object formattedNumber = DOCX.Numbering2.GetFormattedNumber(doc.MainDocumentPart, p);
        if (hasNumberOrMarker && formattedNumber is null)
            throw new Exception();
        if (hasNumberOrMarker)
            return false;
        if (formattedNumber is not null)
            throw new Exception();
        if (IsFirstLineOfBigLevel(e, bigLevelNumberingFormats))
            return false;
        // StringValue indent = Util.GetLeftIndent(doc.MainDocumentPart, p);
        // System.Console.WriteLine("indent " + indent + " - " + e.InnerText);
        // bool value = indent is null || indent == "0";
        bool value = DOCX.Paragraphs.IsFlushLeft(doc.MainDocumentPart, p);
        // if (e.InnerText == "Essential Factual Background" && !value)
        //     throw new Exception();
        // if (e.InnerText == "The Course of the Proceedings at Lewes Crown Court" && !value)
        //     throw new Exception();
        // if (e.InnerText == "Grounds of Appeal" && !value)
        //     throw new Exception();
        // if (e.InnerText == "Discussion and Conclusions" && !value)
        //     throw new Exception();
        // if (e.InnerText == "Disposal" && !value)
        //     throw new Exception();
        // if (value) {
        //     System.Console.Write("This is a CrossHeading: ");
        //     System.Console.WriteLine(e.InnerText);
        // } else {
        //     System.Console.Write("This is not a CrossHeading: ");
        //     System.Console.WriteLine(e.InnerText);
        // }
        return value;
    }

    private CrossHeading CrossHeading() {
        logger.LogTrace("parsing element " + i);
        OpenXmlElement e = elements.ElementAt(i);
        while (IsSkippable(e)) {
            i += 1;
            logger.LogTrace("parsing element " + i);
            e = elements.ElementAt(i);;
        }
        if (!IsFirstLineOfCrossHeading(e))
            return null;
        WLine heading = new WLine(doc.MainDocumentPart, (Paragraph) e);
        i += 1;
        if (i == elements.Count)
            return null;

        List<IDivision> children = ParagraphsUntilCrossHeadingOrAnnex();
        if (children.Count == 0) {
            logger.LogDebug("Abandoning CrossHeading: ");
            logger.LogTrace(e.InnerText);
            return null;
        }
        return new CrossHeading { Heading = heading, Children = children };
    }

    /* paragraphs */

    public delegate bool StopParsingParagraphs(OpenXmlElement e);

    protected List<IDivision> ParagraphsUntil(StopParsingParagraphs predicate) {
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (IsSkippable(e)) {
                i += 1;
                continue;
            }
            if (predicate(e))
                break;
            IDivision para = ParseParagraph();
            paragraphs.Add(para);
        }
        return paragraphs;
    }

    protected virtual bool IsFirstLineOfConclusions(OpenXmlElement e) => false;

    protected List<IDivision> ParagraphsUntilEndOfBody() {
        StopParsingParagraphs predicate = e => {
            if (IsFirstLineOfConclusions(e))
                return true;;
            if (IsFirstLineOfAnnex(e))
                return true;;
            return false;
        };
        return ParagraphsUntil(predicate);
    }

    protected virtual List<IDivision> ParagraphsUntilAnnex() {
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (IsFirstLineOfAnnex(e))
                break;
            if (IsTitledJudgeName(e))
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
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (IsFirstLineOfAnnex(e))
                break;
            AddBlock(e, blocks);
        }
        return blocks;
    }


    private List<IDivision> ParagraphsUntilCrossHeadingOrAnnex() {
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (IsSkippable(e)) {   // this is unnecessary
                i += 1;
                continue;
            }
            if (IsFirstLineOfCrossHeading(e))
                break;
            if (IsFirstLineOfAnnex(e))
                break;
            if (IsTitledJudgeName(e))
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
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (IsSkippable(e)) {   // this is unnecessary
                i += 1;
                continue;
            }
            if (IsFirstLineOfBigLevel(e, format))
                break;
            if (IsFirstLineOfBigLevel(e, ancestorFormats))
                break;
            if (IsFirstLineOfAnnex(e))
                break;
            if (IsTitledJudgeName(e))
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
        OpenXmlElement e = elements.ElementAt(i);
        if (IsSkippable(e)) {
            i += 1;
            return null;
        }
        if (e is Paragraph p) {
            return ParseParagraphAndSubparagraphs(p);
        }
        if (e is Table table) {
            i += 1;
            var t = new WTable(doc.MainDocumentPart, table);
            return new WDummyDivision(t);
        }
        if (e is SdtBlock sdt) { // "EWHC/Admin/2021/30"
            i += 1;
            return Blocks.ParseStructuredDocumentTag2(main, sdt);
        }

        throw new System.Exception(e.GetType().ToString());
    }

    private IDivision ParseParagraphAndSubparagraphs(Paragraph p) {
        ILeaf div = ParseSimpleParagraph(p);
        if (i == elements.Count)
            return div;
        if (div.Heading is not null)
            return div;
        if (div.Contents.Count() != 1)
            return div;
        if (div.Contents.First() is not WLine)
            return div;

        float left1 = DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, p.ParagraphProperties) ?? 0.0f;
        float firstLine1 = DOCX.Paragraphs.GetFirstLineIndentWithNumberingAndStyleInInches(main, p.ParagraphProperties) ?? 0.0f;
        float indent1 = firstLine1 > 0 ? left1 : left1 + firstLine1;
        // float left2 = DOCX.Paragraphs.GetLeftIndentWithStyleButNotNumberingInInches(main, p.ParagraphProperties) ?? 0.0f;
        // float firstLine2 = DOCX.Paragraphs.GetFirstLineIndentWithStyleButNotNumberingInInches(main, p.ParagraphProperties) ?? 0.0f;
        // float indent2 = firstLine2 > 0 ? left2 : left2 + firstLine2;

        List<IBlock> intro = new List<IBlock>();
        intro.AddRange(div.Contents);
        List<IDivision> subparagraphs = new List<IDivision>();

        while (i < elements.Count) {
            OpenXmlElement next = elements.ElementAt(i);
            if (IsSkippable(next)) {
                i += 1;
                continue;
            }
            if (next is Table table) {
                if (subparagraphs.Any())
                    break;
                intro.Add(new WTable(doc.MainDocumentPart, table));
                i += 1;
                continue;
            }
            if (next is SdtBlock sdt) { // "EWHC/Admin/2021/30"
                if (subparagraphs.Any())
                    break;
                intro.AddRange(Blocks.ParseStdBlock(main, sdt));
                i += 1;
                continue;
            }
            if (next is Paragraph nextPara) {
                float nextLeft1 = DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, nextPara.ParagraphProperties) ?? 0.0f;
                float nextFirstLine1 = DOCX.Paragraphs.GetFirstLineIndentWithNumberingAndStyleInInches(main, nextPara.ParagraphProperties) ?? 0.0f;
                float nextIndent1 = nextFirstLine1 > 0 ? nextLeft1 : nextLeft1 + nextFirstLine1;
                if (nextIndent1 <= indent1)
                    break;
                // float nextLeft2 = DOCX.Paragraphs.GetLeftIndentWithStyleButNotNumberingInInches(main, nextPara.ParagraphProperties) ?? 0.0f;
                // float nextFirstLine2 = DOCX.Paragraphs.GetFirstLineIndentWithStyleButNotNumberingInInches(main, nextPara.ParagraphProperties) ?? 0.0f;
                // float nextIndent2 = nextFirstLine2 > 0 ? nextLeft2 : nextLeft2 + nextFirstLine2;

                IDivision subparagraph = ParseParagraphAndSubparagraphs(nextPara);
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

    private ILeaf ParseSimpleParagraph(Paragraph p) {
        i += 1;
        WLine line = new WLine(doc.MainDocumentPart, p);
        DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, p);
        if (info is not null) {
            DOCX.WNumber number = new DOCX.WNumber(main, info.Value, p);
            return new WNewNumberedParagraph(number, new WLine(line) { IsFirstLineOfNumberedParagraph = true });
        }
        INumber num2 = Fields.RemoveListNum(line);
        if (num2 is not null)
            return new WNewNumberedParagraph(num2, new WLine(line) { IsFirstLineOfNumberedParagraph = true });
        string format = @"^(“?\d+\.?) (?!(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec))";
        WText num3 = GetNumberFromFirstLineOfBigLevel(p, format);
        if (num3 is not null) {
            WLine line1 = RemoveNumberFromFirstLineOfBigLevel(p, format);
            line1.IsFirstLineOfNumberedParagraph = true;
            return new WNewNumberedParagraph(num3, line1);
        }
        return new WDummyDivision(line);
    }

    /* annexes */

    private readonly Regex annexPattern = new Regex(@"^\s*ANNEX\s+\d+\s*$");

    protected virtual bool IsFirstLineOfAnnex(OpenXmlElement e) {
        return annexPattern.IsMatch(e.InnerText);
    }

    private List<IAnnex> Annexes() {
        List<IAnnex> annexes = new List<IAnnex>();
        while (i < elements.Count) {
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
        OpenXmlElement e = elements.ElementAt(i);
        if (!IsFirstLineOfAnnex(e))
            return null;
            // throw new Exception();
        WLine number = new WLine(doc.MainDocumentPart, (Paragraph) e);
        i += 1;
        if (i == elements.Count)
            return null;
            // throw new Exception("An annex must have contents.");
        List<IBlock> contents = new List<IBlock>();
        while (i < elements.Count) {
            e = elements.ElementAt(i);
            if (IsFirstLineOfAnnex(e))
                break;
            if (IsTitledJudgeName(e))
                break;
            AddBlock(e, contents);
        }
        return new Annex { Number = number, Contents = contents };
    }

}

}
