using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.CaseLaw.Model;

using CSS2 = UK.Gov.Legislation.Judgments.CSS;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso;

internal abstract class Builder
{
    private static readonly ILogger logger = Logging.Factory.CreateLogger<Builder>();

    public const string ns = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
    public const string AknNamespace = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    protected abstract string UKNS { get; }

    protected readonly XmlDocument doc = new();

    protected XmlElement CreateElement(string name)
    {
        return doc.CreateElement(name, ns);
    }

    protected XmlElement CreateAndAppend(string name, XmlNode parent)
    {
        var e = CreateElement(name);
        parent.AppendChild(e);
        return e;
    }

    protected void Build1(IJudgment judgment)
    {
        var akomaNtoso = CreateAndAppend("akomaNtoso", doc);
        akomaNtoso.SetAttribute("xmlns:uk", Metadata.ukns);

        var main = CreateAndAppend("judgment", akomaNtoso);
        main.SetAttribute("name", Enum.GetName(typeof(JudgmentType), judgment.Type).ToLower());

        var meta = Metadata.make(doc, judgment, judgment.Metadata, true);
        main.AppendChild(meta);

        AddCoverPage(main, judgment);
        AddHeader(main, judgment);
        AddBody(main, judgment);
        AddConclusions(main, judgment.Conclusions);
        AddAnnexesAndInternalAttachments(main, judgment);
    }

    private void AddCoverPage(XmlElement main, IJudgment judgment)
    {
        if (judgment.CoverPage is null || !judgment.CoverPage.Any())
        {
            return;
        }

        var container = doc.CreateElement("coverPage", ns);
        main.AppendChild(container);
        blocks(container, judgment.CoverPage);
    }

    private void AddHeader(XmlElement main, IJudgment judgment)
    {
        var header = doc.CreateElement("header", ns);
        main.AppendChild(header);
        blocks(header, judgment.Header);
    }

    private void AddBody(XmlElement main, IJudgment judgment)
    {
        var body = doc.CreateElement("judgmentBody", ns);
        main.AppendChild(body);
        foreach (var decision in judgment.Body)
        {
            AddDecision(body, decision);
        }
    }

    private void AddConclusions(XmlElement main, IEnumerable<IBlock> conclusions)
    {
        if (conclusions is null || !conclusions.Any())
        {
            return;
        }

        var container = doc.CreateElement("conclusions", ns);
        main.AppendChild(container);
        blocks(container, conclusions);
    }

    private void AddAnnexesAndInternalAttachments(XmlElement main, IJudgment judgment)
    {
        var annexes = judgment.Annexes ?? Enumerable.Empty<IAnnex>();
        if (!annexes.Any() && !judgment.InternalAttachments.Any())
        {
            return;
        }

        var attachments = doc.CreateElement("attachments", ns);
        main.AppendChild(attachments);
        foreach (var annex in annexes.Select((value, i) => new { i, value }))
        {
            AddAnnex(attachments, judgment, annex.value, annex.i + 1);
        }

        foreach (var attach in judgment.InternalAttachments)
        {
            AddInternalAttachment(attachments, judgment, attach);
        }
    }

    private void AddAnnex(XmlElement attachments, IJudgment judgment, IAnnex annex, int n)
    {
        var attachment = doc.CreateElement("attachment", ns);
        attachments.AppendChild(attachment);
        var main = doc.CreateElement("doc", ns);
        main.SetAttribute("name", "annex");
        attachment.AppendChild(main);

        var metadata = new AttachmentMetadata(AttachmentType.Annex, judgment.Metadata, n);
        var meta = Metadata.make(doc, null, metadata, false);
        main.AppendChild(meta);

        var body = doc.CreateElement("mainBody", ns);
        main.AppendChild(body);
        p(body, annex.Number);
        blocks(body, annex.Contents);
    }

