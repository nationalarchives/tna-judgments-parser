
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker;
using UK.Gov.NationalArchives.CaseLaw.Model;
using CSS2 = UK.Gov.Legislation.Judgments.CSS;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

abstract class Builder {

    private static ILogger logger = Logging.Factory.CreateLogger<Builder>();

    public static readonly string ns = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
    public static readonly string AknNamespace = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    protected abstract string UKNS { get; }

    protected readonly XmlDocument doc;

    protected Builder() {
        doc = new XmlDocument();
    }

    protected XmlElement CreateElement(string name) {
        return doc.CreateElement(name, ns);
    }
    protected XmlElement CreateAndAppend(string name, XmlNode parent) {
        XmlElement e = CreateElement(name);
        parent.AppendChild(e);
        return e;
    }

    protected void Build1(IJudgment judgment) {

        XmlElement akomaNtoso = CreateAndAppend("akomaNtoso", doc);
        akomaNtoso.SetAttribute("xmlns:uk", Metadata.ukns);

        XmlElement main = CreateAndAppend("judgment", akomaNtoso);
        main.SetAttribute("name", Enum.GetName(typeof(JudgmentType), judgment.Type).ToLower());

        XmlElement meta = Metadata.make(doc, judgment, judgment.Metadata, true);
        main.AppendChild(meta);

        AddCoverPage(main, judgment);
        AddHeader(main, judgment);
        AddBody(main, judgment);
        AddConclusions(main, judgment.Conclusions);
        AddAnnexesAndInternalAttachments(main, judgment);
    }

    private void AddCoverPage(XmlElement main, IJudgment judgment) {
        if (judgment.CoverPage is null)
            return;
        if (judgment.CoverPage.Count() == 0)
            return;
        XmlElement container = doc.CreateElement("coverPage", ns);
        main.AppendChild(container);
        blocks(container, judgment.CoverPage);
    }

    private void AddHeader(XmlElement main, IJudgment judgment) {
        XmlElement header = doc.CreateElement("header", ns);
        main.AppendChild(header);
        blocks(header, judgment.Header);
    }

    private void AddBody(XmlElement main, IJudgment judgment) {
        XmlElement body = doc.CreateElement("judgmentBody", ns);
        main.AppendChild(body);
        foreach (IDecision decision in judgment.Body)
            AddDecision(body,decision);
    }

    private void AddConclusions(XmlElement main, IEnumerable<IBlock> conclusions) {
        if (conclusions is null)
            return;
        if (conclusions.Count() == 0)
            return;
        XmlElement container = doc.CreateElement("conclusions", ns);
        main.AppendChild(container);
        blocks(container, conclusions);
    }

    private void AddAnnexesAndInternalAttachments(XmlElement main, IJudgment judgment) {
        IEnumerable<IAnnex> annexes = judgment.Annexes ?? Enumerable.Empty<IAnnex>();
        if (!annexes.Any() && !judgment.InternalAttachments.Any())
            return;
        XmlElement attachments = doc.CreateElement("attachments", ns);
        main.AppendChild(attachments);
        foreach (var annex in annexes.Select((value, i) => new { i, value }))
            AddAnnex(attachments, judgment, annex.value, annex.i + 1);
        foreach (var attach in judgment.InternalAttachments)
            AddInternalAttachment(attachments, judgment, attach);
    }

    private void AddAnnex(XmlElement attachments, IJudgment judgment, IAnnex annex, int n) {
        XmlElement attachment = doc.CreateElement("attachment", ns);
        attachments.AppendChild(attachment);
        XmlElement main = doc.CreateElement("doc", ns);
        main.SetAttribute("name", "annex");
        attachment.AppendChild(main);

        AttachmentMetadata metadata = new AttachmentMetadata(AttachmentType.Annex, judgment.Metadata, n);
        XmlElement meta = Metadata.make(doc, null, metadata, false);
        main.AppendChild(meta);

        XmlElement body = doc.CreateElement("mainBody", ns);
        main.AppendChild(body);
        p(body, annex.Number);
        blocks(body, annex.Contents);
    }

