
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class PressSummaryParser {

    internal static PressSummary Parse(WordprocessingDocument doc) {
        return new PressSummaryParser().Parse1(doc);
    }

    private PressSummary Parse1(WordprocessingDocument doc) {
        WordDocument preParsed = new PreParser().Parse(doc);
        return Parse(doc, preParsed);
    }

    private PressSummary Parse(WordprocessingDocument doc, WordDocument preParsed) {
        var contents = Enumerable.Concat( preParsed.Header, ParseBody(preParsed.Body) );
        contents = PressSummaryEnricher.Enrich(contents);
        var metadata = new PSMetadata(doc.MainDocumentPart, contents);
        var images = WImage.Get(doc);
        return new PressSummary(doc) { Metadata = metadata, Body = contents, Images = images };
    }

    private int i = 0;

    private IList<IBlock> ParseBody(IList<BlockWithBreak> unparsed) {
        List<IBlock> parsed = new List<IBlock>();
        while (i < unparsed.Count) {
            IBlock parsed1 = ParseWithSubparagraphs(unparsed);
            parsed.Add(parsed1);
        }
        return parsed;
    }

    private IBlock ParseWithSubparagraphs(IList<BlockWithBreak> unparsed) {
        IBlock firstBlock = unparsed[i].Block;
        i += 1;
        if (firstBlock is not WLine first)
            return firstBlock;
        float firstIndent = OptimizedParser.GetEffectiveIndent(first);

        int save = i;
        List<IBlock> children = new List<IBlock>();
        while (i < unparsed.Count) {
            IBlock nextBlock = unparsed[i].Block;
            if (nextBlock is not WLine next)
                break;
            float nextIndent = OptimizedParser.GetEffectiveIndent(next);
            if (nextIndent - OptimizedParser.MarginOfError <= firstIndent)
                break;
            nextBlock = ParseWithSubparagraphs(unparsed);
            children.Add(nextBlock);
        }

        if (!children.Any())
            return first;

        IFormattedText firstNum;
        WLine intro;
        if (first is WOldNumberedParagraph np1) {
            firstNum = np1.Number;
            intro = WLine.RemoveNumber(np1);
        } else {
            firstNum = null;
            intro = first;
        }

        if (!children.Any(child => child is BlockWrapper)) {
        }
        if (children.All(child => child is BlockWrapper)) {

        }
        i = save;
        return first;
    }

}

}