    private void AddInternalAttachment(XmlElement attachments, IJudgment judgment, IInternalAttachment attach)
    {
        var attachment = doc.CreateElement("attachment", ns);
        attachments.AppendChild(attachment);
        var main = doc.CreateElement("doc", ns);
        main.SetAttribute("name", Enum.GetName(typeof(AttachmentType), attach.Type).ToLower());
        attachment.AppendChild(main);

        var metadata =
            new AttachmentMetadata(attach.Type, judgment.Metadata, attach.Number) { Styles = attach.CSSStyles() };
        var meta = Metadata.make(doc, null, metadata, false);
        main.AppendChild(meta);

        var body = doc.CreateElement("mainBody", ns);
        main.AppendChild(body);
        blocks(body, attach.Contents);
    }

    /* structure */

    private void AddDecision(XmlElement body, IDecision model)
    {
        var decision = doc.CreateElement("decision", ns);
        body.AppendChild(decision);
        if (model.Author is not null)
        {
            var wrapper = doc.CreateElement("level", ns);
            decision.AppendChild(wrapper);
            wrapper.SetAttribute("class", "author");
            var content = doc.CreateElement("content", ns);
            wrapper.AppendChild(content);
            Block(content, model.Author, "p");
        }

        AddDivisions(decision, model.Contents);
    }

    protected void AddDivisions(XmlElement parent, IEnumerable<IDivision> divisions)
    {
        foreach (var division in divisions)
        {
            AddDivision(parent, division);
        }
    }

    protected abstract string MakeDivisionId(IDivision div);

    protected virtual void AddDivision(XmlElement parent, IDivision div)
    {
        if (div is ITableOfContents toc)
        {
            AddTableOfContents(parent, toc);
            return;
        }

        var name = div.Name ?? "level";
        var level = doc.CreateElement(name, ns);
        var eId = MakeDivisionId(div);
        if (eId is not null)
        {
            level.SetAttribute("eId", eId);
        }

        parent.AppendChild(level);
        if (div.Number is not null)
        {
            _ = AddAndWrapText(level, "num", div.Number);
        }

        if (div.Heading is not null)
        {
            Block(level, div.Heading, "heading");
        }

        if (div is IBranch branch)
        {
            AddIntro(level, branch);
            AddDivisions(level, branch.Children);
            AddWrapUp(level, branch);
        }
        else if (div is ILeaf leaf && leaf.Contents?.Count() > 0)
        {
            var content = doc.CreateElement("content", ns);
            level.AppendChild(content);
            blocks(content, leaf.Contents);
        }
        else
        {
            throw new Exception();
        }
    }

    protected void AddIntro(XmlElement level, IBranch branch)
    {
        if (branch.Intro is null || !branch.Intro.Any())
        {
            return;
        }

        var intro = CreateAndAppend("intro", level);
        blocks(intro, branch.Intro);
    }

    protected void AddWrapUp(XmlElement level, IBranch branch)
    {
        if (branch.WrapUp is null || !branch.WrapUp.Any())
        {
            return;
        }

        var wrapUp = CreateAndAppend("wrapUp", level);
        blocks(wrapUp, branch.WrapUp);
    }

    private void AddTableOfContents(XmlElement parent, ITableOfContents toc)
    {
        var level = CreateAndAppend("hcontainer", parent);
        level.SetAttribute("name", "tableOfContents");
        var content = CreateAndAppend("content", level);
        AddTableOfContents(content, toc.Contents);
    }

    private void AddTableOfContents(XmlElement parent, ITableOfContents2 toc)
    {
        AddTableOfContents(parent, toc.Contents);
    }

    private void AddTableOfContents(XmlElement parent, IEnumerable<ILine> contents)
    {
        var e = CreateAndAppend("toc", parent);
        foreach (var item in contents)
        {
            var tocItem = Block(e, item, "tocItem");
            tocItem.SetAttribute("level", "0");
            tocItem.SetAttribute("href", "#");
        }
    }


    /* blocks */

    protected void blocks(XmlElement parent, IEnumerable<IBlock> blocks)
    {
        foreach (var block in blocks)
        {
            Block(parent, block);
        }
    }