    private void AddInternalAttachment(XmlElement attachments, IJudgment judgment, IInternalAttachment attach) {
        XmlElement attachment = doc.CreateElement("attachment", ns);
        attachments.AppendChild(attachment);
        XmlElement main = doc.CreateElement("doc", ns);
        main.SetAttribute("name", Enum.GetName(typeof(AttachmentType), attach.Type).ToLower());
        attachment.AppendChild(main);

        AttachmentMetadata metadata = new AttachmentMetadata(attach.Type, judgment.Metadata, attach.Number) { Styles = attach.CSSStyles() };
        XmlElement meta = Metadata.make(doc, null, metadata, false);
        main.AppendChild(meta);

        XmlElement body = doc.CreateElement("mainBody", ns);
        main.AppendChild(body);
        blocks(body, attach.Contents);
    }

    /* structure */

    private void AddDecision(XmlElement body, IDecision model) {
        XmlElement decision = doc.CreateElement("decision", ns);
        body.AppendChild(decision);
        if (model.Author is not null) {
            XmlElement wrapper = doc.CreateElement("level", ns);
            decision.AppendChild(wrapper);
            wrapper.SetAttribute("class", "author");
            XmlElement content = doc.CreateElement("content", ns);
            wrapper.AppendChild(content);
            Block(content, model.Author, "p");
        }
        AddDivisions(decision, model.Contents);
    }

    protected void AddDivisions(XmlElement parent, IEnumerable<IDivision> divisions) {
        foreach (IDivision division in divisions)
            AddDivision(parent, division);
    }

    abstract protected string MakeDivisionId(IDivision div);

    protected virtual void AddDivision(XmlElement parent, IDivision div) {
        if (div is ITableOfContents toc) {
            AddTableOfContents(parent, toc);
            return;
        }
        string name = div.Name ?? "level";
        XmlElement level = doc.CreateElement(name, ns);
        string eId = MakeDivisionId(div);
        if (eId is not null)
            level.SetAttribute("eId", eId);
        parent.AppendChild(level);
        if (div.Number is not null) {
            XmlElement num = AddAndWrapText(level, "num", div.Number);
        }
        if (div.Heading is not null)
            Block(level, div.Heading, "heading");
        if (div is IBranch branch) {
            AddIntro(level, branch);
            AddDivisions(level, branch.Children);
            AddWrapUp(level, branch);
        } else if (div is ILeaf leaf && leaf.Contents?.Count() > 0) {
            XmlElement content = doc.CreateElement("content", ns);
            level.AppendChild(content);
            blocks(content, leaf.Contents);
        } else {
            throw new Exception();
        }
    }

    protected void AddIntro(XmlElement level, IBranch branch) {
        if (branch.Intro is null || !branch.Intro.Any())
            return;
        XmlElement intro = CreateAndAppend("intro", level);
        blocks(intro, branch.Intro);
    }
    protected void AddWrapUp(XmlElement level, IBranch branch) {
        if (branch.WrapUp is null || !branch.WrapUp.Any())
            return;
        XmlElement wrapUp = CreateAndAppend("wrapUp", level);
        blocks(wrapUp, branch.WrapUp);
    }

    private void AddTableOfContents(XmlElement parent, ITableOfContents toc) {
        XmlElement level = CreateAndAppend("hcontainer", parent);
        level.SetAttribute("name", "tableOfContents");
        XmlElement content = CreateAndAppend("content", level);
        AddTableOfContents(content, toc.Contents);
    }
    private void AddTableOfContents(XmlElement parent, ITableOfContents2 toc) {
        AddTableOfContents(parent, toc.Contents);
    }
    private void AddTableOfContents(XmlElement parent, IEnumerable<ILine> contents) {
        XmlElement e = CreateAndAppend("toc", parent);
        foreach (ILine item in contents) {
            var tocItem = Block(e, item, "tocItem");
            tocItem.SetAttribute("level", "0");
            tocItem.SetAttribute("href", "#");
        }
    }


