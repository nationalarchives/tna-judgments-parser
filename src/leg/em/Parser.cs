
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class Parser : CaseLaw.OptimizedParser {

    internal static IDocument Parse(WordprocessingDocument doc) {
        CaseLaw.WordDocument preParsed = new CaseLaw.PreParser().Parse(doc);
        Parser instance = new Parser(doc, preParsed);
        return instance.Parse();
    }

    private static ILogger logger = Logging.Factory.CreateLogger<Parser>();

    private Parser(WordprocessingDocument doc, WordDocument preParsed) : base(doc, preParsed, null, null) { }

    private IDocument Parse() {

        List<IBlock> header = Header();
        header.InsertRange(0, PreParsed.Header);

        List<IDivision> body = Body2();

        IEnumerable<IImage> images = WImage.Get(doc);
        DocumentMetadata metadata = Metadata.Make(header, doc);
        return new DividedDocument {
            Header = header,
            Body = body,
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
            IDivision div = ParseDivsion();
            body.Add(div);
        }
        return body;
    }

    private IDivision ParseDivsion() {
        int save = i;
        IDivision div = ParseSection();
        if (div is not null)
            return div;
        i = save;
        return ParseParagraph();
    }

    private static bool IsSectionHeading(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line.Style != "EMSectionTitle")
            return false;
        return true;
    }

    private IDivision ParseSection() {
        logger.LogTrace("parsing element " + i);
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
        return new BigLevel { Number = num, Heading = heading, Children = children };
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
        if (IsSectionHeading(block))
            return null;
        return ParseParagraph();
    }

}

}