    protected virtual void Block(XmlElement parent, IBlock block)
    {
        if (block is IOldNumberedParagraph np)
        {
            var container = doc.CreateElement("blockContainer", ns);
            parent.AppendChild(container);
            if (np.Number is not null)
            {
                AddAndWrapText(container, "num", np.Number);
            }

            p(container, np);
        }
        else if (block is IRestriction restrict)
        {
            AddNamedBlock(parent, restrict, "restriction");
        }
        else if (block is ILine line)
        {
            p(parent, line);
        }
        else if (block is ITable table)
        {
            AddTable(parent, table);
        }
        else if (block is ITableOfContents2 toc)
        {
            AddTableOfContents(parent, toc);
        }
        else if (block is IQuotedStructure qs)
        {
            AddQuotedStructure(parent, qs);
        }
        else if (block is IDivWrapper wrapper)
        {
            AddDivision(parent, wrapper.Division);
        }
        else
        {
            throw new Exception(block.GetType().ToString());
        }
    }

    /* quoted structures */

    protected virtual void AddQuotedStructure(XmlElement blockContext, IQuotedStructure qs)
    {
        var block = CreateAndAppend("block", blockContext);
        block.SetAttribute("name", "embeddedStructure");
        var embeddedStructure = CreateAndAppend("embeddedStructure", block);
        AddDivisions(embeddedStructure, qs.Contents);
    }

    /* tables */

    protected static int getColspan(XmlElement td)
    {
        var attr = td.GetAttribute("colspan");
        return string.IsNullOrEmpty(attr) ? 1 : int.Parse(attr);
    }

    protected static void incrementRowspan(XmlElement td)
    {
        var attr = td.GetAttribute("rowspan");
        var rowspan = string.IsNullOrEmpty(attr) ? 1 : int.Parse(attr);
        rowspan += 1;
        td.SetAttribute("rowspan", rowspan.ToString());
    }

    protected static void DecrementRowspans(List<XmlElement> row)
    {
        foreach (var td in row)
        {
            var attr = td.GetAttribute("rowspan");
            var rowspan = string.IsNullOrEmpty(attr) ? 1 : int.Parse(attr);
            rowspan -= 1;
            if (rowspan > 1)
            {
                td.SetAttribute("rowspan", rowspan.ToString());
            }
            else
            {
                td.RemoveAttribute("rowspan");
            }
        }
    }

    protected virtual void AddTable(XmlElement parent, ITable model)
    {
        var table = doc.CreateElement("table", ns);
        if (model.Style is not null)
        {
            table.SetAttribute("class", model.Style);
        }

        parent.AppendChild(table);
        var columnWidths = model.ColumnWidthsIns;
        if (columnWidths.Any())
        {
            var s = columnWidths.Select(w => CSS2.ConvertSize(w, "in"));
            var s2 = string.Join(" ", s);
            table.SetAttribute("widths", UKNS, s2);
        }

        /* This keeps a grid of cells, with the dimensions the table would have
        /* if none of the cells were merged. Merged cells are repeated.
        /* The purpose is to find the correct cell above for vertically merged cells. */
        var allCellsWithRepeats = new List<List<XmlElement>>();

        var rows = model.Rows.Select(r => r.Cells.ToList()).ToList(); // enrichers are lazy
        var iRow = 0;
        foreach (var row in rows)
        {
            var thisRowOfCellsWithRepeats = new List<XmlElement>();
            allCellsWithRepeats.Add(thisRowOfCellsWithRepeats);

            var rowIsHeader = model.Rows.ElementAt(iRow).IsHeader;
            var tr = doc.CreateElement("tr", ns);
            var iCell = 0;
            foreach (var cell in row)
            {
                if (cell.VMerge == VerticalMerge.Continuation)
                {
                    // the cell above for which this is a continuation
                    var above = allCellsWithRepeats[iRow - 1][iCell];
                    incrementRowspan(above);
                    blocks(above, cell.Contents);
                    var colspanAbove = getColspan(above);
                    for (var i = 0; i < colspanAbove; i++)
                    {
                        thisRowOfCellsWithRepeats.Add(above);
                    }

                    iCell += colspanAbove;
                    continue;
                }

                var td = doc.CreateElement(rowIsHeader ? "th" : "td", ns);
                if (cell.ColSpan is not null)
                {
                    td.SetAttribute("colspan", cell.ColSpan.ToString());
                }

                var styles = cell.GetCSSStyles();
                ApplyTableCellStyleCleanup(cell, styles);
                if (styles.Any())
                {
                    td.SetAttribute("style", CSS.SerializeInline(styles));
                }

                tr.AppendChild(td);
                blocks(td, cell.Contents);

                var colspan = cell.ColSpan ?? 1;
                for (var i = 0; i < colspan; i++)
                {
                    thisRowOfCellsWithRepeats.Add(td);
                }

                iCell += colspan;
            }

            if (tr.HasChildNodes)
            {
                // some rows might contain nothing but merged cells
                table.AppendChild(tr);
            }
            else
            {
                // if row is not added, rowspans in row above may need to be adjusted, e.g., [2024] EWHC 2920 (KB)
                var above = allCellsWithRepeats[iRow - 1];
                DecrementRowspans(above);
            }

            iRow += 1;
        }
    }