    /* blocks */

    // private void AddContainer(XmlElement parent, IContainer model) {
    //     string name;
    //     XmlElement container = CreateAndAppend("container", parent);
    //     container.SetAttribute("name", name);
    //     blocks(container, model.Contents);
    // }

    protected void blocks(XmlElement parent, IEnumerable<IBlock> blocks) {
        foreach (IBlock block in blocks) {
            Block(parent, block);
        }
    }

    protected virtual void Block(XmlElement parent, IBlock block) {
        if (block is IOldNumberedParagraph np) {
            XmlElement container = doc.CreateElement("blockContainer", ns);
            parent.AppendChild(container);
            if (np.Number is not null)
                AddAndWrapText(container, "num", np.Number);
            this.p(container, np);
        } else if (block is IRestriction restrict) {
            AddNamedBlock(parent, restrict, "restriction");
        } else if (block is ILine line) {
            this.p(parent, line);
        } else if (block is ITable table) {
            AddTable(parent, table);
        } else if (block is ITableOfContents2 toc) {
            AddTableOfContents(parent, toc);
        } else if (block is IQuotedStructure qs) {
            AddQuotedStructure(parent, qs);
        } else if (block is IDivWrapper wrapper) {
            AddDivision(parent, wrapper.Division);
        } else {
            throw new Exception(block.GetType().ToString());
        }
    }

    /* quoted structures */

    protected virtual void AddQuotedStructure(XmlElement blockContext, IQuotedStructure qs) {
        XmlElement block = CreateAndAppend("block", blockContext);
        block.SetAttribute("name", "embeddedStructure");
        XmlElement embeddedStructure = CreateAndAppend("embeddedStructure", block);
        AddDivisions(embeddedStructure, qs.Contents);
    }

    /* tables */

    protected static int getColspan(XmlElement td) {
        string attr = td.GetAttribute("colspan");
        return string.IsNullOrEmpty(attr) ? 1 : int.Parse(attr);
    }
    protected static void incrementRowspan(XmlElement td) {
        string attr = td.GetAttribute("rowspan");
        int rowspan = string.IsNullOrEmpty(attr) ? 1 : int.Parse(attr);
        rowspan += 1;
        td.SetAttribute("rowspan", rowspan.ToString());
    }
    protected static void DecrementRowspans(List<XmlElement> row) {
        foreach (XmlElement td in row) {
            string attr = td.GetAttribute("rowspan");
            int rowspan = string.IsNullOrEmpty(attr) ? 1 : int.Parse(attr);
            rowspan -= 1;
            if (rowspan > 1)
                td.SetAttribute("rowspan", rowspan.ToString());
            else
                td.RemoveAttribute("rowspan");
        }
    }

