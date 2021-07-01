
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class Parser {

    public static Judgment Parse(WordprocessingDocument doc) {
        return new Parser(doc).Parse();  // .Augment();
    }

    private readonly WordprocessingDocument doc;
    private readonly MainDocumentPart main;
    private readonly OpenXmlElementList elements;
    // private readonly MainDocumentPart main;
    // private Judgment judgment;
    private int i = 0;

    private Parser(WordprocessingDocument doc) {
        this.doc = doc;
        main = doc.MainDocumentPart;
        elements = main.Document.Body.ChildElements;
    }

    private Judgment Parse() {
        // Judgment judgment = new Judgment(doc);
        int save = i;
        IEnumerable<IBlock> header = Header();
        // judgment.Header = Header();
        if (header is null)
            i = save;
        else
            header = Augmentation.AugmentHeader(header);
        IEnumerable<IDecision> body = Body();
        body = Augmentation.AugmentBody(body);
        IEnumerable<IAnnex> annexes = Annexes();
        if (i != doc.MainDocumentPart.Document.Body.ChildElements.Count) {
            System.Console.WriteLine("parsing did not complete: " + i);
            System.Console.WriteLine(doc.MainDocumentPart.Document.Body.ChildElements.ElementAt(i).InnerText);
            throw new Exception();
        }
        annexes = Augmentation.AugmentAnnexes(annexes);
        return new Judgment(doc) { Header = header, Body = body, Annexes = annexes };
    }

    private bool IsSkippable(OpenXmlElement e) {
        if (e is SectionProperties)
            return true;
        if (e is BookmarkStart || e is BookmarkEnd)
            return true;
        if (e.Descendants().OfType<Drawing>().Any())
            return false;
        if (e.Descendants().OfType<Picture>().Any())
            return false;
        if (e is Paragraph p && string.IsNullOrWhiteSpace(p.InnerText))
            return true;
        return false;
    }

    private void Add(OpenXmlElement e, List<IBlock> collection) {
        if (IsSkippable(e)) {
            ;
        } else if (e is Paragraph para) {
            var np = Blocks.Parse1(doc.MainDocumentPart, para);
            collection.Add(np);
        } else if (e is Table table) {
            var t = new WTable(doc.MainDocumentPart, table);
            collection.Add(t);
        // } else if (IsSkippable(e)) {
        //     ;
        } else {
            throw new System.Exception(e.GetType().ToString());
        }
        i += 1;
    }

    private List<IBlock> Header() {
        // OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        List<IBlock> header = new List<IBlock>();
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (e.InnerText == "Approved Judgment")
                break;
            if (e.InnerText == "(Approved)" && elements.ElementAt(i-1).InnerText == "JUDGMENT")
                break;
            if (e is Paragraph p && p.ParagraphProperties?.ParagraphStyleId?.Val == "CoverDesc" && e.InnerText.StartsWith("Judgment Approved by the court"))
                break;
            Add(e, header);
        }
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (e is Paragraph && IsTitledJudgeName(e.InnerText)) {
                return header;
            }
            Add(e, header);
        }
        return null;
    }

    private static readonly string[] titledJudgeNamePatterns = {
        @"^MRS? JUSTICE [A-Z]+$",
        @"^(Lord|Lady|Mrs?|The Honourable Mrs?) Justice ([A-Z][a-z]* )?[A-Z][a-z]+(-[A-Z][a-z]+)?( VP)?$",
        @"^Mrs? ([A-Z]\.){1,3} [A-Z][a-z]+$",
        @"^[A-Z][a-z]+ [A-Z][a-z]+ QC, Deputy High Court Judge$"
    };
    IEnumerable<Regex> titledJudgeNameRegexes = titledJudgeNamePatterns
        .Select(p => new Regex(p));

    private bool IsTitledJudgeName(OpenXmlElement e) {
        if (e is not Paragraph)
            return false;
        return IsTitledJudgeName(e.InnerText);
    }
    private bool IsTitledJudgeName(string text) {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.EndsWith(":"))
            text = text.Substring(0 , text.Length - 1).Trim();
        // string[] patterns = {
        //     @"^MRS? JUSTICE [A-Z]+$",
        //     @"^(Lord|Mrs?|The Honourable Mrs?) Justice ([A-Z][a-z]* )?[A-Z][a-z]+( VP)?$",
        //     @"^Mrs? ([A-Z]\.){1,3} [A-Z][a-z]+$",
        //     @"^[A-Z][a-z]+ [A-Z][a-z]+ QC, Deputy High Court Judge$"
        // };
        // foreach (string pattern in patterns)
        //     if (Regex.Match(text, pattern).Success)
        //         return true;
        foreach (Regex re in titledJudgeNameRegexes)
            if (re.IsMatch(text))
                return true;
        return false;
    }

    private List<IDecision> Body() {
        OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        List<IDecision> decisions = new List<IDecision>();
        // OpenXmlElement e = elements.ElementAt(i);
        // foreach (OpenXmlElement e in doc.MainDocumentPart.Document.Body.ChildElements.Skip(i)) {
        //     if (e is Paragraph para) {
        //         judgment.body.Add(WNumberedParagraph.Parse(doc.MainDocumentPart, para));
        //         continue;
        //     } else if (e is Table table) {
        //         judgment.body.Add(new WTable(doc.MainDocumentPart, table));
        //         continue;
        //     } else if (e is SectionProperties)
        //         continue;
        //     else if (e is BookmarkStart)
        //         continue;
        //     else if (e is BookmarkEnd)
        //         continue;
        //     throw new System.Exception(e.GetType().ToString());
        // }
        // OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        while (i < elements.Count) {
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
            // OpenXmlElement e = elements.ElementAt(i);
            // Add(e, judgment.body);
        }
        return decisions;
    }

    // private IDecision Decision() {
    //     OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
    //     OpenXmlElement e = elements.ElementAt(i);
    //     if (e is not Paragraph || !IsTitledJudgeName(e.InnerText))
    //         throw new Exception("A decision should start with the name of a Judge.");
    //     WLine author = new WLine(doc.MainDocumentPart, (Paragraph) e);
    //     i += 1;
    //     if (i == elements.Count)
    //         throw new Exception("A decision must have contents.");
    //     List<IBlock> contents = new List<IBlock>();
    //     while (i < elements.Count) {
    //         e = elements.ElementAt(i);
    //         if (e is Paragraph && IsTitledJudgeName(e.InnerText))
    //             break;
    //         if (IsFirstLineOfAnnex(e))
    //             break;
    //         Add(e, contents);
    //     }
    //     return new Decision { Author = author, Contents = contents };
    // }

    // private void AddLeaf(OpenXmlElement e, List<IDivision> collection) {
    //     if (e is Paragraph para) {
    //         string number = Numbering.GetFormattedNumber(doc.MainDocumentPart, para);
    //         if (number is null) {

    //         } else {

    //         }
    //         collection.Add(np);
    //     } else if (e is Table table) {
    //         var t = new WTable(doc.MainDocumentPart, table);
    //         collection.Add(t);
    //     } else if (e is SectionProperties || e is BookmarkStart || e is BookmarkEnd) {
    //         ;
    //     } else {
    //         throw new System.Exception(e.GetType().ToString());
    //     }
    //     i += 1;
    // }


    private IDecision Decision() {
        OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        OpenXmlElement e = elements.ElementAt(i);
        // if (e is not Paragraph) {

        // }
        // if (e is not Paragraph || !IsTitledJudgeName(e.InnerText))
        //     throw new Exception("A decision should start with the name of a Judge.");
        if (!IsTitledJudgeName(e.InnerText))
            return null;
            // throw new Exception("A decision should start with the name of a Judge.");
        WLine author = new WLine(doc.MainDocumentPart, (Paragraph) e);
        i += 1;
        if (i == elements.Count)
            return null;
            // throw new Exception("A decision must have contents.");
        List<IDivision> contents = Divisions();
        // List<IDivision> contents = new List<IDivision>();
        // while (i < elements.Count) {
        //     e = elements.ElementAt(i);
        //     if (e is Paragraph && IsTitledJudgeName(e.InnerText))
        //         break;
        //     if ()
        //         break;
        //     if (IsFirstLineOfAnnex(e))
        //         break;
        //     AddDivision(e, contents);
        // }
        if (contents is null || contents.Count == 0)
            return null;
        return new Decision { Author = author, Contents = contents };
    }

    private List<IDivision> Divisions() {
        int save = i;
        List<IDivision> bigLevels = BigLevels();
        if (bigLevels is not null && bigLevels.Count > 1)
            return bigLevels;
        System.Console.WriteLine("rolling back big-levels at index " + i + ": ");
        if (i < elements.Count)
            System.Console.WriteLine(elements.ElementAt(i).OuterXml);
        i = save;
        List<IDivision> xHeads = CrossHeadings();
        if (xHeads is not null && xHeads.Count > 1)
            return xHeads;
        System.Console.WriteLine("rolling back cross-headings at index " + i + ": ");
        if (i < elements.Count)
            System.Console.WriteLine(elements.ElementAt(i).OuterXml);
        i = save;
        return ParagraphsUntilAnnex();
    }

    /* numbered big levels */

    string[] bigLevelNumberingFormats = {
        @"^([A-Z]\.) ",
        @"^(\(\d+\)) ",
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

    private WText GetNumberFromFirstLineOfBigLevel(OpenXmlElement e, string format) {
        string text = NormalizeFirstLineOfBigLevel(e, format);
        Match match = Regex.Match(text, format);
        if (!match.Success)
            return null;
        string number = match.Groups[1].Value;
        RunProperties rPr = e.Descendants<RunProperties>().FirstOrDefault();
        return new WText(number, rPr);
    }

    private WLine RemoveNumberFromFirstLineOfBigLevel(Paragraph e, string format) {
        IEnumerable<IInline> unfiltered = Inline.ParseRuns(main, e.ChildElements);
        unfiltered = Augmentation.MergeRuns(unfiltered);
        IInline first = unfiltered.First();
        if (first is not WText t1)
            throw new Exception();
        Match match = Regex.Match(t1.Text, format);
        if (match.Success) {
            string rest = t1.Text.Substring(match.Length).TrimStart();
            if (string.IsNullOrEmpty(rest)) {
                return new WLine(main, e.ParagraphProperties, unfiltered.Skip(1)) { IsFirstLineOfNumberedParagraph = true };
            } else {
                WText prepend = new WText(rest, t1.properties);
                return new WLine(main, e.ParagraphProperties, unfiltered.Skip(1).Prepend(prepend)) { IsFirstLineOfNumberedParagraph = true };
            }
        }
        match = Regex.Match(t1.Text + " ", format + @"$");
        if (match.Success) {
            IInline second = unfiltered.Skip(1).FirstOrDefault();
            if (second is not null && second is WTab) {
                return new WLine(main, e.ParagraphProperties, unfiltered.Skip(2));
            } else if (second is not null && second is WText t2) {
                WText prepend = new WText(t2.Text.TrimStart(), t2.properties);
                return new WLine(main, e.ParagraphProperties, unfiltered.Skip(2).Prepend(prepend));
            } else {
                throw new Exception();
            }
        }
        throw new Exception();
    }

    private bool IsFirstLineOfBigLevel(OpenXmlElement e, string format) {
        if (e is not Paragraph p)
            return false;
        if (e.InnerText.StartsWith("(i) The ")) {
            bool junk1 = DOCX.Paragraphs.IsFlushLeft(main, p);
            string junk2 = NormalizeFirstLineOfBigLevel(e, format);
            bool junk3 = Regex.IsMatch(junk2, format);
        }
        if (!DOCX.Paragraphs.IsFlushLeft(main, p))
            return false;
        string text = NormalizeFirstLineOfBigLevel(e, format);
        if (Regex.IsMatch(text, format)) {
            System.Console.Write("This is a BigLevel: ");
            System.Console.WriteLine(e.InnerText);
            return true;
        }
        return false;
    }
    private bool IsFirstLineOfBigLevel(OpenXmlElement e, string[] formats) {
        foreach (string format in formats)
            if (IsFirstLineOfBigLevel(e, format))
                return true;
        return false;
    }

    private BigLevel BigLevel(string format, string[] childFormats, string[] ancestorFormats) {
        OpenXmlElement e = elements.ElementAt(i);
        while (IsSkippable(e)) {
            i += 1;
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

    private List<IDivision> CrossHeadings() {
        // OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        List<IDivision> crossHeadings = new List<IDivision>();
        // int save1 = i;
        List<IDivision> intro = ParagraphsUntilCrossHeadingOrAnnex();
        if (intro.Count > 0) {
            IDivision wrapper = new GroupOfParagraphs() { Children = intro };
            crossHeadings.Add(wrapper);
        }
        while (i < elements.Count) {
            // if (IsSkippable(e)) {
            //     i += 1;
            //     continue;
            // }
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

    private bool IsFirstLineOfCrossHeading(OpenXmlElement e) {
        if (e is not Paragraph p)
            return false;
        if (DOCX.Numbering.HasNumberOrMarker(doc.MainDocumentPart, p) && DOCX.Numbering2.GetFormattedNumber(doc.MainDocumentPart, p) is null)
            throw new Exception();
        if (DOCX.Numbering.HasNumberOrMarker(doc.MainDocumentPart, p))
            return false;
        if (DOCX.Numbering2.GetFormattedNumber(doc.MainDocumentPart, p) is not null)
            throw new Exception();
        if (IsFirstLineOfBigLevel(e, bigLevelNumberingFormats))
            return false;
        // StringValue indent = Util.GetLeftIndent(doc.MainDocumentPart, p);
        // System.Console.WriteLine("indent " + indent + " - " + e.InnerText);
        // bool value = indent is null || indent == "0";
        bool value = DOCX.Paragraphs.IsFlushLeft(doc.MainDocumentPart, p);
        if (e.InnerText == "Essential Factual Background" && !value) {
            float? left = DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, p.ParagraphProperties);
            float? hanging = DOCX.Paragraphs.GetFirstLineIndentWithNumberingAndStyleInInches(main, p.ParagraphProperties);
            throw new Exception();
        }
        if (e.InnerText == "The Course of the Proceedings at Lewes Crown Court" && !value)
            throw new Exception();
        if (e.InnerText == "Grounds of Appeal" && !value) {
            float? left = DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, p.ParagraphProperties);
            float? hanging = DOCX.Paragraphs.GetFirstLineIndentWithNumberingAndStyleInInches(main, p.ParagraphProperties);
            throw new Exception();
        }
        if (e.InnerText == "Discussion and Conclusions" && !value)
            throw new Exception();
        if (e.InnerText == "Disposal" && !value)
            throw new Exception();
        if (value) {
            System.Console.Write("This is a CrossHeading: ");
            System.Console.WriteLine(e.InnerText);
        }
        return value;
    }

    private CrossHeading CrossHeading() {
        // OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        OpenXmlElement e = elements.ElementAt(i);
        while (IsSkippable(e)) {
            i += 1;
            e = elements.ElementAt(i);;
        }
        if (!IsFirstLineOfCrossHeading(e))
            return null;
        WLine heading = new WLine(doc.MainDocumentPart, (Paragraph) e);
        i += 1;
        if (i == elements.Count)
            return null;
            // throw new Exception("A cross heading must have contents.");

        List<IDivision> children = ParagraphsUntilCrossHeadingOrAnnex();
        // List<IDivision> children = new List<IDivision>();
        // while (i < elements.Count) {
        //     e = elements.ElementAt(i);
        //     if (IsSkippable(e)) {
        //         i += 1;
        //         continue;
        //     }
        //     if (IsFirstLineOfCrossHeading(e))
        //         break;
        //     if (IsFirstLineOfAnnex(e))
        //         break;
        //     ILeaf para = ParseParagraph();
        //     if (para is null)
        //         continue;
        //     children.Add(para);
        // }
        // List<IDivision> children = Paragraphs();
        if (children.Count == 0)
            return null;
        return new CrossHeading { Heading = heading, Children = children };
    }

    /* paragraphs */

    private List<IDivision> ParagraphsUntilAnnex() {
        OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (IsFirstLineOfAnnex(e))
                break;
            if (IsTitledJudgeName(e))
                break;
            ILeaf para = ParseParagraph();
            if (para is null)
                continue;
            paragraphs.Add(para);
        }
        return paragraphs;
    }

    private List<IDivision> ParagraphsUntilCrossHeadingOrAnnex() {
        // OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
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
            ILeaf para = ParseParagraph();
            if (para is null)
                continue;
            paragraphs.Add(para);
        }
        return paragraphs;
    }

    private List<IDivision> ParagraphsUntilBigLevelOrAnnex(string format, string[] ancestorFormats) {
        // OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
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
            ILeaf para = ParseParagraph();
            if (para is null)
                continue;
            paragraphs.Add(para);
        }
        return paragraphs;
    }

    private ILeaf ParseParagraph() {
        OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        OpenXmlElement e = elements.ElementAt(i);
        if (IsSkippable(e)) {
            i += 1;
            return null;
        }
        if (e is Paragraph p) {
            i += 1;
            WLine line = new WLine(doc.MainDocumentPart, p);
            IFormattedText number = DOCX.Numbering2.GetFormattedNumber(main, p);
            if (number is null)
                return new WDummyDivision(line);
            else
                return new WNewNumberedParagraph(number, new WLine(line) { IsFirstLineOfNumberedParagraph = true });
        }
        if (e is Table table) {
            i += 1;
            var t = new WTable(doc.MainDocumentPart, table);
            return new WDummyDivision(t);
        }
        throw new System.Exception(e.GetType().ToString());
    }



    /* annexes */

    private readonly Regex annexPattern = new Regex(@"^\s*ANNEX\s+\d+\s*$");

    private bool IsFirstLineOfAnnex(OpenXmlElement e) {
        return annexPattern.IsMatch(e.InnerText);
    }

    private List<IAnnex> Annexes() {
        OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
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
        OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
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
            Add(e, contents);
        }
        return new Annex { Number = number, Contents = contents };
    }

}

}
