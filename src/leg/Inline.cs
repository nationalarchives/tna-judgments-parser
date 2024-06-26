
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Model {

interface IDocType { }

interface IDocType1 : IDocType, Judgments.IFormattedText { }

interface IDocType2 : IDocType, Judgments.IInlineContainer { }

interface IDocNumber { }

interface IDocNumber1 : IDocNumber, Judgments.IFormattedText { }

interface IDocNumber2 : IDocNumber, Judgments.IInlineContainer { }


internal class DocType1 : Judgments.Parse.WText {

    public DocType1(Judgments.Parse.WText text) : base(text.Text, text.properties) { }

}

internal class DocNumber1 : Judgments.Parse.WText {

    public DocNumber1(Judgments.Parse.WText text) : base(text.Text, text.properties) { }

}

abstract class InlineContainer : Judgments.IInlineContainer {

    public IEnumerable<IInline> Contents { get; init; }

}

internal class DocType2 : InlineContainer, IDocType2 { }

internal class DocNumber2 : InlineContainer, IDocNumber2 { }

}
