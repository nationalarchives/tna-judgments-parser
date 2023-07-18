
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;
using JudgmentsP = UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation {

// delegate IDocument Parser(WordprocessingDocument document);

[Obsolete]
class GenericDocumentParser : JudgmentsP.AbstractParser {

    internal static IDocument Parse(WordprocessingDocument doc) {
        return new GenericDocumentParser(doc).Parse();
    }

    private GenericDocumentParser(WordprocessingDocument doc) : base(doc) { }

    private IDocument Parse() {
        string name = "Explanatory Memorandum";
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");
        IEnumerable<IImage> images = JudgmentsP.WImage.Get(doc);
        int save = i;
        List<IBlock> header = Header();
        if (header is null)
            i = save;
        
        header = EnrichHeader(header);

        string shortUriComponent = null;
        if (header is not null) {
            Model.IDocNumber docNum = Util.Descendants<Model.IDocNumber>(header).FirstOrDefault();
            if (docNum is not null)
                shortUriComponent = ExtractShortUriComponent(docNum);
        }
        
        DocumentMetadata metadata = new DocumentMetadata {
            Name = name,
            ShortUriComponent = shortUriComponent,
            CSS = css
        };

        // List<IDivision> divisions = BigLevels();
        // if (i == elements.Count)
        //     return new DividedDocument { Body = divisions };
        // i = save;
        List<IDivision> divisions = ParagraphsUntil(e => false);
        if (i == elements.Count) {
            return new DividedDocument { Header = header, Body = divisions, Images = images, Meta = metadata };
        }
        // i = save;
        IEnumerable<IBlock> blocks = JudgmentsP.Blocks.ParseBlocks(main, elements);
        // List<IBlock> blocks = AllParagraphs();
        // if (i != elements.Count)
        //     throw new Exception("parsing did not complete");
        return new UndividedDocument { Header = header, Body = blocks, Images = images, Meta = metadata };
    }

    protected override List<IBlock> Header() {
        if (elements.Count < 3)
            return null;
        IEnumerable<IBlock> header = JudgmentsP.Blocks.ParseBlocks(main, elements.Take(3));
        // if (header.Count() < 3)
        //     return null;
        if (header.Last() is not JudgmentsP.WLine line)
            return null;
        string text = line.NormalizedContent;
        if (!Regex.IsMatch(text, @"^\d{4} No\. \d+$"))
            return null;
        i = 3;
        return header.ToList();
    }

    protected List<IBlock> EnrichHeader(List<IBlock> header) {
        if (header is null)
            return header;
        IEnumerable<IBlock> enriched =  new Enrich.DocType().Enrich(header);
        enriched =  new Enrich.DocNumber().Enrich(enriched);
        return enriched.ToList();
    }

    private string ExtractShortUriComponent(Model.IDocNumber docNumber) {
        string text;
        if (docNumber is Model.IDocNumber1 docNum1)
            text = docNum1.Text;
        else if (docNumber is Model.DocNumber2 docNum2)
            text = IInline.ToString(docNum2.Contents);
        else
            throw new Exception();
        Match match = Regex.Match(text, @"^(\d{4}) No\. (\d+)$");
        if (!match.Success)
            return null;
        return "uksiem/" + match.Groups[1].Value + "/" + match.Groups[2].Value;
    }

    protected override List<IDecision> Body() {
        throw new System.NotImplementedException();
    }

    protected override IDivision ParseParagraph() {
        // IDivision div = base.ParseParagraph();
        // if (div is not JudgmentsP.WNewNumberedParagraph para1)
        //     return div;
        // if (div.Number is null)
        //     return div;
        // if (div.Number is not DOCX.WNumber num)
        //     return div;
        // if (div.Heading is not null)
        //     return div;
        // if (para1.Contents.Count() != 1)
        //     return div;
        // if (para1.Contents.First() is not JudgmentsP.WLine heading)
        //     return div;
        // if (!Regex.IsMatch(div.Number.Text, @"^\d+\.?$"))
        //     return div;
        // // List<Model.Subparagraph> subparagraphs = ParseSubparagraphs();
        // List<Model.Subparagraph> subparagraphs = new List<Model.Subparagraph>();
        // Model.Subparagraph next = ParseSubparagraph();
        // while (next is not null) {
        //     subparagraphs.Add(next);
        //     next = ParseSubparagraph();
        // }
        // i -= 1;
        // if (!subparagraphs.Any())
        //     return div;
        // num.IsNumberOfParagraphWithHeading = true;
        // heading.IsHeadingOfNumberedParagraph = true;
        // heading.IsFirstLineOfNumberedParagraph = false;
        // return new Model.BranchParagraph {
        //     Number = num,
        //     Heading = heading,
        //     Children = subparagraphs
        // };
        return null;
    }

    // private List<Model.Subparagraph> ParseSubparagraphs() {
    //     List<Model.Subparagraph> subparagraphs = new List<Model.Subparagraph>();
    //     Model.Subparagraph next = ParseSubparagraph();
    //     while (next is not null) {
    //         subparagraphs.Add(next);
    //         next = ParseSubparagraph();
    //     }
    //     i -= 1;
    //     return subparagraphs;
    // }
    private Model.Subparagraph ParseSubparagraph() {
        if (i == elements.Count)
            return null;
        IDivision div = base.ParseParagraph();
        while (div is null && i < elements.Count)
            div = base.ParseParagraph();
        if (div is JudgmentsP.WDummyDivision ddiv) {
            if (ddiv.Contents.Count() != 1)
                return null;
            if (elements.ElementAt(i - 1) is not DocumentFormat.OpenXml.Wordprocessing.Paragraph p)
                return null;
            string format =  @"^(\d+\.\d+\.?) ";
            JudgmentsP.WText num3 = base.GetNumberFromParagraph(p, format);
            if (num3 is not null) {
                JudgmentsP.WLine removed = base.RemoveNumberFromParagraph(p, format);
                removed.IsFirstLineOfNumberedParagraph = true;
                div = new JudgmentsP.WNewNumberedParagraph(num3, removed);
            }
        }
        if (div is not JudgmentsP.WNewNumberedParagraph para1)
            return null;
        if (div.Number is null)
            return null;
        if (div.Heading is not null)
            return null;
        if (!Regex.IsMatch(div.Number.Text, @"^\d+\.\d+\.?$"))
            return null;
        return new Model.Subparagraph {
            Number = div.Number,
            Contents = para1.Contents
        };
    }

}

}