    protected string ContainingParagraphStyle;

    /// <summary>
    /// Extension point: subclasses may add doc-type-specific attributes
    /// to the block element after the standard <c>class</c> attribute
    /// has been set and before content is appended. Default no-op.
    /// </summary>
    protected virtual void DecorateBlockElement(XmlElement block, ILine line) { }

    /// <summary>
    /// Extension point: subclasses may mutate a table cell's serialised
    /// CSS styles before they are written to the <c>td</c>/<c>th</c>.
    /// Default no-op — the base builder emits cell styles unchanged, as
    /// on main.
    /// </summary>
    protected virtual void ApplyTableCellStyleCleanup(ICell cell, Dictionary<string, string> styles) { }

    protected virtual XmlElement Block(XmlElement parent, ILine line, string name)
    {
        var block = doc.CreateElement(name, ns);
        parent.AppendChild(block);
        if (line.Style is not null)
        {
            block.SetAttribute("class", line.Style);
        }

        DecorateBlockElement(block, line);
        var styles = line.GetCSSStyles();
        if (styles.Count > 0)
        {
            block.SetAttribute("style", CSS.SerializeInline(styles));
        }

        ContainingParagraphStyle = line.Style;
        foreach (var inline in line.Contents)
        {
            AddInline(block, inline);
        }

        ContainingParagraphStyle = null;
        return block;
    }

    private void AddNamedBlock(XmlElement parent, ILine line, string name)
    {
        var block = CreateAndAppend("block", parent);
        block.SetAttribute("name", name);
        if (line.Style is not null)
        {
            block.SetAttribute("class", line.Style);
        }

        var styles = line.GetCSSStyles();
        if (styles.Count > 0)
        {
            block.SetAttribute("style", CSS.SerializeInline(styles));
        }

        ContainingParagraphStyle = line.Style;
        foreach (var inline in line.Contents)
        {
            AddInline(block, inline);
        }

        ContainingParagraphStyle = null;
    }

    protected virtual void p(XmlElement parent, ILine line)
    {
        if (line is IRestriction)
        {
            AddNamedBlock(parent, line, "restriction");
        }
        else
        {
            Block(parent, line, "p");
        }
    }

    /* inline */

    private void AddInlineContainer(XmlElement parent, IInlineContainer model, string name)
    {
        // if (!model.Contents.Any())
        //     return;
        var container = CreateAndAppend("inline", parent);
        container.SetAttribute("name", name);
        AddInlineContainerContents(container, model.Contents);
    }

    protected void AddInlineContainerContents(XmlElement container, IEnumerable<IInline> contents)
    {
        if (!contents.All(IFormattedText.IsFormattedTextAndNothingElse))
        {
            AddInlines(container, contents);
            return;
        }

        var texts = contents.Cast<IFormattedText>();
        if (texts.Count() == 1)
        {
            TextAndFormatting(container, texts.First());
        }
        else
        {
            AddOrWrapText(container, texts);
        }
    }

    protected void AddInlines(XmlElement parent, IEnumerable<IInline> models)
    {
        foreach (var model in models)
        {
            AddInline(parent, model);
        }
    }