    protected virtual void AddTable(XmlElement parent, ITable model) {
        XmlElement table = doc.CreateElement("table", ns);
        if (model.Style is not null)
            table.SetAttribute("class", model.Style);
        parent.AppendChild(table);
        List<float> columnWidths = model.ColumnWidthsIns;
        if (columnWidths.Any()) {
            IEnumerable<string> s = columnWidths.Select(w => CSS2.ConvertSize(w, "in"));
            string s2 = string.Join(" ", s);
            table.SetAttribute("widths", UKNS, s2);
        }

        /* This keeps a grid of cells, with the dimensions the table would have
        /* if none of the cells were merged. Merged cells are repeated.
        /* The purpose is to find the correct cell above for vertically merged cells. */
        List<List<XmlElement>> allCellsWithRepeats = new List<List<XmlElement>>();

        List<List<ICell>> rows = model.Rows.Select(r => r.Cells.ToList()).ToList(); // enrichers are lazy
        int iRow = 0;
        foreach (List<ICell> row in rows) {

            List<XmlElement> thisRowOfCellsWithRepeats = new List<XmlElement>();
            allCellsWithRepeats.Add(thisRowOfCellsWithRepeats);

            bool rowIsHeader = model.Rows.ElementAt(iRow).IsHeader;
            XmlElement tr = doc.CreateElement("tr", ns);
            int iCell = 0;
            foreach (ICell cell in row) {
                if (cell.VMerge == VerticalMerge.Continuation) {
                    // the cell above for which this is a continuation
                    XmlElement above = allCellsWithRepeats[iRow - 1][iCell];
                    incrementRowspan(above);
                    this.blocks(above, cell.Contents);
                    int colspanAbove = getColspan(above);
                    for (int i = 0; i < colspanAbove; i++)
                        thisRowOfCellsWithRepeats.Add(above);
                    iCell += colspanAbove;
                    continue;
                }
                XmlElement td = doc.CreateElement(rowIsHeader ? "th" : "td", ns);
                if (cell.ColSpan is not null)
                    td.SetAttribute("colspan", cell.ColSpan.ToString());
                Dictionary<string, string> styles = cell.GetCSSStyles();
                if (styles.Any())
                    td.SetAttribute("style", CSS.SerializeInline(styles));
                tr.AppendChild(td);
                this.blocks(td, cell.Contents);

                int colspan = cell.ColSpan ?? 1;
                for (int i = 0; i < colspan; i++)
                    thisRowOfCellsWithRepeats.Add(td);
                iCell += colspan;
            }
            if (tr.HasChildNodes) {   // some rows might contain nothing but merged cells
                table.AppendChild(tr);
            } else {
                // if row is not added, rowspans in row above may need to be adjusted, e.g., [2024] EWHC 2920 (KB)
                List<XmlElement> above = allCellsWithRepeats[iRow - 1];
                DecrementRowspans(above);
            }
            iRow += 1;
        }
    }

    private string ContainingParagraphStyle;

    protected virtual XmlElement Block(XmlElement parent, ILine line, string name) {
        XmlElement block = doc.CreateElement(name, ns);
        parent.AppendChild(block);
        if (line.Style is not null)
            block.SetAttribute("class", line.Style);
        Dictionary<string, string> styles = line.GetCSSStyles();
        if (styles.Count > 0)
            block.SetAttribute("style", CSS.SerializeInline(styles));
        ContainingParagraphStyle = line.Style;
        foreach (IInline inline in line.Contents)
            AddInline(block, inline);
        ContainingParagraphStyle = null;
        return block;
    }

    private void AddNamedBlock(XmlElement parent, ILine line, string name) {
        XmlElement block = CreateAndAppend("block", parent);
        block.SetAttribute("name", name);
        if (line.Style is not null)
            block.SetAttribute("class", line.Style);
        Dictionary<string, string> styles = line.GetCSSStyles();
        if (styles.Count > 0)
            block.SetAttribute("style", CSS.SerializeInline(styles));
        ContainingParagraphStyle = line.Style;
        foreach (IInline inline in line.Contents)
            AddInline(block, inline);
        ContainingParagraphStyle = null;
    }
    virtual protected void p(XmlElement parent, ILine line) {
        if (line is IRestriction restriction)
            AddNamedBlock(parent, line, "restriction");
        else
            Block(parent, line, "p");
    }

    /* inline */

    private void AddInlineContainer(XmlElement parent, IInlineContainer model, string name) {
        // if (!model.Contents.Any())
        //     return;
        XmlElement container = CreateAndAppend("inline", parent);
        container.SetAttribute("name", name);
        AddInlineContainerContents(container, model.Contents);
    }
    protected void AddInlineContainerContents(XmlElement container, IEnumerable<IInline> contents) {
        if (!contents.All(IFormattedText.IsFormattedTextAndNothingElse)) {
            AddInlines(container, contents);
            return;
        }
        var texts = contents.Cast<IFormattedText>();
        if (texts.Count() == 1)
            TextAndFormatting(container, texts.First());
        else
            AddOrWrapText(container, texts);
    }

    protected virtual void AddInlines(XmlElement parent, IEnumerable<IInline> models) {
        foreach (IInline model in models)
            AddInline(parent, model);
    }

