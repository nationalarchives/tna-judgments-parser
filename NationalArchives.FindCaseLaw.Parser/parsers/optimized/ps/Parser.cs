
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using PS = UK.Gov.NationalArchives.CaseLaw.PressSummaries;

namespace UK.Gov.NationalArchives.CaseLaw.PressSummaries {

class Parser : OptimizedParser {

    internal static PressSummary Parse(WordprocessingDocument doc, IOutsideMetadata meta) {
        WordDocument preParsed = new PreParser().Parse(doc);
        return Parse(preParsed, meta);
    }
    internal static PressSummary Parse(WordDocument preParsed, IOutsideMetadata meta) {
        return new Parser(preParsed, meta).Parse();
    }

    private Parser(WordDocument preParsed, IOutsideMetadata meta) : base(preParsed.Docx, preParsed, meta, null) { }

    private PressSummary Parse() {
        List<IBlock> header = Header();
        header.InsertRange(0, PreParsed.Header);
        List<IDivision> body = Body2();
        var metadata = new Metadata(PreParsed.Docx.MainDocumentPart, header);
        var images = WImage.Get(PreParsed.Docx);
        return new PressSummary() { InternalMetadata = metadata, Preface = header, Body = body, Images = images, ExternalMetadata = this.meta };
    }

    protected override List<IBlock> Header() {
        List<IBlock> header = PS.Header.Split(PreParsed.Body);
        i = header.Count;
        return header;
    }

    protected override List<IDecision> Body() {
        throw new System.NotImplementedException();
    }

    private List<IDivision> Body2() {
        List<IDivision> parsed = new List<IDivision>();
        while (i < PreParsed.Body.Count) {
            int save = i;
            IDivision xhead = ParseCrossHeading();
            if (xhead is not null) {
                parsed.Add(xhead);
                continue;
            }
            i = save;  // 00053_ukait_2008_sb_pakistan
            IDivision para = ParseParagraph();
            parsed.Add(para);
        }
        return parsed;
    }

    /* cross headings */

    private bool IsHeading(WLine line) {
        if (i < 4)
            return false;
        if (line is WOldNumberedParagraph)
            return false;
        if (line.NormalizedContent == "PRESS SUMMARY")
            return false;
        if (line.Style == "SectionHeading")
            return true;
        if (!line.Contents.All(i => i is IFormattedText ft && (ft.Bold ?? false))) // IsAllBold
            return false;
        if (!Regex.IsMatch(line.NormalizedContent, "^[A-Z ]+$"))  // IsAllCaps
            return false;
        return true;
    }

    private IDivision ParseCrossHeading() {
        IBlock block1 = PreParsed.Body[i].Block;
        if (block1 is not WLine line1)
            return null;
        if (!IsHeading(line1))
            return null;
        i += 1;

        List<IDivision> children = new List<IDivision>();
        while (i < PreParsed.Body.Count) {
            IBlock block = PreParsed.Body[i].Block;
            if (block is not WLine line) {
                i += 1;
                WDummyDivision dd = new WDummyDivision(new List<IBlock>(1) { block });
                children.Add(dd);
                continue;
            }
            if (IsHeading(line))
                break;
            var p = ParseParagraphAndSubparagraphs(line, true); // true means unnumbered paragraph won't get promoted to a cross-heading
            children.Add(p);
        }
        if (!children.Any())
            return null;

        if (children.All(child => child is WDummyDivision)) {
            IEnumerable<IBlock> contents = children.Cast<WDummyDivision>().SelectMany(dd => dd.Contents);
            return new GroupOfUnnumberedParagraphs(line1, contents);
        }
        return new CrossHeading{ Heading = line1, Children = children };
    }

}

}