    protected virtual void AddInline(XmlElement parent, IInline model)
    {
        switch (model)
        {
            case IDocType1 docType1:
                AddAndWrapText(parent, "docType", docType1);
                break;
            case IDocType2 docType:
                {
                    var courtType = CreateAndAppend("docType", parent);
                    foreach (var inline in docType.Contents)
                    {
                        AddInline(courtType, inline);
                    }

                    break;
                }
            case INeutralCitation cite:
                AddAndWrapText(parent, "neutralCitation", cite);
                break;
            case INeutralCitation2 cite2:
                {
                    var ncn2 = CreateAndAppend("neutralCitation", parent);
                    foreach (IInline inline in cite2.Contents)
                    {
                        AddInline(ncn2, inline);
                    }

                    break;
                }
            case ICourtType1 courtType1:
                AddCourtType1(parent, courtType1);
                break;
            case ICourtType2 courtType2:
                AddCourtType2(parent, courtType2);
                break;
            case ICaseNo caseNo:
                AddAndWrapText(parent, "docketNumber", caseNo);
                break;
            case IParty1 party:
                AddParty(parent, party);
                break;
            case IParty2 party2:
                AddParty2(parent, party2);
                break;
            case IRole role:
                AddRole(parent, role);
                break;
            case IDocTitle docTitle:
                AddDocTitle(parent, docTitle);
                break;
            case IDocTitle2 docTitle2:
                AddDocTitle(parent, docTitle2);
                break;
            case IJudge judge:
                AddJudge(parent, judge);
                break;
            case ILawyer lawyer:
                AddLawyer(parent, lawyer);
                break;
            case IDocJurisdiction juris:
                AddDocJurisdiction(parent, juris);
                break;
            case ILocation loc:
                AddLocation(parent, loc);
                break;
            case IHyperlink1 link:
                AddHyperlink(parent, link);
                break;
            case IHyperlink2 link2:
                AddHyperlink(parent, link2);
                break;
            case IInternalLink iLink:
                AddInternalLink(parent, iLink);
                break;
            case IFormattedText fText:
                AddOrWrapText(parent, fText);
                break;
            case IDocDate docDate:
                AddDocDate(parent, docDate);
                break;
            case IDate date:
                AddDate(parent, date);
                break;
            case IDateTime time:
                AddTime(parent, time);
                break;
            case IFootnote footnote:
                AddFootnote(parent, footnote);
                break;
            case IImageRef imageRef:
                AddImageRef(parent, imageRef);
                break;
            case IExternalImage eImg:
                AddExternalImage(parent, eImg);
                break;
            case IMath math:
                AddMath(parent, math);
                break;
            case IPageReference page:
                AddInlineContainer(parent, page, "page");
                break;
            case ILineBreak:
                AddLineBreak(parent);
                break;
            case ITab:
                AddTab(parent);
                break;
            case IBookmark:
                break;
            case IInvalidRef reference:
                reference.Add(parent);
                break;
            default:
                throw new Exception(model.GetType().ToString());
        }
    }

    protected virtual XmlElement AddAndWrapText(XmlElement parent, string name, IFormattedText model)
    {
        var e = CreateAndAppend(name, parent);
        TextAndFormatting(e, model);
        return e;
    }

    private void TextAndFormatting(XmlElement e, IFormattedText model)
    {
        if (model.Style is not null)
        {
            e.SetAttribute("class", model.Style);
        }

        var styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
        {
            e.SetAttribute("style", CSS.SerializeInline(styles));
        }

        if (model.IsHidden)
        {
            logger.LogInformation("hidden text: " + model.Text);
            e.SetAttribute("class", model.Style is null ? "hidden" : model.Style + " hidden");
            return;
        }

        if (model.BackgroundColor is not null && model.BackgroundColor != "auto" && model.BackgroundColor != "FFFFFF" &&
            model.BackgroundColor != "white")
        {
            logger.LogInformation("text with background color (" + model.BackgroundColor + "): " + model.Text);
        }

        TextWithoutFormatting(e, model);
    }

