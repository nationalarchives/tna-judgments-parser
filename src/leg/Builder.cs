
using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
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
        
        // Add dc namespace if document has lastModified info (for dc:modified in proprietary)
        // Check both ExpressionDate with lastModified name and IA/EM-specific LastModified property
        bool hasModified = (document.Meta.ExpressionDateName == "lastModified" && document.Meta.ExpressionDate != null) ||
                          (document.Meta is ImpactAssessments.IAMetadata iaData && iaData.LastModified.HasValue) ||
                          (document.Meta is ExplanatoryMemoranda.EMMetadata emData && emData.LastModified.HasValue);
        if (hasModified) {
            main.SetAttribute("xmlns:dc", "http://purl.org/dc/elements/1.1/");
        }
        
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
        expLanguage.SetAttribute("language", "en");

        XmlElement manifestation = CreateAndAppend("FRBRManifestation", identification);
        XmlElement maniThis = CreateAndAppend("FRBRthis", manifestation);
        maniThis.SetAttribute("value", data.ExpressionUri + "/data.akn");
        XmlElement maniURI = CreateAndAppend("FRBRuri", manifestation);
        maniURI.SetAttribute("value",  data.ExpressionUri + "/data.akn");
        XmlElement maniDate = CreateAndAppend("FRBRdate", manifestation);
        maniDate.SetAttribute("date", FormatDateOnly(DateTime.UtcNow));
        maniDate.SetAttribute("name", "transform");
        XmlElement maniAuthor = CreateAndAppend("FRBRauthor", manifestation);
        maniAuthor.SetAttribute("href", "#tna");
        XmlElement maniFormat = CreateAndAppend("FRBRformat", manifestation);
        maniFormat.SetAttribute("value", "application/akn+xml");

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
        
        // Add dc:modified for all documents that have lastModified information
        // This provides a consistent location for file modification timestamp
        string modifiedValue = null;
        
        // For IA documents, use the LastModified property
        if (data is ImpactAssessments.IAMetadata iaData && iaData.LastModified.HasValue) {
            modifiedValue = FormatDateOnly(iaData.LastModified);
        }
        // For EM documents, use the LastModified property
        else if (data is ExplanatoryMemoranda.EMMetadata emData && emData.LastModified.HasValue) {
            modifiedValue = FormatDateOnly(emData.LastModified);
        }
        // For other documents, use ExpressionDate if it's a lastModified timestamp
        else if (data.ExpressionDateName == "lastModified" && data.ExpressionDate != null) {
            // Extract date portion only (first 10 characters: yyyy-MM-dd)
            modifiedValue = data.ExpressionDate.Length >= 10 ? data.ExpressionDate[..10] : data.ExpressionDate;
        }
        
        if (modifiedValue != null) {
            XmlElement modified = doc.CreateElement("dc", "modified", "http://purl.org/dc/elements/1.1/");
            proprietary.AppendChild(modified);
            modified.AppendChild(doc.CreateTextNode(modifiedValue));
        }

        // Add legislation reference if available (for Impact Assessments)
        if (data.LegislationUri is not null) {
            XmlElement legislation = doc.CreateElement("uk", "legislation", UKNS);
            proprietary.AppendChild(legislation);
            legislation.AppendChild(doc.CreateTextNode(data.LegislationUri));
        }

        // Add additional EM metadata from CSV mapping (for Explanatory Memoranda)
        if (data is ExplanatoryMemoranda.EMMetadata emMetadata) {
            if (!string.IsNullOrEmpty(emMetadata.DocumentMainType)) {
                XmlElement documentMainType = doc.CreateElement("uk", "documentMainType", UKNS);
                proprietary.AppendChild(documentMainType);
                documentMainType.AppendChild(doc.CreateTextNode(emMetadata.DocumentMainType));
            }
            
            if (!string.IsNullOrEmpty(emMetadata.Department)) {
                XmlElement department = doc.CreateElement("uk", "department", UKNS);
                proprietary.AppendChild(department);
                department.AppendChild(doc.CreateTextNode(emMetadata.Department));
            }
            
            if (!string.IsNullOrEmpty(emMetadata.EmDate)) {
                XmlElement emDate = doc.CreateElement("uk", "emDate", UKNS);
                proprietary.AppendChild(emDate);
                emDate.AppendChild(doc.CreateTextNode(emMetadata.EmDate));
            }
            
            if (!string.IsNullOrEmpty(emMetadata.LegislationClass)) {
                XmlElement legislationClass = doc.CreateElement("uk", "legislationClass", UKNS);
                proprietary.AppendChild(legislationClass);
                legislationClass.AppendChild(doc.CreateTextNode(emMetadata.LegislationClass));
            }
            
            // Year and version values (explicit for easier MarkLogic loading)
            if (emMetadata.EmYear.HasValue) {
                XmlElement emYear = doc.CreateElement("uk", "emYear", UKNS);
                proprietary.AppendChild(emYear);
                emYear.AppendChild(doc.CreateTextNode(emMetadata.EmYear.Value.ToString()));
            }
            
            XmlElement emVersion = doc.CreateElement("uk", "emVersion", UKNS);
            proprietary.AppendChild(emVersion);
            emVersion.AppendChild(doc.CreateTextNode(emMetadata.EmVersion.ToString()));
            
            if (emMetadata.LegislationYear.HasValue) {
                XmlElement legislationYear = doc.CreateElement("uk", "legislationYear", UKNS);
                proprietary.AppendChild(legislationYear);
                legislationYear.AppendChild(doc.CreateTextNode(emMetadata.LegislationYear.Value.ToString()));
            }
            
            if (!string.IsNullOrEmpty(emMetadata.LegislationNumber)) {
                XmlElement legislationNumber = doc.CreateElement("uk", "legislationNumber", UKNS);
                proprietary.AppendChild(legislationNumber);
                legislationNumber.AppendChild(doc.CreateTextNode(emMetadata.LegislationNumber));
            }
        }
        // Add additional IA metadata from CSV mapping (for Impact Assessments)
        else if (data is ImpactAssessments.IAMetadata iaMetadata) {
            // Add UKIA URI (e.g., http://www.legislation.gov.uk/id/ukia/2025/17)
            if (!string.IsNullOrEmpty(iaMetadata.UkiaUri)) {
                XmlElement ia = doc.CreateElement("uk", "IA", UKNS);
                proprietary.AppendChild(ia);
                ia.AppendChild(doc.CreateTextNode(iaMetadata.UkiaUri));
            }
            
            if (!string.IsNullOrEmpty(iaMetadata.DocumentStage)) {
                XmlElement documentStage = doc.CreateElement("uk", "documentStage", UKNS);
                proprietary.AppendChild(documentStage);
                documentStage.AppendChild(doc.CreateTextNode(iaMetadata.DocumentStage));
            }
            
            if (!string.IsNullOrEmpty(iaMetadata.DocumentMainType)) {
                XmlElement documentMainType = doc.CreateElement("uk", "documentMainType", UKNS);
                proprietary.AppendChild(documentMainType);
                documentMainType.AppendChild(doc.CreateTextNode(iaMetadata.DocumentMainType));
            }
            
            if (!string.IsNullOrEmpty(iaMetadata.Department)) {
                XmlElement department = doc.CreateElement("uk", "department", UKNS);
                proprietary.AppendChild(department);
                department.AppendChild(doc.CreateTextNode(iaMetadata.Department));
            }
            
            if (!string.IsNullOrEmpty(iaMetadata.IADate)) {
                XmlElement iaDate = doc.CreateElement("uk", "iaDate", UKNS);
                proprietary.AppendChild(iaDate);
                iaDate.AppendChild(doc.CreateTextNode(iaMetadata.IADate));
            }
            
            if (!string.IsNullOrEmpty(iaMetadata.PDFDate)) {
                XmlElement pdfDate = doc.CreateElement("uk", "pdfDate", UKNS);
                proprietary.AppendChild(pdfDate);
                pdfDate.AppendChild(doc.CreateTextNode(iaMetadata.PDFDate));
            }
            
            if (!string.IsNullOrEmpty(iaMetadata.LegislationClass)) {
                XmlElement legislationClass = doc.CreateElement("uk", "legislationClass", UKNS);
                proprietary.AppendChild(legislationClass);
                legislationClass.AppendChild(doc.CreateTextNode(iaMetadata.LegislationClass));
            }
            
            // Year and number values (explicit for easier MarkLogic loading)
            if (iaMetadata.UkiaYear.HasValue) {
                XmlElement ukiaYear = doc.CreateElement("uk", "ukiaYear", UKNS);
                proprietary.AppendChild(ukiaYear);
                ukiaYear.AppendChild(doc.CreateTextNode(iaMetadata.UkiaYear.Value.ToString()));
            }
            
            if (iaMetadata.UkiaNumber.HasValue) {
                XmlElement ukiaNumber = doc.CreateElement("uk", "ukiaNumber", UKNS);
                proprietary.AppendChild(ukiaNumber);
                ukiaNumber.AppendChild(doc.CreateTextNode(iaMetadata.UkiaNumber.Value.ToString()));
            }
            
            if (iaMetadata.LegislationYear.HasValue) {
                XmlElement legislationYear = doc.CreateElement("uk", "legislationYear", UKNS);
                proprietary.AppendChild(legislationYear);
                legislationYear.AppendChild(doc.CreateTextNode(iaMetadata.LegislationYear.Value.ToString()));
            }
            
            if (!string.IsNullOrEmpty(iaMetadata.LegislationNumber)) {
                XmlElement legislationNumber = doc.CreateElement("uk", "legislationNumber", UKNS);
                proprietary.AppendChild(legislationNumber);
                legislationNumber.AppendChild(doc.CreateTextNode(iaMetadata.LegislationNumber));
            }
        }

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
    //     if (div is IParagraph para)
    //         base.AddDivision(parent, div);
    //     else if (div is ISubparagraph subpara)
    //         base.AddDivision(parent, div);
    //     else
    //         base.AddDivision(parent, div);
    // }

    protected override void AddInline(XmlElement parent, Judgments.IInline model) {
        if (model is DocType2 docType) {
            XmlElement e = CreateAndAppend("docType", parent);
            foreach (Judgments.IInline child in docType.Contents)
                base.AddInline(e, child);
        } else if (model is DocNumber2 docNum) {
            XmlElement e = CreateAndAppend("docNumber", parent);
            foreach (Judgments.IInline child in docNum.Contents)
                base.AddInline(e, child);
        } else if (model is DocTitle docTitle) {
            XmlElement e = CreateAndAppend("docTitle", parent);
            foreach (Judgments.IInline child in docTitle.Contents)
                base.AddInline(e, child);
        } else if (model is DocStage docStage) {
            XmlElement e = CreateAndAppend("docStage", parent);
            foreach (Judgments.IInline child in docStage.Contents)
                base.AddInline(e, child);
        } else if (model is DocDate docDate) {
            XmlElement e = CreateAndAppend("docDate", parent);
            string dateValue = ExtractDateFromContent(docDate.Contents);
            if (!string.IsNullOrEmpty(dateValue)) {
                e.SetAttribute("date", dateValue);
            }
            foreach (Judgments.IInline child in docDate.Contents)
                base.AddInline(e, child);
        } else if (model is LeadDepartment leadDept) {
            XmlElement e = CreateAndAppend("inline", parent);
            e.SetAttribute("name", "leadDepartment");
            foreach (Judgments.IInline child in leadDept.Contents)
                base.AddInline(e, child);
        } else if (model is OtherDepartments otherDept) {
            XmlElement e = CreateAndAppend("inline", parent);
            e.SetAttribute("name", "otherDepartments");
            foreach (Judgments.IInline child in otherDept.Contents)
                base.AddInline(e, child);
        } else if (model is DocDepartment docDept) {
            XmlElement e = CreateAndAppend("docProponent", parent);
            foreach (Judgments.IInline child in docDept.Contents)
                base.AddInline(e, child);
        } else {
            base.AddInline(parent, model);
        }
    }

    private string ExtractDateFromContent(IEnumerable<Judgments.IInline> contents) {
        return null;
    }

    protected override string MakeDivisionId(IDivision div) {
        return null;
    }

    protected override void Block(XmlElement parent, IBlock block) {
        if (block is IOldNumberedParagraph np && 
            (parent.LocalName == "td" || parent.LocalName == "th")) {
            XmlElement p = doc.CreateElement("p", ns);
            parent.AppendChild(p);
            if (np.Number is not null && !string.IsNullOrWhiteSpace(np.Number.Text)) {
                p.AppendChild(doc.CreateTextNode(np.Number.Text + " "));
            }
            foreach (IInline inline in np.Contents)
                AddInline(p, inline);
        } else {
            base.Block(parent, block);
        }
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
        
        // Annex content goes directly in mainBody (no wrapper needed)
        // Schema now allows block elements (p, table, blockContainer) in mainBody
        p(body, annex.Number);
        blocks(body, annex.Contents);
    }

}

}