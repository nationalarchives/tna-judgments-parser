
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Models {

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

// IA-specific semantic inline elements
interface IDocTitle : Judgments.IInlineContainer { }
interface IDocStage : Judgments.IInlineContainer { }
interface IDocDate : Judgments.IInlineContainer { }
interface IDocDepartment : Judgments.IInlineContainer { }
interface ILeadDepartment : Judgments.IInlineContainer { }
interface IOtherDepartments : Judgments.IInlineContainer { }

internal class DocTitle : InlineContainer, IDocTitle { }
internal class DocStage : InlineContainer, IDocStage { }
internal class DocDate : InlineContainer, IDocDate { }
internal class DocDepartment : InlineContainer, IDocDepartment { }
internal class LeadDepartment : InlineContainer, ILeadDepartment { }
internal class OtherDepartments : InlineContainer, IOtherDepartments { }

}