    private void TextWithoutFormatting(XmlElement parent, IFormattedText model)
    {
        // string content = IsRedacted(model) ? ReplaceRedacted(model.Text) : model.Text;
        // XmlText text = doc.CreateTextNode(content);
        var text = doc.CreateTextNode(model.Text);
        parent.AppendChild(text);
    }

    private void AddDate(XmlElement parent, IDate model)
    {
        var date = doc.CreateElement("date", ns);
        parent.AppendChild(date);
        date.SetAttribute("date", model.Date);
        if (model.Contents.Count() == 1)
        {
            var fText = model.Contents.First();
            var styles = fText.GetCSSStyles(ContainingParagraphStyle);
            if (styles.Count > 0)
            {
                date.SetAttribute("style", CSS.SerializeInline(styles));
            }

            var text = doc.CreateTextNode(fText.Text);
            date.AppendChild(text);
        }
        else
        {
            AddOrWrapText(date, model.Contents);
        }
    }

    private void AddTime(XmlElement parent, IDateTime model)
    {
        var e = doc.CreateElement("time", ns);
        parent.AppendChild(e);
        var attr = model.DateTime.ToString("s", CultureInfo.InvariantCulture);
        e.SetAttribute("time", attr);
        if (model.Contents.Count() == 1)
        {
            var fText = model.Contents.First();
            var styles = fText.GetCSSStyles(ContainingParagraphStyle);
            if (styles.Count > 0)
            {
                e.SetAttribute("style", CSS.SerializeInline(styles));
            }

            var text = doc.CreateTextNode(fText.Text);
            e.AppendChild(text);
        }
        else
        {
            AddOrWrapText(e, model.Contents);
        }
    }

    private void AddDocDate(XmlElement parent, IDocDate model)
    {
        var docDate = doc.CreateElement("docDate", ns);
        parent.AppendChild(docDate);
        docDate.SetAttribute("date", ((IDate)model).Date);
        docDate.SetAttribute("refersTo", "#" + Metadata.MakeDateId(model));
        if (model.Contents.Count() == 1)
        {
            var fText = model.Contents.First();
            var styles = fText.GetCSSStyles(ContainingParagraphStyle);
            if (styles.Count > 0)
            {
                docDate.SetAttribute("style", CSS.SerializeInline(styles));
            }

            var text = doc.CreateTextNode(fText.Text);
            docDate.AppendChild(text);
        }
        else
        {
            AddOrWrapText(docDate, model.Contents);
        }
    }

    private void AddCourtType1(XmlElement parent, ICourtType1 model)
    {
        var courtType = CreateAndAppend("courtType", parent);
        courtType.SetAttribute("refersTo", "#" + Metadata.MakeCourtId(model));
        TextAndFormatting(courtType, model);
    }

    private void AddCourtType2(XmlElement parent, ICourtType2 model)
    {
        var courtType = CreateAndAppend("courtType", parent);
        courtType.SetAttribute("refersTo", "#" + Metadata.MakeCourtId(model));
        foreach (var inline in model.Contents)
        {
            AddInline(courtType, inline);
        }
    }

    private void AddParty(XmlElement parent, IParty1 model)
    {
        if (model.Suppress)
        {
            AddOrWrapText(parent, model);
            return;
        }

        var party = doc.CreateElement("party", ns);
        parent.AppendChild(party);
        party.SetAttribute("refersTo", "#" + model.Id);
        if (model.Role.HasValue)
        {
            party.SetAttribute("as", "#" + ((PartyRole)model.Role).EId());
        }

        var styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
        {
            party.SetAttribute("style", CSS.SerializeInline(styles));
        }

        var text = doc.CreateTextNode(((IParty)model).Text);
        party.AppendChild(text);
    }

    private void AddParty2(XmlElement parent, IParty2 model)
    {
        if (model.Suppress)
        {
            AddInlines(parent, model.Contents);
            return;
        }

        var party = doc.CreateElement("party", ns);
        parent.AppendChild(party);
        party.SetAttribute("refersTo", "#" + model.Id);
        if (model.Role.HasValue)
        {
            party.SetAttribute("as", "#" + ((PartyRole)model.Role).EId());
        }

        foreach (var inline in model.Contents)
        {
            AddInline(party, inline);
        }
    }

