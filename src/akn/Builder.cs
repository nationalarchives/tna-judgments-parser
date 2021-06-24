
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Builder {

    public static readonly string ns = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    private readonly XmlDocument doc;

    private Builder(IJudgment judgment) {
        doc = new XmlDocument();
        // doc.Schemas

        XmlProcessingInstruction stylesheet = doc.CreateProcessingInstruction("xml-stylesheet", "href='../judgment.xsl' type='text/xsl'");
        doc.AppendChild(stylesheet);

        XmlElement akomaNtoso = doc.CreateElement("akomaNtoso", ns);
        doc.AppendChild(akomaNtoso);

        XmlElement main = doc.CreateElement("judgment", ns);
        main.SetAttribute("name", "judgment");
        akomaNtoso.AppendChild(main);
        XmlElement meta = Metadata.make(doc, judgment, judgment.Metadata, true);
        main.AppendChild(meta);

        AddCoverPage(main, judgment);
        AddHeader(main, judgment);
        AddBody(main, judgment);
        AddConclusions(main, judgment.Conclusions);
        Annexes(main, judgment);
    }

    private void AddCoverPage(XmlElement main, IJudgment judgment) {
        if (judgment.CoverPage is null)
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
            Decision(body,decision);
    }

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
        foreach (IRow row in model.Rows()) {
            XmlElement rowElement = doc.CreateElement("tr", ns);
            tableElement.AppendChild(rowElement);
            foreach (ICell cell in row.Cells()) {
                XmlElement cellElement = doc.CreateElement("td", ns);
                rowElement.AppendChild(cellElement);
                this.blocks(cellElement, cell.Contents());
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
            AddText(parent, "neutralCitation", cite);
        else if (model is ICourtType caseType)
            AddText(parent, "courtType", caseType);
        else if (model is ICaseNo caseNo)
            AddText(parent, "docketNumber", caseNo);
        else if (model is IParty party)
            AddParty(parent, party);
        else if (model is IFormattedText fText)
            AddText(parent, fText);
        else if (model is IDocDate docDate)
            AddDocDate(parent, docDate);
        else if (model is IFootnote footnote)
            AddFootnote(parent, footnote);
        else if (model is IImageRef imageRef)
            AddImageRef(parent, imageRef);
        else if (model is ILineBreak)
            AddLineBreak(parent);
        else if (model is ITab tab)
            AddTab(parent);
        else
            throw new Exception(model.GetType().ToString());
    }

    private XmlElement AddText(XmlElement parent, string name, IFormattedText model) {
        XmlElement e = doc.CreateElement(name, ns);
        parent.AppendChild(e);
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            e.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        e.AppendChild(text);
        return e;
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
            AddText(docDate, model.Contents);
        }
    }

    private void AddParty(XmlElement parent, IParty model) {
        XmlElement party = doc.CreateElement("party", ns);
        parent.AppendChild(party);
        party.SetAttribute("refersTo", "#" + model.PartyId);
        Dictionary<string, string> styles = model.GetCSSStyles();
        if (styles.Count > 0)
            party.SetAttribute("style", CSS.SerializeInline(styles));
        XmlText text = doc.CreateTextNode(model.Text);
        party.AppendChild(text);
    }

    private void AddText(XmlElement parent, IEnumerable<IFormattedText> text) {
        foreach (IFormattedText span in text)
            AddText(parent, span);
    }
    private void AddText(XmlElement parent, IFormattedText fText) {
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
        foreach (ILine line in fn.Content)
            this.p(authorialNote, line);
    }

    private void AddImageRef(XmlElement parent, IImageRef model) {
        XmlElement img = doc.CreateElement("img", ns);
        img.SetAttribute("src", model.Src);
        if (model.Style is not null)
            img.SetAttribute("style", model.Style);
        parent.AppendChild(img);
    }

    private void AddLineBreak(XmlElement parent) {
        XmlElement br = doc.CreateElement("br", ns);
        parent.AppendChild(br);
    }

    private void AddTab(XmlElement parent) {
        XmlElement tab = doc.CreateElement("span", ns);
        tab.SetAttribute("class", "tab");
        tab.SetAttribute("style", "display:inline-block");
        // tab.SetAttribute("style", "min-width:1in");
        tab.AppendChild(doc.CreateTextNode(" "));
        parent.AppendChild(tab);
    }

    private void Decision(XmlElement body, IDecision model) {
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
        Divisions(decision, model.Contents);
    }

    private void Divisions(XmlElement parent, IEnumerable<IDivision> divisions) {
        foreach (IDivision division in divisions)
            Division(parent, division);
    }

    private void Division(XmlElement parent, IDivision div) {
        string name = (div is ILeaf && div.Number is not null) ? "paragraph" : "level";
        XmlElement level = doc.CreateElement(name, ns);
        parent.AppendChild(level);
        // level.SetAttribute("class", div.GetType().Name);
        if (div.Number is not null) {
            XmlElement num = AddText(level, "num", div.Number);
            // if ()
            // XmlElement num = doc.CreateElement("num", ns);
            // level.AppendChild(num);
            // AddText(num, div.Number);
        }
        if (div.Heading is not null)
            Block(level, div.Heading, "heading");
        if (div is IBranch branch) {
            Divisions(level, branch.Children);
        } else if (div is ILeaf leaf) {
            XmlElement content = doc.CreateElement("content", ns);
            level.AppendChild(content);
            blocks(content, leaf.Contents);
        } else {
            throw new Exception();
        }
    }

    /* conclusions */

    private void AddConclusions(XmlElement main, IEnumerable<IBlock> conclusions) {
        if (conclusions.Count() == 0)
            return;
        XmlElement container = doc.CreateElement("conclusions", ns);
        main.AppendChild(container);
        blocks(container, conclusions);
    }

    /* annexes */

    private void Annexes(XmlElement main, IJudgment judgment) {
        if (judgment.Annexes.Count() == 0)
            return;
        XmlElement attachments = doc.CreateElement("attachments", ns);
        main.AppendChild(attachments);
        foreach (var annex in judgment.Annexes.Select((value, i) => new { i, value }))
            Annex(attachments, judgment, annex.value, annex.i + 1);
    }

    private void Annex(XmlElement attachments, IJudgment judgment, IAnnex annex, int n) {
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

    /* public */

    public static XmlDocument Build(UK.Gov.Legislation.Judgments.IJudgment judgment) {
        Builder akn = new Builder(judgment);
        return akn.doc;
    }

}

}
