using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Models;
using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

/// <summary>
/// EM-specific header splitter. Real-world EMs frequently diverge from the
/// canonical "EXPLANATORY MEMORANDUM TO / &lt;title&gt; / &lt;regulation number&gt;"
/// three-line shape: some omit the "TO" label and open straight with the
/// regulation title, others have regulation numbers in shapes the recogniser
/// can't normalise. This subclass adds two recovery heuristics so those EMs
/// still get a preface instead of falling through to the body parser.
///
/// Both heuristics live here (not in <see cref="BaseHeaderSplitter"/>)
/// because they assume an EM cover-sheet shape: DocType / title /
/// regulation-number / numbered body. IA / EN / TN / CoP / OD have looser
/// cover sheets (with arbitrary metadata between title and body) that the
/// same heuristics misread, so those doc types stay on the strict base.
/// </summary>
class EMHeaderSplitter : BaseHeaderSplitter {

    private EMHeaderSplitter(IEnumerable<IBlock> blocks, LegislativeDocumentConfig config)
        : base(blocks, config) { }

    internal static new List<IBlock> Split(IEnumerable<BlockWithBreak> blocks, LegislativeDocumentConfig config) {
        return Split(blocks.Select(bb => bb.Block), config);
    }

    internal static new List<IBlock> Split(IEnumerable<IBlock> blocks, LegislativeDocumentConfig config) {
        var splitter = new EMHeaderSplitter(blocks, config);
        splitter.Enrich();
        return splitter.Enriched;
    }

    protected override void HandleUnrecognisedStartLine(WLine line) {
        if (HasRegulationNumberShapeWithin(maxNonBlankLookAhead: 4)) {
            DocType2 docType = new DocType2 { Contents = line.Contents };
            WLine newLine = WLine.Make(line, new List<IInline>(1) { docType });
            Enriched.Add(newLine);
            state = State.AfterRegulationTitle;
            return;
        }
        state = State.Fail;
    }

    protected override bool TryPromoteOnBodyMarker(WOldNumberedParagraph numbered) {
        return PromoteLastTitleLineToDocNumber();
    }

}

}
