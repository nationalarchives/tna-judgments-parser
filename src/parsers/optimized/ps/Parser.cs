
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class PressSummaryParser {

    [Obsolete]
    internal static PressSummary Parse(WordprocessingDocument doc) {
        WordDocument preParsed = new PreParser().Parse(doc);
        return Parse(doc, preParsed);
    }
    [Obsolete]
    internal static PressSummary Parse(WordprocessingDocument doc, WordDocument preParsed) {
        return new PressSummaryParser().Parse1(doc, preParsed);
    }

    private int i = 0;

    private PressSummaryParser() {}

    [Obsolete]
    private PressSummary Parse1(WordprocessingDocument doc, WordDocument preParsed) {
        var contents = Enumerable.Concat( preParsed.Header, ParseBody(preParsed.Body) );
        contents = PressSummaryEnricher.Enrich(contents);
        var metadata = new PSMetadata(doc.MainDocumentPart, contents);
        var images = WImage.Get(doc);
        return new PressSummary(doc) { Metadata = metadata, Body = contents, Images = images };
    }

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

        if (children.All(child => child is WOldNumberedParagraph)) {
            List<IBlock> newIntro = new List<IBlock>(1) { intro };
            IEnumerable<IDivision> newChildren = children.Cast<WOldNumberedParagraph>()
                .Select(np => new LeafSubparagraph { Number = np.Number, Contents = new List<IBlock>(1) { WLine.RemoveNumber(np) } });
            IDivision div;
            if (firstNum is null)
                div = new GroupOfParagraphs { Intro = newIntro, Children = newChildren };
            else
                div = new BranchParagraph { Number = firstNum, Intro = newIntro, Children = newChildren };
            return new DivWrapper { Division = div };
        }

        if (children.All(child => child is DivWrapper)) {
            List<IBlock> newIntro = new List<IBlock>(1) { intro };
            IEnumerable<IDivision> newChildren = children.Cast<DivWrapper>()
                .Select(wrapper => wrapper.Division)
                .Select(child => {
                    if (child is GroupOfParagraphs gop)
                        return new BranchSubparagraph { Intro = gop.Intro, Children = gop.Children };
                    if (child is BranchParagraph bp)
                        return BranchSubparagraph.Demote(bp);
                    return child;
                });
            IDivision div;
            if (firstNum is null)
                div = new GroupOfParagraphs { Intro = newIntro, Children = newChildren };
            else
                div = new BranchParagraph { Number = firstNum, Intro = newIntro, Children = newChildren };
            return new DivWrapper { Division = div };
        }

        i = save;
        return first;
    }

}

}