    protected virtual void AddInline(XmlElement parent, IInline model) {
            if (model is IDocType1 docType1) {
                AddAndWrapText(parent, "docType", docType1);
            } else if (model is IDocType2 docType) {
                XmlElement courtType = CreateAndAppend("docType", parent);
                foreach (IInline inline in docType.Contents)
                    AddInline(courtType, inline);
            } else if (model is INeutralCitation cite)
                AddAndWrapText(parent, "neutralCitation", cite);
            else if (model is INeutralCitation2 cite2) {
                XmlElement ncn2 = CreateAndAppend("neutralCitation", parent);
                foreach (IInline inline in cite2.Contents)
                    AddInline(ncn2, inline);
            } else if (model is ICourtType1 courtType1)
                AddCourtType1(parent, courtType1);
            else if (model is ICourtType2 courtType2)
                AddCourtType2(parent, courtType2);
            else if (model is ICaseNo caseNo)
                AddAndWrapText(parent, "docketNumber", caseNo);
            else if (model is IParty1 party)
                AddParty(parent, party);
            else if (model is IParty2 party2)
                AddParty2(parent, party2);
            else if (model is IRole role)
                AddRole(parent, role);
            else if (model is IDocTitle docTitle)
                AddDocTitle(parent, docTitle);
            else if (model is IDocTitle2 docTitle2)
                AddDocTitle(parent, docTitle2);
            else if (model is IJudge judge)
                AddJudge(parent, judge);
            else if (model is ILawyer lawyer)
                AddLawyer(parent, lawyer);
            else if (model is IDocJurisdiction juris)
                AddDocJurisdiction(parent, juris);
            else if (model is ILocation loc)
                AddLocation(parent, loc);
            else if (model is IHyperlink1 link)
                AddHperlink(parent, link);
            else if (model is IHyperlink2 link2)
                AddHperlink(parent, link2);
            else if (model is IInternalLink iLink)
                AddInternalLink(parent, iLink);
            else if (model is IFormattedText fText)
                AddOrWrapText(parent, fText);
            else if (model is IDocDate docDate)
                AddDocDate(parent, docDate);
            else if (model is IDate date)
                AddDate(parent, date);
            else if (model is IDateTime time)
                AddTime(parent, time);
            else if (model is IFootnote footnote)
                AddFootnote(parent, footnote);
            else if (model is IImageRef imageRef)
                AddImageRef(parent, imageRef);
            else if (model is IExternalImage eImg)
                AddExternalImage(parent, eImg);
            else if (model is IMath math)
                AddMath(parent, math);
            else if (model is IPageReference page)
                AddInlineContainer(parent, page, "page");
            else if (model is ILineBreak)
                AddLineBreak(parent);
            else if (model is ITab)
                AddTab(parent);
        else if (model is IBookmark) { ; }
        else if (model is IInvalidRef reference) {
                reference.Add(parent);
            }

            else
                throw new Exception(model.GetType().ToString());
    }

    protected virtual XmlElement AddAndWrapText(XmlElement parent, string name, IFormattedText model) {
        XmlElement e = CreateAndAppend(name, parent);
        TextAndFormatting(e, model);
        return e;
    }

    private void TextAndFormatting(XmlElement e, IFormattedText model) {
        if (model.Style is not null)
            e.SetAttribute("class", model.Style);
        Dictionary<string, string> styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
            e.SetAttribute("style", CSS.SerializeInline(styles));
        if (model.IsHidden) {
            logger.LogInformation("hidden text: " + model.Text);
            e.SetAttribute("class", model.Style is null ? "hidden" : model.Style + " hidden");
            return;
        }
        if (model.BackgroundColor is not null && model.BackgroundColor != "auto" && model.BackgroundColor != "FFFFFF" && model.BackgroundColor != "white") {
            logger.LogInformation("text with background color (" + model.BackgroundColor + "): " + model.Text);
        }
        TextWithoutFormatting(e, model);
    }

