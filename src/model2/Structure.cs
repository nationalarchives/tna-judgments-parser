
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

internal class BigLevel : IBranch {

    public IFormattedText Number { get; internal set; }

    public ILine Heading { get; internal set; }

    public IEnumerable<IDivision> Children { get; internal set; }

}

internal class CrossHeading : IBranch {

    public IFormattedText Number => null;

    public ILine Heading { get; internal set; }

    public IEnumerable<IDivision> Children { get; internal set; }

}

internal class GroupOfParagraphs : IBranch {

    public IFormattedText Number => null;

    public ILine Heading => null;

    public IEnumerable<IDivision> Children { get; internal set; }

}

internal class WNewNumberedParagraph : ILeaf {

    internal WNewNumberedParagraph(IFormattedText number, IEnumerable<IBlock> blocks) {
        Number = number;
        Contents = blocks;
    }
    internal WNewNumberedParagraph(IFormattedText number, IBlock block) {
        // if (block is WLine line)
        //     line.FirstLineIndent = null;
        Number = number;
        Contents = new List<IBlock>(1) { block };
    }
    // internal WNewNumberedParagraph(string number, RunProperties numProps, IEnumerable<IBlock> blocks) {
    //     Number = new WText(number, numProps);
    //     Contents = blocks;
    // }
    // internal WNewNumberedParagraph(string number, RunProperties numProps, IBlock block) {
    //     Number = new WText(number, numProps);
    //     Contents = new List<IBlock>(1) { block };
    // }

    public IFormattedText Number { get; internal set; }

    public ILine Heading => null;

    public IEnumerable<IBlock> Contents { get; internal set; }

}

internal class WDummyDivision : ILeaf {

    internal WDummyDivision(IEnumerable<IBlock> blocks) {
        Contents = blocks;
    }
    internal WDummyDivision(IBlock block) {
        Contents = new List<IBlock>(1) { block };
    }

    public IFormattedText Number => null;

    public ILine Heading => null;

    public IEnumerable<IBlock> Contents { get; internal set; }

}

internal class WOldNumberedParagraph : WLine, IOldNumberedParagraph {

    internal WOldNumberedParagraph(DOCX.NumberInfo info, MainDocumentPart main, Paragraph paragraph) : base(main, paragraph) {
        Number = new DOCX.WNumber(main, info, paragraph);
        IsFirstLineOfNumberedParagraph = true;
    }
    internal WOldNumberedParagraph(IFormattedText number, WLine proto) : base(proto, proto.Contents) {
        Number = number;
        IsFirstLineOfNumberedParagraph = proto.IsFirstLineOfNumberedParagraph;
    }
    internal WOldNumberedParagraph(WOldNumberedParagraph proto, IEnumerable<IInline> contents) : base(proto, contents) {
        Number = proto.Number;
        IsFirstLineOfNumberedParagraph = proto.IsFirstLineOfNumberedParagraph;
    }

    public IFormattedText Number { get; }

}

internal class WTableOfContents : ITableOfContents {

    public IFormattedText Number => null;

    public ILine Heading => null;

    public IEnumerable<ILine> Contents { get; internal set; }

    internal WTableOfContents(IEnumerable<ILine> blocks) {
        Contents = blocks;
    }

}

}
