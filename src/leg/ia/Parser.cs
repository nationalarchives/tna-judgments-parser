
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.ImpactAssessments {

partial class Parser : CaseLaw.OptimizedParser {

    internal static IDocument Parse(WordprocessingDocument doc) {
        CaseLaw.WordDocument preParsed = new CaseLaw.PreParser().Parse(doc);
        Parser instance = new Parser(doc, preParsed);
        return instance.Parse();
    }

    private static ILogger logger = Logging.Factory.CreateLogger<Parser>();

    private Parser(WordprocessingDocument doc, CaseLaw.WordDocument preParsed) : base(doc, preParsed, null, null) { }

    private IDocument Parse() {

        List<IBlock> header = Header();
        header.InsertRange(0, PreParsed.Header);

        List<IDivision> body = Body2();
        IEnumerable<IAnnex> annexes = Annexes();
        if (i != PreParsed.Body.Count)
            logger.LogCritical("parsing did not complete: {}", i);

        IEnumerable<IImage> images = WImage.Get(doc);
        DocumentMetadata metadata = Metadata.Make(header, doc);
        logger.LogInformation("the type is {}", metadata.Name);
        logger.LogInformation("the uri is {}", metadata.ShortUriComponent);
        return new DividedDocument {
            Header = header,
            Body = body,
            Annexes = annexes,
            Images = images,
            Meta = metadata 
        };
    }

    protected override List<IBlock> Header() {
        List<IBlock> header = HeaderSplitter.Split(PreParsed.Body);
        i = header.Count;
        return header;
    }

    protected override List<IDecision> Body() {
        throw new System.NotImplementedException();
    }

    private List<IDivision> Body2() {       
        List<IDivision> body = new List<IDivision>();
        while (i < PreParsed.Body.Count()) {
            if (NextBlockIsAnnexHeading())
                break;
            IDivision div = ParseDivsion();
            body.Add(div);
        }
        return body;
    }

    private IDivision ParseDivsion() {
        int save = i;
        IDivision div = ParseCrossHeading();
        if (div is not null)
            return div;
        i = save;
        div = ParseSection();
        if (div is not null)
            return div;
        i = save;
        return ParseParagraph();
    }

    /* cross-headings */

    private bool IsCrossHeading(WLine line) {
        if (i < 3)
            return false;
        if (line is WOldNumberedParagraph)
            return false;
        float left = line.LeftIndentInches ?? 0f;
        if (left != 0)
            return false;
        float first = line.FirstLineIndentInches ?? 0f;
        if (first != 0)
            return false;
        // if (!line.IsFlushLeft())
        //     return false;
        if (!line.Contents.All(i => i is IFormattedText ft && (ft.Bold ?? false))) // IsAllBold
            return false;
        return true;
    }

    private IDivision ParseCrossHeading() {
        IBlock block1 = PreParsed.Body[i].Block;
        if (block1 is not WLine line1)
            return null;
        if (!IsCrossHeading(line1))
            return null;
        i += 1;

        List<Model.Section> children = new ();
        while (i < PreParsed.Body.Count) {
            Model.Section child = ParseSection();
            if (child is null)
                break;
            children.Add(child);
        }
        if (!children.Any())
            return null;

        return new CrossHeading{ Heading = line1, Children = children };
    }

    /* sections */

    [GeneratedRegex("^\\d+\\.$")]
    private static partial Regex SectionNumberRegex();

    private static bool IsSectionHeading(IBlock block) {
        if (block is not WOldNumberedParagraph np)
            return false;
        if (np.Style != "IASectionTitle")
            return false;
        if (!SectionNumberRegex().IsMatch(np.Number.Text))
            return false;
        return true;
    }

    private Model.Section ParseSection() {
        logger.LogTrace("parsing element {}", i);
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (!IsSectionHeading(block))
            return null;
        WLine line = block as WLine;
        i += 1;

        IFormattedText num;
        ILine heading;
        if (line is WOldNumberedParagraph np) {
            num = np.Number;
            heading = WLine.RemoveNumber(np);
        } else {
            num = null;
            heading = line;
        }
        List<IDivision> children = ParseSectionChildren();
        return new Model.Section { Number = num, Heading = heading, Children = children };
    }

    private List<IDivision> ParseSectionChildren() {
        List<IDivision> children = new List<IDivision>();
        IDivision child = ParseSectionChild();
        while (child is not null) {
            children.Add(child);
            child = ParseSectionChild();
        }
        return children;
    }

    private IDivision ParseSectionChild() {
        if (i == PreParsed.Body.Count)
            return null;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (IsFirstLineOfAnnex(block))
            return null;
        if (IsSectionHeading(block))
            return null;
        if (IsLevel1Subheading(block))
            return ParseLevel1Subheading(block as WLine);
        return ParseParagraph();
    }

    /* sub-headings */

    private static bool IsLevel1Subheading(IBlock block) {
        if (block is not WLine line)
            return false;
        if (block is WOldNumberedParagraph)
            return false;
        if (line.Style != "IALevel1Subheading")
            return false;
        return true;
    }

    private Model.Subheading ParseLevel1Subheading(WLine subhead) {
        i += 1;
        List<IDivision> children = ParseLevel1SubheadingChildren();
        return new Model.Subheading { Heading = subhead, Children = children };
    }

    private List<IDivision> ParseLevel1SubheadingChildren() {
        List<IDivision> children = new();
        IDivision child = ParseLevel1SubheadingChild();
        while (child is not null) {
            children.Add(child);
            child = ParseLevel1SubheadingChild();
        }
        return children;
    }

    private IDivision ParseLevel1SubheadingChild() {
        if (i == PreParsed.Body.Count)
            return null;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (IsSectionHeading(block))
            return null;
        if (IsLevel1Subheading(block))
            return null;
        if (IsLevel2Subheading(block))
            return ParseLevel2Subheading(block as WLine);
        return ParseParagraph();
    }

    private static bool IsLevel2Subheading(IBlock block) {
        if (block is not WLine line)
            return false;
        if (block is WOldNumberedParagraph)
            return false;
        if (line.Style != "IALevel2Subheading")
            return false;
        return true;
    }

    private Model.Subheading ParseLevel2Subheading(WLine subhead) {
        i += 1;
        List<IDivision> children = ParseLevel2SubheadingChildren();
        return new Model.Subheading { Heading = subhead, Children = children };
    }

    private List<IDivision> ParseLevel2SubheadingChildren() {
        List<IDivision> children = new();
        IDivision child = ParseLevel2SubheadingChild();
        while (child is not null) {
            children.Add(child);
            child = ParseLevel2SubheadingChild();
        }
        return children;
    }

    private IDivision ParseLevel2SubheadingChild() {
        if (i == PreParsed.Body.Count)
            return null;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        if (IsSectionHeading(block))
            return null;
        if (IsLevel1Subheading(block))
            return null;
        if (IsLevel2Subheading(block))
            return null;
        return ParseParagraph();
    }

    /* paragraphs */

    override protected bool CannotBeSubparagraph(WLine line) {
        if (IsFirstLineOfAnnex(line))
            return true;
        if (IsSectionHeading(line))
            return true;
        if (IsLevel1Subheading(line))
            return true;
        if (IsLevel2Subheading(line))
            return true;
        return false;
    }

    /* annexes */

    private bool NextBlockIsAnnexHeading() {
        if (i == PreParsed.Body.Count)
            return false;
        IBlock block = PreParsed.Body.ElementAt(i).Block;
        return IsFirstLineOfAnnex(block);
    }

}

}
