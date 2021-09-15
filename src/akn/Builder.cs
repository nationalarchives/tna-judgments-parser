
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Builder {

    private static ILogger logger = Logging.Factory.CreateLogger<Builder>();

    public static readonly string ns = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    private readonly XmlDocument doc;

    private Builder() {
        doc = new XmlDocument();
    }

    private XmlElement CreateElement(string name) {
        return doc.CreateElement(name, ns);
    }
    private XmlElement CreateAndAppend(string name, XmlNode parent) {
        XmlElement e = CreateElement(name);
        parent.AppendChild(e);
        return e;
    }

    private void Build1(IJudgment judgment) {

        XmlElement akomaNtoso = CreateAndAppend("akomaNtoso", doc);

        XmlElement main = CreateAndAppend("judgment", akomaNtoso);
        main.SetAttribute("name", "judgment");

        XmlElement meta = Metadata.make(doc, judgment, judgment.Metadata, true);
        main.AppendChild(meta);

        AddCoverPage(main, judgment);
        AddHeader(main, judgment);
        AddBody(main, judgment);
        AddConclusions(main, judgment.Conclusions);
        AddAnnexes(main, judgment);
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

    private void AddAnnexes(XmlElement main, IJudgment judgment) {
        if (judgment.Annexes is null)
            return;
        if (judgment.Annexes.Count() == 0)
            return;
        XmlElement attachments = doc.CreateElement("attachments", ns);
        main.AppendChild(attachments);
        foreach (var annex in judgment.Annexes.Select((value, i) => new { i, value }))
            AddAnnex(attachments, judgment, annex.value, annex.i + 1);
    }

    private void AddAnnex(XmlElement attachments, IJudgment judgment, IAnnex annex, int n) {
        XmlElement attachment = doc.CreateElement("attachment", ns);
        attachments.AppendChild(attachment);
        XmlElement main = doc.CreateElement("doc", ns);
        main.SetAttribute("name", "annex");
        attachment.AppendChild(main);

        AttachmentMetadata metadata = new AttachmentMetadata(judgment.Metadata, n);
        XmlElement meta = Metadata.make(doc, null, metadata, false);
        main.AppendChild(meta);

        XmlElement body = doc.CreateElement("mainBody", ns);
        main.AppendChild(body);
        p(body, annex.Number);
        blocks(body, annex.Contents);
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

    private void AddDivisions(XmlElement parent, IEnumerable<IDivision> divisions) {
        foreach (IDivision division in divisions)
            AddDivision(parent, division);
    }

    private void AddDivision(XmlElement parent, IDivision div) {
        string name = (div is ILeaf && div.Number is not null) ? "paragraph" : "level";
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

    /* blocks */

    private void blocks(XmlElement parent, IEnumerable<IBlock> blocks) {
        foreach (IBlock block in blocks) {
            if (block is IOldNumberedParagraph np) {
                XmlElement container = doc.CreateElement("blockContainer", ns);
                parent.AppendChild(container);
                XmlElement num = doc.CreateElement("num", ns);
                container.AppendChild(num);
                XmlText text = doc.CreateTextNode(np.Number);
                num.AppendChild(text);
                // this.blocks(container, np.Contents);
                this.p(container, np);
            } else if (block is ILine line) {
                this.p(parent, line);
            } else if (block is ITable table) {
                AddTable(parent, table);
            } else {
                throw new Exception(block.GetType().ToString());
            }
        }
    }

    private void AddTable(XmlElement parent, ITable model) {
        XmlElement tableElement = doc.CreateElement("table", ns);
        parent.AppendChild(tableElement);
        foreach (IRow row in model.Rows) {
            XmlElement rowElement = doc.CreateElement("tr", ns);
            tableElement.AppendChild(rowElement);
            foreach (ICell cell in row.Cells) {
                XmlElement cellElement = doc.CreateElement("td", ns);
                rowElement.AppendChild(cellElement);
                this.blocks(cellElement, cell.Contents);
            }
        }
    }
    private void Block(XmlElement parent, ILine line, string name) {
        XmlElement block = doc.CreateElement(name, ns);
        parent.AppendChild(block);
        if (line.Style is not null)
            block.SetAttribute("class", line.Style);
        Dictionary<string, string> styles = line.GetCSSStyles();
        if (styles.Count > 0)
            block.SetAttribute("style", CSS.SerializeInline(styles));
        foreach (IInline inline in line.Contents)
            AddInline(block, inline);
    }
    private void p(XmlElement parent, ILine line) {
        Block(parent, line, "p");
    }

    private void AddInline(XmlElement parent, IInline model) {
        if (model is INeutralCitation cite)
            AddAndWrapText(parent, "neutralCitation", cite);
        else if (model is ICourtType1 courtType1)
            AddAndWrapText(parent, "courtType", courtType1);
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
        if (model.BackgroundColor is not null) {
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
        docDate.SetAttribute("date", model.Date);
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

    private void AddCourtType2(XmlElement parent, ICourtType2 model) {
        XmlElement courtType = CreateAndAppend("courtType", parent);
        AddOrWrapText(courtType, model.Contents);
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
        XmlText text = doc.CreateTextNode(model.Text);
        party.AppendChild(text);
    }
    private void AddParty2(XmlElement parent, IParty2 model) {
        XmlElement party = doc.CreateElement("party", ns);
        parent.AppendChild(party);
        party.SetAttribute("refersTo", "#" + model.Id);
        if (model.Role.HasValue)
            party.SetAttribute("as", "#" + ((PartyRole) model.Role).EId());
        AddOrWrapText(party, model.Contents);
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
        XmlElement judge = doc.CreateElement("lawyer", ns);
        parent.AppendChild(judge);
        judge.SetAttribute("refersTo", "#" + model.Id);
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            judge.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        judge.AppendChild(text);
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
        return akn.doc;
    }

}

}