    private void AddRole(XmlElement parent, IRole model)
    {
        var role = CreateAndAppend("role", parent);
        role.SetAttribute("refersTo", "#" + model.Role.EId());
        if (model.Contents.All(IFormattedText.IsFormattedTextAndNothingElse))
        {
            if (model.Contents.Count() == 1)
            {
                TextAndFormatting(role, model.Contents.Cast<IFormattedText>().First());
            }
            else
            {
                AddOrWrapText(role, model.Contents.Cast<IFormattedText>());
            }
        }
        else
        {
            foreach (var inline in model.Contents)
            {
                AddInline(role, inline);
            }
        }
    }

    private void AddDocTitle(XmlElement parent, IDocTitle model)
    {
        var docTitle = doc.CreateElement("docTitle", ns);
        parent.AppendChild(docTitle);
        var styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
        {
            docTitle.SetAttribute("style", CSS.SerializeInline(styles));
        }

        var text = doc.CreateTextNode(model.Text);
        docTitle.AppendChild(text);
    }

    private void AddDocTitle(XmlElement parent, IDocTitle2 model)
    {
        AddInlineContainer(parent, "docTitle", model.Contents);
    }

    private void AddInlineContainer(XmlElement parent, string name, IEnumerable<IInline> contents)
    {
        var x = doc.CreateElement(name, ns);
        parent.AppendChild(x);
        foreach (var inline in contents)
        {
            AddInline(x, inline);
        }
    }

    private void AddJudge(XmlElement parent, IJudge model)
    {
        var judge = doc.CreateElement("judge", ns);
        parent.AppendChild(judge);
        judge.SetAttribute("refersTo", "#" + model.Id);
        var styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
        {
            judge.SetAttribute("style", CSS.SerializeInline(styles));
        }

        var text = doc.CreateTextNode(model.Text);
        judge.AppendChild(text);
    }

    private void AddLawyer(XmlElement parent, ILawyer model)
    {
        var lawyer = doc.CreateElement("lawyer", ns);
        parent.AppendChild(lawyer);
        lawyer.SetAttribute("refersTo", "#" + model.Id);
        var styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
        {
            lawyer.SetAttribute("style", CSS.SerializeInline(styles));
        }

        var text = doc.CreateTextNode(model.Text);
        lawyer.AppendChild(text);
    }

    private void AddDocJurisdiction(XmlElement parent, IDocJurisdiction model)
    {
        var juris = doc.CreateElement("docJurisdiction", ns);
        parent.AppendChild(juris);
        juris.SetAttribute("refersTo", "#" + model.Id);
        if (model.Contents.Count() == 1 && model.Contents.First() is IFormattedText text)
        {
            TextAndFormatting(juris, text);
        }
        else
        {
            foreach (var inline in model.Contents)
            {
                AddInline(juris, inline);
            }
        }
    }

    private void AddLocation(XmlElement parent, ILocation model)
    {
        var loc = doc.CreateElement("location", ns);
        parent.AppendChild(loc);
        loc.SetAttribute("refersTo", "#" + model.Id);
        var styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
        {
            loc.SetAttribute("style", CSS.SerializeInline(styles));
        }

        var text = doc.CreateTextNode(model.Text);
        loc.AppendChild(text);
    }


    private void AddOrWrapText(XmlElement parent, IEnumerable<IFormattedText> text)
    {
        foreach (var span in text)
        {
            AddOrWrapText(parent, span);
        }
    }

    protected void AddOrWrapText(XmlElement parent, IFormattedText fText)
    {
        if (fText.Style is not null)
        {
            AddAndWrapText(parent, "span", fText);
            return;
        }

        if (fText.IsHidden)
        {
            AddAndWrapText(parent, "span", fText);
            return;
        }

        if (string.IsNullOrWhiteSpace(fText.Text))
        {
            AddAndWrapText(parent, "span", fText);
            return;
        }

        var styles = fText.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
        {
            AddAndWrapText(parent, "span", fText);
            return;
        }

        TextWithoutFormatting(parent, fText);
    }