    static bool IsRedacted(IFormattedText fText) {
        string bc = fText.BackgroundColor;
        if (bc is null)
            return false;
        if (bc.ToLower() != "black" && bc != "#000000")
            return false;
        string fc = fText.FontColor;
        if (fc is null)
            return true;
        if (fc.ToLower() == "black" || fc == "#000000")
            return true;
        // other colors?
        return true;
    }
    static string ReplaceRedacted(string text) {
        return new string('x', text.Length);
    }

    private void TextWithoutFormatting(XmlElement parent, IFormattedText model) {
        // string content = IsRedacted(model) ? ReplaceRedacted(model.Text) : model.Text;
        // XmlText text = doc.CreateTextNode(content);
        XmlText text = doc.CreateTextNode(model.Text);
        parent.AppendChild(text);
    }

    private void AddDate(XmlElement parent, IDate model) {
        XmlElement date = doc.CreateElement("date", ns);
        parent.AppendChild(date);
        date.SetAttribute("date", model.Date);
        if (model.Contents.Count() == 1) {
            IFormattedText fText = model.Contents.First();
            Dictionary<string, string> styles = fText.GetCSSStyles(ContainingParagraphStyle);
            if (styles.Count > 0)
                date.SetAttribute("style", CSS.SerializeInline(styles));
            XmlText text = doc.CreateTextNode(fText.Text);
            date.AppendChild(text);
        } else {
            AddOrWrapText(date, model.Contents);
        }
    }

    private void AddTime(XmlElement parent, IDateTime model) {
        XmlElement e = doc.CreateElement("time", ns);
        parent.AppendChild(e);
        string attr = model.DateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
        e.SetAttribute("time", attr);
        if (model.Contents.Count() == 1) {
            IFormattedText fText = model.Contents.First();
            Dictionary<string, string> styles = fText.GetCSSStyles(ContainingParagraphStyle);
            if (styles.Count > 0)
                e.SetAttribute("style", CSS.SerializeInline(styles));
            XmlText text = doc.CreateTextNode(fText.Text);
            e.AppendChild(text);
        } else {
            AddOrWrapText(e, model.Contents);
        }
    }

    private void AddDocDate(XmlElement parent, IDocDate model) {
        XmlElement docDate = doc.CreateElement("docDate", ns);
        parent.AppendChild(docDate);
        docDate.SetAttribute("date", ((IDate)model).Date);
        docDate.SetAttribute("refersTo", "#" + Metadata.MakeDateId(model));
        if (model.Contents.Count() == 1) {
            IFormattedText fText = model.Contents.First();
            Dictionary<string, string> styles = fText.GetCSSStyles(ContainingParagraphStyle);
            if (styles.Count > 0)
                docDate.SetAttribute("style", CSS.SerializeInline(styles));
            XmlText text = doc.CreateTextNode(fText.Text);
            docDate.AppendChild(text);
        } else {
            AddOrWrapText(docDate, model.Contents);
        }
    }

    private void AddCourtType1(XmlElement parent, ICourtType1 model) {
        XmlElement courtType = CreateAndAppend("courtType", parent);
        courtType.SetAttribute("refersTo", "#" + Metadata.MakeCourtId(model));
        TextAndFormatting(courtType, model);
    }
    private void AddCourtType2(XmlElement parent, ICourtType2 model) {
        XmlElement courtType = CreateAndAppend("courtType", parent);
        courtType.SetAttribute("refersTo", "#" + Metadata.MakeCourtId(model));
        foreach (IInline inline in model.Contents)
            AddInline(courtType, inline);
    }

    private void AddParty(XmlElement parent, IParty1 model) {
        if (model.Suppress) {
            AddOrWrapText(parent, model);
            return;
        }
        XmlElement party = doc.CreateElement("party", ns);
        parent.AppendChild(party);
        party.SetAttribute("refersTo", "#" + model.Id);
        if (model.Role.HasValue)
            party.SetAttribute("as", "#" + ((PartyRole) model.Role).EId());
        Dictionary<string, string> styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
            party.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(((IParty) model).Text);
        party.AppendChild(text);
    }
    private void AddParty2(XmlElement parent, IParty2 model) {
        if (model.Suppress) {
            AddInlines(parent, model.Contents);
            return;
        }
        XmlElement party = doc.CreateElement("party", ns);
        parent.AppendChild(party);
        party.SetAttribute("refersTo", "#" + model.Id);
        if (model.Role.HasValue)
            party.SetAttribute("as", "#" + ((PartyRole) model.Role).EId());
        foreach (var inline in model.Contents)
            AddInline(party, inline);
    }

