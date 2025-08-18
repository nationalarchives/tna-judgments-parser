
using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation {

class Builder : AkN.Builder {

    override protected string UKNS => "https://legislation.gov.uk/akn";

    private static string FormatDateOnly(DateTime? date) {
        return date?.ToString("s")[..10];
    }
    public static string FormatDateAndTime(DateTime? date) {
        return date?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static XmlDocument Build(IDocument document) {
        Builder builder = new();
        builder.PrivateBuild(document);
        return builder.doc;
    }

    private void PrivateBuild(IDocument document) {
        XmlElement akomaNtoso = CreateAndAppend("akomaNtoso", doc);
        XmlElement main = CreateAndAppend("doc", akomaNtoso);
        main.SetAttribute("name", document.Meta.Name);
        main.SetAttribute("xmlns:uk", UKNS);
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
        AddAnnexes(main, document);
        AddHash(doc);
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
            workDate.SetAttribute("date", FormatDateOnly(DateTime.UtcNow));
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
            expDate.SetAttribute("date", workDate.GetAttribute("date"));
            expDate.SetAttribute("name", workDate.GetAttribute("name"));
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
        maniDate.SetAttribute("date", FormatDateAndTime(DateTime.UtcNow));
        maniDate.SetAttribute("name", "transform");
        XmlElement maniAuthor = CreateAndAppend("FRBRauthor", manifestation);
        maniAuthor.SetAttribute("href", "#tna");
        XmlElement maniFormat = CreateAndAppend("FRBRformat", manifestation);
        maniFormat.SetAttribute("value", "application/xml");

        if (data is AnnexMetadata)
            return;

        XmlElement references = CreateAndAppend("references", meta);
        references.SetAttribute("source", "#tna");
        XmlElement tna = CreateAndAppend("TLCOrganization", references);
        tna.SetAttribute("eId", "tna");
        tna.SetAttribute("href", "https://www.nationalarchives.gov.uk/");
        tna.SetAttribute("showAs", "The National Archives");

        XmlElement proprietary = CreateAndAppend("proprietary", meta);
        proprietary.SetAttribute("source", "#");
        // if (data is Metadata em && em.AltNum is not null) {
        //     string ukm = "http://www.legislation.gov.uk/namespaces/metadata";
        //     proprietary.SetAttribute("xmlns:ukm", ukm);
        //     XmlElement altNum = doc.CreateElement("ukm", "AlternativeNumber", ukm);
        //     altNum.SetAttribute("Value", em.AltNum.Item2.ToString());
        //     altNum.SetAttribute("Category", em.AltNum.Item1);
        //     proprietary.AppendChild(altNum);
        // }
        XmlElement parser = doc.CreateElement("uk", "parser", UKNS);
        proprietary.AppendChild(parser);
        parser.AppendChild(doc.CreateTextNode(AkN.Metadata.GetParserVersion()));

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

    // protected override void AddDivision(XmlElement parent, Judgments.IDivision div) {
    //     if (div is Model.IParagraph para)
    //         base.AddDivision(parent, div);
    //     else if (div is Model.ISubparagraph subpara)
    //         base.AddDivision(parent, div);
    //     else
    //         base.AddDivision(parent, div);
    // }

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

    protected override void AddDivision(XmlElement parent, IDivision div) {
        if (div is UK.Gov.Legislation.Judgments.Parse.CrossHeading crossHeading) {
            AddCrossHeading(parent, crossHeading);
        } else if (div is Model.Subheading subheading) {
            AddSubheading(parent, subheading);
        } else {
            base.AddDivision(parent, div);
        }
    }

    private void AddCrossHeading(XmlElement parent, UK.Gov.Legislation.Judgments.Parse.CrossHeading crossHeading) {
        XmlElement level = doc.CreateElement("level", ns);
        level.SetAttribute("class", "crossheading");
        parent.AppendChild(level);
        
        if (crossHeading.Heading is not null) {
            XmlElement heading = doc.CreateElement("heading", ns);
            level.AppendChild(heading);
            foreach (var inline in crossHeading.Heading.Contents)
                AddInline(heading, inline);
        }
        
        AddDivisions(level, crossHeading.Children);
    }

    private void AddSubheading(XmlElement parent, Model.Subheading subheading) {
        XmlElement level = doc.CreateElement("level", ns);
        // Determine subheading level based on context or style
        string subheadingClass = DetermineSubheadingClass(subheading);
        level.SetAttribute("class", subheadingClass);
        parent.AppendChild(level);
        
        if (subheading.Heading is not null) {
            XmlElement heading = doc.CreateElement("heading", ns);
            level.AppendChild(heading);
            foreach (var inline in subheading.Heading.Contents)
                AddInline(heading, inline);
        }
        
        AddDivisions(level, subheading.Children);
    }

    private string DetermineSubheadingClass(Model.Subheading subheading) {
        // Check the heading's style to determine if it's level 1 or level 2
        if (subheading.Heading?.Style == "IALevel1Subheading")
            return "subheading1";
        else if (subheading.Heading?.Style == "IALevel2Subheading")
            return "subheading2";
        else
            return "subheading1"; // default
    }

    /* annexes */

    protected void AddAnnexes(XmlElement main, IDocument document) {
        if (document.Annexes is null)
            return;
        if (!document.Annexes.Any())
            return;
        XmlElement attachments = doc.CreateElement("attachments", ns);
        main.AppendChild(attachments);
        foreach (var annex in document.Annexes.Select((value, i) => new { i, value }))
            AddAnnex(attachments, annex.value, annex.i + 1, document.Meta);
    }

    private void AddAnnex(XmlElement attachments, IAnnex annex, int n, DocumentMetadata meta) {
        XmlElement attachment = doc.CreateElement("attachment", ns);
        attachments.AppendChild(attachment);
        XmlElement main = doc.CreateElement("doc", ns);
        main.SetAttribute("name", "annex");
        attachment.AppendChild(main);
        AddMetadata(main, new AnnexMetadata(meta, n));
        XmlElement body = doc.CreateElement("mainBody", ns);
        main.AppendChild(body);
        p(body, annex.Number);
        blocks(body, annex.Contents);
    }

}

}