    protected virtual void AddFootnote(XmlElement parent, IFootnote fn)
    {
        var authorialNote = doc.CreateElement("authorialNote", ns);
        parent.AppendChild(authorialNote);
        authorialNote.SetAttribute("class", "footnote");
        authorialNote.SetAttribute("marker", fn.Marker);
        blocks(authorialNote, fn.Content);
    }

    protected virtual void AddImageRef(XmlElement parent, IImageRef model)
    {
        var img = doc.CreateElement("img", ns);
        img.SetAttribute("src", model.Src);
        if (model.Style is not null)
        {
            img.SetAttribute("style", model.Style);
        }

        parent.AppendChild(img);
    }

    private void AddExternalImage(XmlElement parent, IExternalImage model)
    {
        var img = doc.CreateElement("img", ns);
        img.SetAttribute("src", model.URL);
        parent.AppendChild(img);
    }

    private void AddHyperlink(XmlElement parent, IHyperlink1 link)
    {
        if (link is IRef r)
        {
            AddRef(parent, r);
            return;
        }

        var x = AddAndWrapText(parent, "a", link);
        x.SetAttribute("href", link.Href);
        if (link.ScreenTip is not null)
        {
            x.SetAttribute("title", link.ScreenTip);
        }
    }

    private void AddHyperlink(XmlElement parent, IHyperlink2 link)
    {
        var a = CreateAndAppend("a", parent);
        a.SetAttribute("href", link.Href);
        if (link.ScreenTip is not null)
        {
            a.SetAttribute("title", link.ScreenTip);
        }

        AddInlineContainerContents(a, link.Contents);
    }

    protected virtual void AddInternalLink(XmlElement parent, IInternalLink link)
    {
        AddInlines(parent, link.Contents);
    }

    private void AddRef(XmlElement parent, IRef model)
    {
        var x = AddAndWrapText(parent, "ref", model);
        x.SetAttribute("href", model.Href);
        x.SetAttribute("origin", Metadata.ukns, "parser");
        x.SetAttribute("canonical", Metadata.ukns, model.Canonical);
        if (model.Type.HasValue)
        {
            x.SetAttribute("type", Metadata.ukns, Enum.GetName(typeof(RefType), model.Type.Value).ToLower());
        }

        if (model.IsNeutral.HasValue)
        {
            x.SetAttribute("isNeutral", Metadata.ukns, model.IsNeutral.Value.ToString().ToLower());
        }

        if (model.ScreenTip is not null)
        {
            x.SetAttribute("title", model.ScreenTip);
        }
    }

    private void AddMath(XmlElement parent, IMath model)
    {
        var subFlow = CreateAndAppend("subFlow", parent);
        subFlow.SetAttribute("name", "math");
        var foreign = CreateAndAppend("foreign", subFlow);
        var math = doc.ImportNode(model.MathML, true);
        foreign.AppendChild(math);
    }

    private void AddLineBreak(XmlElement parent)
    {
        var br = doc.CreateElement("br", ns);
        parent.AppendChild(br);
    }

    private void AddTab(XmlElement parent)
    {
        var tab = doc.CreateElement("marker", ns);
        tab.SetAttribute("name", "tab");
        // tab.SetAttribute("style", "display:inline-block");
        // tab.AppendChild(doc.CreateTextNode(" "));
        parent.AppendChild(tab);
    }

    protected static void AddHash(XmlDocument akn, string ns, string prefix = "uk", string localName = "hash")
    {
        var value = ContentHash.CalculateContentHash(akn);
        var nsmgr = new XmlNamespaceManager(akn.NameTable);
        nsmgr.AddNamespace("akn", Builder.ns);
        var proprietary = (XmlElement)akn.SelectSingleNode("/akn:akomaNtoso/akn:*/akn:meta/akn:proprietary", nsmgr);
        var hash = akn.CreateElement(prefix, localName, ns);
        proprietary.AppendChild(hash);
        hash.AppendChild(akn.CreateTextNode(value));
    }
}