    private void AddRole(XmlElement parent, IRole model) {
        XmlElement role = CreateAndAppend("role", parent);
        role.SetAttribute("refersTo", "#" + ((PartyRole) model.Role).EId());
        if (model.Contents.All(inline => IFormattedText.IsFormattedTextAndNothingElse(inline))) {
            if (model.Contents.Count() == 1) {
                TextAndFormatting(role, model.Contents.Cast<IFormattedText>().First());
            } else {
                AddOrWrapText(role, model.Contents.Cast<IFormattedText>());
            }
        } else {
            foreach (IInline inline in model.Contents)
                AddInline(role, inline);
        }
    }

    private void AddDocTitle(XmlElement parent, IDocTitle model) {
        XmlElement docTitle = doc.CreateElement("docTitle", ns);
        parent.AppendChild(docTitle);
        Dictionary<string, string> styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
            docTitle.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        docTitle.AppendChild(text);
    }
    private void AddDocTitle(XmlElement parent, IDocTitle2 model) {
        AddInlineContainer(parent, "docTitle", model.Contents);
    }

    private void AddInlineContainer(XmlElement parent, string name, IEnumerable<IInline> contents) {
        XmlElement x = doc.CreateElement(name, ns);
        parent.AppendChild(x);
        foreach (var inline in contents)
            AddInline(x, inline);
    }

