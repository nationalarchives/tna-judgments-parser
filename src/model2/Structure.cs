
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

internal class BigLevel : IBranch {

    public string Name => null;

    public IFormattedText Number { get; internal set; }

    public ILine Heading { get; internal set; }

    public IEnumerable<IBlock> Intro => null;

    public IEnumerable<IDivision> Children { get; internal set; }

    public IEnumerable<IBlock> WrapUp => null;

}

internal class CrossHeading : IBranch {

    public string Name => null;

    public IFormattedText Number => null;

    public ILine Heading { get; internal set; }

    public IEnumerable<IBlock> Intro => null;

    public IEnumerable<IDivision> Children { get; internal set; }

    public IEnumerable<IBlock> WrapUp => null;

}

internal class GroupOfParagraphs : IBranch {

    public string Name => null;

    public IFormattedText Number => null;

    public ILine Heading => null;

    public IEnumerable<IBlock> Intro { get; internal init; }

    public IEnumerable<IDivision> Children { get; internal set; }

    public IEnumerable<IBlock> WrapUp => null;

}

internal class WNewNumberedParagraph : ILeaf {

    public string Name => "paragraph";

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

    public IFormattedText Number { get; internal set; }

    public ILine Heading => null;

    public IEnumerable<IBlock> Contents { get; internal set; }

}

internal class GroupOfUnnumberedParagraphs : ILeaf {

    public string Name => null;

    public IFormattedText Number => null;

    public ILine Heading { get; private init; }

    public IEnumerable<IBlock> Contents { get; private init; }

    internal GroupOfUnnumberedParagraphs(ILine heading, IEnumerable<IBlock> contents) {
        Heading = heading;
        Contents = contents;
    }

}

internal class WDummyDivision : ILeaf {

    internal WDummyDivision(IEnumerable<IBlock> blocks) {
        Contents = blocks;
    }
    internal WDummyDivision(IBlock block) {
        Contents = new List<IBlock>(1) { block };
    }

    public string Name => null;

    public IFormattedText Number => null;

    public ILine Heading => null;

    public IEnumerable<IBlock> Contents { get; internal set; }

}

internal class WOldNumberedParagraph : WLine, IOldNumberedParagraph {

    public readonly int TabsRemoved;

    internal WOldNumberedParagraph(DOCX.NumberInfo info, MainDocumentPart main, Paragraph paragraph) : base(main, paragraph) {
        Number = new DOCX.WNumber(main, info, paragraph);
        IsFirstLineOfNumberedParagraph = true;
    }
    internal WOldNumberedParagraph(IFormattedText number, WLine proto) : base(proto, proto.Contents) {
        Number = number;
        IsFirstLineOfNumberedParagraph = proto.IsFirstLineOfNumberedParagraph;
    }
    // this probably should be deprecated
    internal WOldNumberedParagraph(WOldNumberedParagraph proto, IEnumerable<IInline> contents) : base(proto, contents) {
        Number = proto.Number;
        IsFirstLineOfNumberedParagraph = proto.IsFirstLineOfNumberedParagraph;
    }
    // here proto is used only for ParagraphProperties and a link to the MainDocumentPart
    internal WOldNumberedParagraph(IFormattedText number, IEnumerable<IInline> contents, WLine proto, int tabsRemoved = 0) : base(proto, contents) {
        Number = number;
        IsFirstLineOfNumberedParagraph = true;
        TabsRemoved = tabsRemoved;
    }

    /// <summary>
    /// Override to give more useful information in the debugger
    /// </summary>
    public override string ToString()
    {
        return TextContent ?? base.ToString();
    }

    public IFormattedText Number { get; }

    private string _textContent;
    override public string TextContent {
        get {
            if (_textContent is null)
                _textContent = Number.Text + " " + base.TextContent;
            return _textContent;
        }
    }

    internal float GetEffectiveFirstLineIndent() {
        float left = LeftIndentWithNumber ?? 0f;
        var first = FirstLineIndentWithNumber ?? 0f;
        first = first > 0 ? left : left + first;
        for (int i = 0; i < TabsRemoved; i++)
        {
            float tab = GetFirstTabAfter(first) ?? GetFirstDefaultTabAfter(first);
            if (first < left && tab > left)
                first = left;
            else
                first = tab;
        }
        return first;
    }

    public bool IsEmpty()
    {
        if (!IInline.IsEmpty(Number))
            return false;
        if (!this.Contents.Any())
            return true;
        return this.Contents.All(IInline.IsEmpty);
    }
}

    internal class WTableOfContents : ITableOfContents {

    public string Name => null;

    public IFormattedText Number => null;

    public ILine Heading => null;

    public IEnumerable<ILine> Contents { get; internal set; }

    internal WTableOfContents(IEnumerable<ILine> blocks) {
        Contents = blocks;
    }

}

internal class DivWrapper : IDivWrapper {

    public IDivision Division { get; internal init; }

}

}
