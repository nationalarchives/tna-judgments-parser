
using System.Xml;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation {

class Builder : AkN.Builder {

    public static XmlDocument Build(IDocument document) {
        Builder builder = new Builder();
        builder.PrivateBuild(document);
        return builder.doc;
    }

    private void PrivateBuild(IDocument document) {
        XmlElement akomaNtoso = CreateAndAppend("akomaNtoso", doc);
        XmlElement main = CreateAndAppend("doc", akomaNtoso);
        main.SetAttribute("name", document.Meta.Name);
        main.SetAttribute("xmlns:uk", UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.ukns); // for widths attr on table element
        AddMetadata(main, document.Meta);
        if (document.Header is not null && document.Header.Any()) {
            XmlElement header = doc.CreateElement("preface", ns);
            main.AppendChild(header);
            blocks(header, document.Header);
        }
        XmlElement body = CreateAndAppend("mainBody", main);
        if (document is IDividedDocument divided)
            AddDivisions(body, divided.Body);
        else if (document is IUndividedDocument undivided)
            blocks(body, undivided.Body);
        else
            throw new System.Exception();
    }

    private void AddMetadata(XmlElement main, DocumentMetadata data) {
        XmlElement meta = CreateAndAppend("meta", main);

        XmlElement identification = CreateAndAppend("identification", meta);
        identification.SetAttribute("source", "#tna");

        XmlElement work = CreateAndAppend("FRBRWork", identification);
        XmlElement workThis = CreateAndAppend("FRBRthis", work);
        workThis.SetAttribute("value", data.WorkUri);
        XmlElement workURI = CreateAndAppend("FRBRuri", work);
        workURI.SetAttribute("value", data.WorkUri);
        XmlElement workDate = CreateAndAppend("FRBRdate", work);
        if (data.WorkDate is null) {
            workDate.SetAttribute("date", System.DateTime.UtcNow.ToString("s").Substring(0, 10));
            workDate.SetAttribute("name", "generated");
        } else {
            workDate.SetAttribute("date", data.WorkDate);
            workDate.SetAttribute("name", data.WorkDateName);
        }
        XmlElement workAuthor = CreateAndAppend("FRBRauthor", work);
        workAuthor.SetAttribute("href", "#");
        XmlElement workCountry = CreateAndAppend("FRBRcountry", work);
        workCountry.SetAttribute("value", "GB-UKM");

        XmlElement expression = CreateAndAppend("FRBRExpression", identification);
        XmlElement expThis = CreateAndAppend("FRBRthis", expression);
        expThis.SetAttribute("value", data.ExpressionUri);
        XmlElement expURI = CreateAndAppend("FRBRuri", expression);
        expURI.SetAttribute("value",data.ExpressionUri);
        XmlElement expDate = CreateAndAppend("FRBRdate", expression);
        if (data.ExpressionDate is null) {
            expDate.SetAttribute("date", System.DateTime.UtcNow.ToString("s").Substring(0, 10));
            expDate.SetAttribute("name", "generated");
        } else {
            expDate.SetAttribute("date", data.ExpressionDate);
            expDate.SetAttribute("name", data.ExpressionDateName);
        }
        XmlElement expAuthor = CreateAndAppend("FRBRauthor", expression);
        expAuthor.SetAttribute("href", "#");
        XmlElement expLanguage = CreateAndAppend("FRBRlanguage", expression);
        expLanguage.SetAttribute("language", "eng");

        XmlElement manifestation = CreateAndAppend("FRBRManifestation", identification);
        XmlElement maniThis = CreateAndAppend("FRBRthis", manifestation);
        maniThis.SetAttribute("value", data.ExpressionUri + "/data.xml");
        XmlElement maniURI = CreateAndAppend("FRBRuri", manifestation);
        maniURI.SetAttribute("value",  data.ExpressionUri + "/data.xml");
        XmlElement maniDate = CreateAndAppend("FRBRdate", manifestation);
        maniDate.SetAttribute("date", System.DateTime.UtcNow.ToString("s"));
        maniDate.SetAttribute("name", "transform");
        XmlElement maniAuthor = CreateAndAppend("FRBRauthor", manifestation);
        maniAuthor.SetAttribute("href", "#tna");
        XmlElement maniFormat = CreateAndAppend("FRBRformat", manifestation);
        maniFormat.SetAttribute("value", "application/xml");

        XmlElement references = CreateAndAppend("references", meta);
        references.SetAttribute("source", "#tna");
        XmlElement tna = CreateAndAppend("TLCOrganization", references);
        tna.SetAttribute("eId", "tna");
        tna.SetAttribute("href", "https://www.nationalarchives.gov.uk/");
        tna.SetAttribute("showAs", "The National Archives");

        if (data.CSS is not null) {
            XmlElement presentation = CreateAndAppend("presentation", meta);
            presentation.SetAttribute("source", "#");
            XmlElement style = doc.CreateElement("style", "http://www.w3.org/1999/xhtml");
            presentation.AppendChild(style);
            style.AppendChild(doc.CreateTextNode("\n"));
            string css = AkN.CSS.Serialize(data.CSS);
            style.AppendChild(doc.CreateTextNode(css));
        }
    }

    protected override void AddDivision(XmlElement parent, Judgments.IDivision div) {
        if (div is Model.IParagraph para)
            base.AddDivision(parent, div);
        else if (div is Model.ISubparagraph subpara)
            base.AddDivision(parent, div);
        else
            base.AddDivision(parent, div);
    }

    protected override void AddInline(XmlElement parent, Judgments.IInline model) {
        if (model is Model.DocType2 docType) {
            XmlElement e = CreateAndAppend("docType", parent);
            foreach (Judgments.IInline child in docType.Contents)
                base.AddInline(e, child);
        } else if (model is Model.DocNumber2 docNum) {
            XmlElement e = CreateAndAppend("docNumber", parent);
            foreach (Judgments.IInline child in docNum.Contents)
                base.AddInline(e, child);
        } else {
            base.AddInline(parent, model);
        }
    }

    protected override string MakeDivisionId(IDivision div) {
        return null;
    }

}

}