    private void AddJudge(XmlElement parent, IJudge model) {
        XmlElement judge = doc.CreateElement("judge", ns);
        parent.AppendChild(judge);
        judge.SetAttribute("refersTo", "#" + model.Id);
        Dictionary<string, string> styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
            judge.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        judge.AppendChild(text);
    }
    private void AddLawyer(XmlElement parent, ILawyer model) {
        XmlElement lawyer = doc.CreateElement("lawyer", ns);
        parent.AppendChild(lawyer);
        lawyer.SetAttribute("refersTo", "#" + model.Id);
        Dictionary<string, string> styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
            lawyer.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        lawyer.AppendChild(text);
    }
    private void AddDocJurisdiction(XmlElement parent, IDocJurisdiction model) {
        XmlElement juris = doc.CreateElement("docJurisdiction", ns);
        parent.AppendChild(juris);
        juris.SetAttribute("refersTo", "#" + model.Id);
        if (model.Contents.Count() == 1 && model.Contents.First() is IFormattedText text) {
            TextAndFormatting(juris, text);
        } else {
            foreach (var inline in model.Contents)
                AddInline(juris, inline);
        }
    }
    private void AddLocation(XmlElement parent, ILocation model) {
        XmlElement loc = doc.CreateElement("location", ns);
        parent.AppendChild(loc);
        loc.SetAttribute("refersTo", "#" + model.Id);
        Dictionary<string, string> styles = model.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0)
            loc.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        loc.AppendChild(text);
    }


    private void AddOrWrapText(XmlElement parent, IEnumerable<IFormattedText> text) {
        foreach (IFormattedText span in text)
            AddOrWrapText(parent, span);
    }
    protected void AddOrWrapText(XmlElement parent, IFormattedText fText) {
        if (fText.Style is not null) {
            AddAndWrapText(parent, "span", fText);
            return;
        }
        if (fText.IsHidden) {
            AddAndWrapText(parent, "span", fText);
            return;
        }
        if (string.IsNullOrWhiteSpace(fText.Text)) {
            AddAndWrapText(parent, "span", fText);
            return;
        }
        Dictionary<string, string> styles = fText.GetCSSStyles(ContainingParagraphStyle);
        if (styles.Count > 0) {
            AddAndWrapText(parent, "span", fText);
            return;
        }
        TextWithoutFormatting(parent, fText);
    }

    protected virtual void AddFootnote(XmlElement parent, IFootnote fn) {
        XmlElement authorialNote = doc.CreateElement("authorialNote", ns);
        parent.AppendChild(authorialNote);
        authorialNote.SetAttribute("class", "footnote");
        authorialNote.SetAttribute("marker", fn.Marker);
        blocks(authorialNote, fn.Content);
    }

    protected virtual void AddImageRef(XmlElement parent, IImageRef model) {
        XmlElement img = doc.CreateElement("img", ns);
        img.SetAttribute("src", model.Src);
        if (model.Style is not null)
            img.SetAttribute("style", model.Style);
        parent.AppendChild(img);
    }
    private void AddExternalImage(XmlElement parent, IExternalImage model) {
        XmlElement img = doc.CreateElement("img", ns);
        img.SetAttribute("src", model.URL);
        parent.AppendChild(img);
    }

    private void AddHperlink(XmlElement parent, IHyperlink1 link) {
        if (link is IRef r) {
            AddRef(parent, r);
            return;
        }
        var x = AddAndWrapText(parent, "a", link);
        x.SetAttribute("href", link.Href);
        if (link.ScreenTip is not null)
            x.SetAttribute("title", link.ScreenTip);
    }

    private void AddHperlink(XmlElement parent, IHyperlink2 link) {
        XmlElement a = CreateAndAppend("a", parent);
        a.SetAttribute("href", link.Href);
        if (link.ScreenTip is not null)
            a.SetAttribute("title", link.ScreenTip);
        AddInlineContainerContents(a, link.Contents);
    }

    protected virtual void AddInternalLink(XmlElement parent, IInternalLink link) {
        AddInlines(parent, link.Contents);
    }

    private void AddRef(XmlElement parent, IRef model) {
        var x = AddAndWrapText(parent, "ref", model);
        x.SetAttribute("href", model.Href);
        x.SetAttribute("origin", Metadata.ukns, "parser");
        x.SetAttribute("canonical", Metadata.ukns, model.Canonical);
        if (model.Type.HasValue)
            x.SetAttribute("type", Metadata.ukns, Enum.GetName(typeof(RefType), model.Type.Value).ToLower());
        if (model.IsNeutral.HasValue)
            x.SetAttribute("isNeutral", Metadata.ukns, model.IsNeutral.Value.ToString().ToLower());
        if (model.ScreenTip is not null)
            x.SetAttribute("title", model.ScreenTip);
    }

    private void AddMath(XmlElement parent, IMath model) {
        XmlElement subFlow = CreateAndAppend("subFlow", parent);
        subFlow.SetAttribute("name", "math");
        XmlElement foreign = CreateAndAppend("foreign", subFlow);
        XmlNode math = doc.ImportNode(model.MathML, true);
        foreign.AppendChild(math);
    }

    private void AddLineBreak(XmlElement parent) {
        XmlElement br = doc.CreateElement("br", ns);
        parent.AppendChild(br);
    }

    private void AddTab(XmlElement parent) {
        XmlElement tab = doc.CreateElement("marker", ns);
        tab.SetAttribute("name", "tab");
        // tab.SetAttribute("style", "display:inline-block");
        // tab.AppendChild(doc.CreateTextNode(" "));
        parent.AppendChild(tab);
    }

    protected void AddHash(XmlDocument akn) {
        AddHash(akn, UKNS);
    }

    protected static void AddHash(XmlDocument akn, string ns) {
        string value = SHA256.Hash(akn);
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(akn.NameTable);
        nsmgr.AddNamespace("akn", Builder.ns);
        XmlElement proprietary = (XmlElement) akn.SelectSingleNode("/akn:akomaNtoso/akn:*/akn:meta/akn:proprietary", nsmgr);
        XmlElement hash = akn.CreateElement("uk", "hash", ns);
        proprietary.AppendChild(hash);
        hash.AppendChild(akn.CreateTextNode(value));
    }

}

}
