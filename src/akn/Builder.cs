
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using Microsoft.Extensions.Logging;

using CSS2 = UK.Gov.Legislation.Judgments.CSS;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Builder {

    private static ILogger logger = Logging.Factory.CreateLogger<Builder>();

    public static readonly string ns = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    protected readonly XmlDocument doc;

    protected Builder() {
        doc = new XmlDocument();
    }

    private XmlElement CreateElement(string name) {
        return doc.CreateElement(name, ns);
    }
    protected XmlElement CreateAndAppend(string name, XmlNode parent) {
        XmlElement e = CreateElement(name);
        parent.AppendChild(e);
        return e;
    }

    private void Build1(IJudgment judgment) {

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

    protected virtual void AddDivision(XmlElement parent, IDivision div) {
        if (div is ITableOfContents toc) {
            AddTableOfContents(parent, toc);
            return;
        }
        string name = (div is ILeaf && div.Number is not null) ? "paragraph" : "level";
        AddDivision(parent, div, name);
    }
    protected void AddDivision(XmlElement parent, IDivision div, string name) {
        XmlElement level = doc.CreateElement(name, ns);
        parent.AppendChild(level);
        if (div.Number is not null) {
            XmlElement num = AddAndWrapText(level, "num", div.Number);
        }
        if (div.Heading is not null)
            Block(level, div.Heading, "heading");
        if (div is IBranch branch) {
            AddDivisions(level, branch.Children);
        } else if (div is ILeaf leaf) {
            XmlElement content = doc.CreateElement("content", ns);
            level.AppendChild(content);
            blocks(content, leaf.Contents);
        } else {
            throw new Exception();
        }
    }

    private void AddTableOfContents(XmlElement parent, ITableOfContents toc) {
        XmlElement level = CreateAndAppend("hcontainer", parent);
        level.SetAttribute("name", "tableOfContents");
        XmlElement content = CreateAndAppend("content", level);
        XmlElement e = CreateAndAppend("toc", content);
        foreach (ILine item in toc.Contents) {
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
            // } else if (block is IContainer contain) {
            //     AddContainer(parent, contain);
            } else {
                throw new Exception(block.GetType().ToString());
            }
        }
    }

    private void AddTable(XmlElement parent, ITable model) {
        XmlElement table = doc.CreateElement("table", ns);
        if (model.Style is not null)
            table.SetAttribute("class", model.Style);
        parent.AppendChild(table);
        List<float> columnWidths = model.ColumnWidthsIns;
        if (columnWidths.Any()) {
            IEnumerable<string> s = columnWidths.Select(w => CSS2.ConvertSize(w, "in"));
            string s2 = string.Join(" ", s);
            table.SetAttribute("widths", Metadata.ukns, s2);
        }
        List<ICell> mergedContentsHandled = new List<ICell>();
        List<List<ICell>> rows = model.Rows.Select(r => r.Cells.ToList()).ToList(); // enrichers are lazy
        int iRow = 0;
        foreach (List<ICell> row in rows) {
            XmlElement tr = doc.CreateElement("tr", ns);
            int iCell = 0;
            foreach (ICell cell in row) {
                if (cell.VMerge == VerticalMerge.Continuation) {
                    logger.LogDebug("skipping merged cell");
                    bool found = mergedContentsHandled.Remove(cell);
                    if (!found)
                        throw new Exception();
                    iCell += 1;
                    continue;
                }
                XmlElement td = doc.CreateElement("td", ns);
                if (cell.ColSpan is not null)
                    td.SetAttribute("colspan", cell.ColSpan.ToString());
                Dictionary<string, string> styles = cell.GetCSSStyles();
                if (styles.Any())
                    td.SetAttribute("style", CSS.SerializeInline(styles));
                tr.AppendChild(td);
                this.blocks(td, cell.Contents);
                if (cell.VMerge == VerticalMerge.Start) {
                    IEnumerable<ICell> merged = rows.Skip(iRow + 1)
                        .Select(r => r.Skip(iCell).First())
                        .TakeWhile(c => c.VMerge == VerticalMerge.Continuation);
                    td.SetAttribute("rowspan", (merged.Count() + 1).ToString());
                    foreach (ICell c in merged) {
                        this.blocks(td, c.Contents);
                        logger.LogDebug("handling merged cell");
                        mergedContentsHandled.Add(c);
                    }
                }
                iCell += 1;
            }
            if (tr.HasChildNodes)   // some rows might contain nothing but merged cells
                table.AppendChild(tr);
            iRow += 1;
        }

        if (mergedContentsHandled.Any()) {
            logger.LogCritical("error handling merged cells");
            throw new Exception();
        }
    }

    private XmlElement Block(XmlElement parent, ILine line, string name) {
        XmlElement block = doc.CreateElement(name, ns);
        parent.AppendChild(block);
        if (line.Style is not null)
            block.SetAttribute("class", line.Style);
        Dictionary<string, string> styles = line.GetCSSStyles();
        if (styles.Count > 0)
            block.SetAttribute("style", CSS.SerializeInline(styles));
        foreach (IInline inline in line.Contents)
            AddInline(block, inline);
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
        foreach (IInline inline in line.Contents)
            AddInline(block, inline);
    }
    private void p(XmlElement parent, ILine line) {
        if (line is IRestriction restriction)
            AddNamedBlock(parent, line, "restriction");
        else
            Block(parent, line, "p");
    }

    /* inline */

    protected virtual void AddInline(XmlElement parent, IInline model) {
        if (model is INeutralCitation cite)
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
        else if (model is IJudge judge)
            AddJudge(parent, judge);
        else if (model is ILawyer lawyer)
            AddLawyer(parent, lawyer);
        else if (model is ILocation loc)
            AddLocation(parent, loc);
        else if (model is IHyperlink1 link)
            AddHperlink(parent, link);
        else if (model is IHyperlink2 link2)
            AddHperlink(parent, link2);
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
        else if (model is ILineBreak)
            AddLineBreak(parent);
        else if (model is ITab tab)
            AddTab(parent);
        else
            throw new Exception(model.GetType().ToString());
    }

    private XmlElement AddAndWrapText(XmlElement parent, string name, IFormattedText model) {
        XmlElement e = CreateAndAppend(name, parent);
        TextAndFormatting(e, model);
        return e;
    }

    private XmlElement TextAndFormatting(XmlElement e, IFormattedText model) {
        if (model.Style is not null)
            e.SetAttribute("class", model.Style);
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            e.SetAttribute("style", CSS.SerializeInline(styles));
        if (model.IsHidden) {
            logger.LogInformation("hidden text: " + model.Text);
            e.SetAttribute("class", model.Style is null ? "hidden" : model.Style + " hidden");
        } else {
            XmlText text = doc.CreateTextNode(model.Text);
            e.AppendChild(text);
        }
        if (model.BackgroundColor is not null && model.BackgroundColor != "auto") {
            logger.LogInformation("text with background color (" + model.BackgroundColor + "): " + model.Text);
        }
        return e;
    }

    private void AddDate(XmlElement parent, IDate model) {
        XmlElement date = doc.CreateElement("date", ns);
        parent.AppendChild(date);
        date.SetAttribute("date", model.Date);
        if (model.Contents.Count() == 1) {
            IFormattedText fText = model.Contents.First();
            Dictionary<string, string> styles = fText.GetCSSStyles();
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
            Dictionary<string, string> styles = fText.GetCSSStyles();
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
            Dictionary<string, string> styles = fText.GetCSSStyles();
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
        XmlElement party = doc.CreateElement("party", ns);
        parent.AppendChild(party);
        party.SetAttribute("refersTo", "#" + model.Id);
        if (model.Role.HasValue)
            party.SetAttribute("as", "#" + ((PartyRole) model.Role).EId());
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            party.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(((IParty) model).Text);
        party.AppendChild(text);
    }
    private void AddParty2(XmlElement parent, IParty2 model) {
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
        if (model.Contents.All(inline => inline is IFormattedText)) {
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
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            docTitle.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        docTitle.AppendChild(text);
    }

    private void AddJudge(XmlElement parent, IJudge model) {
        XmlElement judge = doc.CreateElement("judge", ns);
        parent.AppendChild(judge);
        judge.SetAttribute("refersTo", "#" + model.Id);
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            judge.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        judge.AppendChild(text);
    }
    private void AddLawyer(XmlElement parent, ILawyer model) {
        XmlElement lawyer = doc.CreateElement("lawyer", ns);
        parent.AppendChild(lawyer);
        lawyer.SetAttribute("refersTo", "#" + model.Id);
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            lawyer.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        lawyer.AppendChild(text);
    }
    private void AddLocation(XmlElement parent, ILocation model) {
        XmlElement loc = doc.CreateElement("location", ns);
        parent.AppendChild(loc);
        loc.SetAttribute("refersTo", "#" + model.Id);
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            loc.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        loc.AppendChild(text);
    }


    private void AddOrWrapText(XmlElement parent, IEnumerable<IFormattedText> text) {
        foreach (IFormattedText span in text)
            AddOrWrapText(parent, span);
    }
    private void AddOrWrapText(XmlElement parent, IFormattedText fText) {
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
        XmlText text = doc.CreateTextNode(fText.Text);
        Dictionary<string, string> styles = fText.GetCSSStyles();
        if (styles.Count > 0) {
            XmlElement span = doc.CreateElement("span", ns);
            parent.AppendChild(span);
            span.SetAttribute("style", CSS.SerializeInline(styles));
            span.AppendChild(text);
        } else {
            parent.AppendChild(text);
        }
    }

    private void AddFootnote(XmlElement parent, IFootnote fn) {
        XmlElement authorialNote = doc.CreateElement("authorialNote", ns);
        parent.AppendChild(authorialNote);
        authorialNote.SetAttribute("class", "footnote");
        authorialNote.SetAttribute("marker", fn.Marker);
        blocks(authorialNote, fn.Content);
    }

    private void AddImageRef(XmlElement parent, IImageRef model) {
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
        foreach (IInline inline in link.Contents)
            AddInline(a, inline);
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

    /* public */

    public static XmlDocument Build(UK.Gov.Legislation.Judgments.IJudgment judgment) {
        Builder akn = new Builder();
        akn.Build1(judgment);
        AddHash(akn.doc);
        return akn.doc;
    }

    private static void AddHash(XmlDocument akn) {
        string value = SHA256.Hash(akn);
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(akn.NameTable);
        nsmgr.AddNamespace("akn", Builder.ns);
        XmlElement proprietary = (XmlElement) akn.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:proprietary", nsmgr);
        XmlElement hash = akn.CreateElement("hash", Metadata.ukns);
        proprietary.AppendChild(hash);
        hash.AppendChild(akn.CreateTextNode(value));
    }


}

}
