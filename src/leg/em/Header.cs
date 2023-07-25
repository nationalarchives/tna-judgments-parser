
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class HeaderSplitter {

    internal static List<IBlock> Split(IEnumerable<BlockWithBreak> blocks) {
        return Split(blocks.Select(bb => bb.Block));
    }
    internal static List<IBlock> Split(IEnumerable<IBlock> blocks) {
        var enricher = new HeaderSplitter(blocks);
        enricher.Enrich();
        return enricher.Enriched;
    }

    private enum State {
        Start,
        AfterDocType,
        AfterRegulationTitle,
        Done,
        Fail
    };

    private State state = State.Start;

    private readonly IEnumerable<IBlock> Blocks;

    private readonly List<IBlock> Enriched = new List<IBlock>(3);

    private HeaderSplitter(IEnumerable<IBlock> blocks) {
        Blocks = blocks;
    }

    private void Enrich() {
        foreach (var block in Blocks)
            switch (state) {
                case State.Start:
                    Start(block);
                    break;
                case State.AfterDocType:
                    AfterDocType(block);
                    break;
                case State.AfterRegulationTitle:
                    AfterRegulationTitle(block);
                    break;
                case State.Done:
                    return;
                case State.Fail:
                    Enriched.Clear();
                    return;
                default:
                    throw new System.Exception();
            }
    }

    private void Start(IBlock block) {
        if (block is not WLine line) {
            state = State.Fail;
            return;
        }
        if (line is WOldNumberedParagraph) {
            state = State.Fail;
            return;
        }
        string[] titles = { "Explanatory Memorandum To", "Policy Note" };
        bool isTitle = titles.Any(title => title.Equals(line.NormalizedContent, System.StringComparison.InvariantCultureIgnoreCase));
        if (!isTitle) {
            state = State.Fail;
            return;
        }
        // WDocType docType = new WDocType(line.Contents);
        // WLine newLine = WLine.Make(line, new List<IInline>(1) { docType });
        // Enriched.Add(newLine);
        Enriched.Add(line);
        state = State.AfterDocType;
    }

    private void AfterDocType(IBlock block) {
        if (block is not WLine line) {
            state = State.Fail;
            return;
        }
        Enriched.Add(line);
        state = State.AfterRegulationTitle;
    }

    private void AfterRegulationTitle(IBlock block) {
        if (block is not WLine line) {
            state = State.Fail;
            return;
        }
        if (!RegulationNumber.Is(line.NormalizedContent)) {
            state = State.Fail;
            return;
        }
        Enriched.Add(line);
        state = State.Done;
    }

}

}
