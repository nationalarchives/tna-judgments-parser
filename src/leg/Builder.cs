
using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation {

class Builder : AkN.Builder {

    public const string DefaultManifestationName = "historic-akn-transform";

    override protected string UKNS => "https://legislation.gov.uk/akn";
    private const string UKM_NS = "http://www.legislation.gov.uk/namespaces/metadata";

    private readonly string manifestationName;

    private Builder(string manifestationName) {
        this.manifestationName = manifestationName ?? DefaultManifestationName;
    }


    public static string FormatDateOnly(DateTime? date) {
        return date?.ToString("s")[..10];
    }

    private void AppendDocumentClassification(XmlElement proprietary, string mainTypeValue) {
        if (string.IsNullOrEmpty(mainTypeValue))
            return;
        XmlElement classification = doc.CreateElement("ukm", "DocumentClassification", UKM_NS);
        proprietary.AppendChild(classification);
        XmlElement mainType = doc.CreateElement("ukm", "DocumentMainType", UKM_NS);
        classification.AppendChild(mainType);
        mainType.SetAttribute("Value", mainTypeValue);
    }

    private static string BuildDcTitle(DocumentMetadata data) {
        string docTypeLabel = data switch {
            ImpactAssessments.IAMetadata => "Impact Assessment",
            ExplanatoryMemoranda.EMMetadata => "Explanatory Memorandum",
            ExplanatoryNotes.ENMetadata => "Explanatory Notes",
            TranspositionNotes.TNMetadata => "Transposition Note",
            CodesOfPractice.CoPMetadata => "Code of Practice",
            OtherDocuments.ODMetadata => "Other Document",
            _ => null
        };
        if (docTypeLabel is null)
            return null;
        if (!string.IsNullOrEmpty(data.LegislationTitle))
            return $"{docTypeLabel} for {data.LegislationTitle}";
        return docTypeLabel;
    }

    public static XmlDocument Build(IDocument document, string manifestationName = DefaultManifestationName) {
        Builder builder = new(manifestationName);
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
        AddHash(doc, UKM_NS, "ukm", "Hash");
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
        workAuthor.SetAttribute("href", data.WorkAuthor ?? "#");
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
        maniDate.SetAttribute("name", manifestationName);
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
        proprietary.SetAttribute("xmlns:ukm", UKM_NS);
        proprietary.SetAttribute("xmlns:dc", "http://purl.org/dc/elements/1.1/");

        XmlElement parser = doc.CreateElement("ukm", "Parser", UKM_NS);
        proprietary.AppendChild(parser);
        parser.SetAttribute("Value", AkN.Metadata.GetParserVersion());
        
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
        // For EN documents, use the LastModified property
        else if (data is ExplanatoryNotes.ENMetadata enData && enData.LastModified.HasValue) {
            modifiedValue = FormatDateOnly(enData.LastModified);
        }
        // For TN documents, use the LastModified property
        else if (data is TranspositionNotes.TNMetadata tnData && tnData.LastModified.HasValue) {
            modifiedValue = FormatDateOnly(tnData.LastModified);
        }
        // For CoP documents, use the LastModified property
        else if (data is CodesOfPractice.CoPMetadata copData && copData.LastModified.HasValue) {
            modifiedValue = FormatDateOnly(copData.LastModified);
        }
        // For OD documents, use the LastModified property
        else if (data is OtherDocuments.ODMetadata odData && odData.LastModified.HasValue) {
            modifiedValue = FormatDateOnly(odData.LastModified);
        }
        else if (data.ExpressionDateName == "lastModified" && data.ExpressionDate != null) {
            modifiedValue = data.ExpressionDate;
        }
        
        if (modifiedValue != null) {
            XmlElement modified = doc.CreateElement("dc", "modified", "http://purl.org/dc/elements/1.1/");
            proprietary.AppendChild(modified);
            modified.AppendChild(doc.CreateTextNode(modifiedValue));
        }

        // Add DC metadata properties
        string dcTitle = BuildDcTitle(data);
        if (!string.IsNullOrEmpty(dcTitle)) {
            string dcNs = "http://purl.org/dc/elements/1.1/";
            XmlElement title = doc.CreateElement("dc", "title", dcNs);
            proprietary.AppendChild(title);
            title.AppendChild(doc.CreateTextNode(dcTitle));

            XmlElement description = doc.CreateElement("dc", "description", dcNs);
            proprietary.AppendChild(description);
            description.AppendChild(doc.CreateTextNode(dcTitle));
        }

        {
            string dcNs = "http://purl.org/dc/elements/1.1/";
            XmlElement dcType = doc.CreateElement("dc", "type", dcNs);
            proprietary.AppendChild(dcType);
            dcType.AppendChild(doc.CreateTextNode("text"));

            XmlElement dcFormat = doc.CreateElement("dc", "format", dcNs);
            proprietary.AppendChild(dcFormat);
            dcFormat.AppendChild(doc.CreateTextNode("application/akn+xml"));

            XmlElement dcLanguage = doc.CreateElement("dc", "language", dcNs);
            proprietary.AppendChild(dcLanguage);
            dcLanguage.AppendChild(doc.CreateTextNode("en"));
        }

        if (!string.IsNullOrEmpty(data.Publisher)) {
            XmlElement dcPublisher = doc.CreateElement("dc", "publisher", "http://purl.org/dc/elements/1.1/");
            proprietary.AppendChild(dcPublisher);
            dcPublisher.AppendChild(doc.CreateTextNode(data.Publisher));
        }

        if (data.LegislationUri is not null) {
            XmlElement legislation = doc.CreateElement("ukm", "Legislation", UKM_NS);
            proprietary.AppendChild(legislation);
            legislation.SetAttribute("URI", data.LegislationUri);
            if (!string.IsNullOrEmpty(data.LegislationClass))
                legislation.SetAttribute("Class", data.LegislationClass);
            if (data.LegislationYear.HasValue)
                legislation.SetAttribute("Year", data.LegislationYear.Value.ToString());
            if (!string.IsNullOrEmpty(data.LegislationNumber))
                legislation.SetAttribute("Number", data.LegislationNumber);
        }

        // Add additional EM metadata from CSV mapping (for Explanatory Memoranda)
        if (data is ExplanatoryMemoranda.EMMetadata emMetadata) {
            // Add associated document URI for contractor upload (e.g. http://www.legislation.gov.uk/id/uksi/2013/2911/memorandum/1)
            if (!string.IsNullOrEmpty(emMetadata.ShortUriComponent)) {
                XmlElement associatedId = doc.CreateElement("dc", "identifier", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(associatedId);
                associatedId.AppendChild(doc.CreateTextNode(emMetadata.WorkUri));
            }
            AppendDocumentClassification(proprietary, emMetadata.DocumentMainType);

            if (!string.IsNullOrEmpty(emMetadata.Department)) {
                XmlElement department = doc.CreateElement("ukm", "Department", UKM_NS);
                proprietary.AppendChild(department);
                department.SetAttribute("Value", emMetadata.Department);
            }

            if (!string.IsNullOrEmpty(emMetadata.EmDate)) {
                XmlElement date = doc.CreateElement("dc", "date", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(date);
                date.AppendChild(doc.CreateTextNode(emMetadata.EmDate));
            }
        }
        // Add additional IA metadata from CSV mapping (for Impact Assessments)
        else if (data is ImpactAssessments.IAMetadata iaMetadata) {
            // Add associated document URI for contractor upload (e.g. http://www.legislation.gov.uk/id/ukia/2025/17)
            if (!string.IsNullOrEmpty(iaMetadata.UkiaUri)) {
                XmlElement associatedId = doc.CreateElement("dc", "identifier", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(associatedId);
                associatedId.AppendChild(doc.CreateTextNode(iaMetadata.UkiaUri));
            }
            
            if (!string.IsNullOrEmpty(iaMetadata.DocumentStage)) {
                XmlElement documentStage = doc.CreateElement("ukm", "DocumentStage", UKM_NS);
                proprietary.AppendChild(documentStage);
                documentStage.SetAttribute("Value", iaMetadata.DocumentStage);
            }

            AppendDocumentClassification(proprietary, iaMetadata.DocumentMainType);

            if (!string.IsNullOrEmpty(iaMetadata.Department)) {
                XmlElement department = doc.CreateElement("ukm", "Department", UKM_NS);
                proprietary.AppendChild(department);
                department.SetAttribute("Value", iaMetadata.Department);
            }

            if (!string.IsNullOrEmpty(iaMetadata.IADate)) {
                XmlElement date = doc.CreateElement("dc", "date", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(date);
                date.AppendChild(doc.CreateTextNode(iaMetadata.IADate));
            }

            if (!string.IsNullOrEmpty(iaMetadata.PDFDate)) {
                XmlElement pdfDate = doc.CreateElement("ukm", "PdfDate", UKM_NS);
                proprietary.AppendChild(pdfDate);
                pdfDate.SetAttribute("Value", iaMetadata.PDFDate);
            }

            if (iaMetadata.UkiaYear.HasValue) {
                XmlElement year = doc.CreateElement("ukm", "Year", UKM_NS);
                proprietary.AppendChild(year);
                year.SetAttribute("Value", iaMetadata.UkiaYear.Value.ToString());
            }

            if (iaMetadata.UkiaNumber.HasValue) {
                XmlElement number = doc.CreateElement("ukm", "Number", UKM_NS);
                proprietary.AppendChild(number);
                number.SetAttribute("Value", iaMetadata.UkiaNumber.Value.ToString());
            }
        }
        // Add additional EN metadata from CSV mapping (for Explanatory Notes)
        else if (data is ExplanatoryNotes.ENMetadata enMetadata) {
            if (!string.IsNullOrEmpty(enMetadata.ShortUriComponent)) {
                XmlElement associatedId = doc.CreateElement("dc", "identifier", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(associatedId);
                associatedId.AppendChild(doc.CreateTextNode(enMetadata.WorkUri));
            }
            AppendDocumentClassification(proprietary, enMetadata.DocumentMainType);
            if (!string.IsNullOrEmpty(enMetadata.EnDate)) {
                XmlElement date = doc.CreateElement("dc", "date", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(date);
                date.AppendChild(doc.CreateTextNode(enMetadata.EnDate));
            }
        }
        // Add additional TN metadata from CSV mapping (for Transposition Notes)
        else if (data is TranspositionNotes.TNMetadata tnMetadata) {
            if (!string.IsNullOrEmpty(tnMetadata.ShortUriComponent)) {
                XmlElement associatedId = doc.CreateElement("dc", "identifier", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(associatedId);
                associatedId.AppendChild(doc.CreateTextNode(tnMetadata.WorkUri));
            }
            AppendDocumentClassification(proprietary, tnMetadata.DocumentMainType);
            if (!string.IsNullOrEmpty(tnMetadata.Department)) {
                XmlElement department = doc.CreateElement("ukm", "Department", UKM_NS);
                proprietary.AppendChild(department);
                department.SetAttribute("Value", tnMetadata.Department);
            }
            if (!string.IsNullOrEmpty(tnMetadata.TnDate)) {
                XmlElement date = doc.CreateElement("dc", "date", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(date);
                date.AppendChild(doc.CreateTextNode(tnMetadata.TnDate));
            }
        }
        // Add additional CoP metadata from CSV mapping (for Codes of Practice)
        else if (data is CodesOfPractice.CoPMetadata copMetadata) {
            if (!string.IsNullOrEmpty(copMetadata.ShortUriComponent)) {
                XmlElement associatedId = doc.CreateElement("dc", "identifier", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(associatedId);
                associatedId.AppendChild(doc.CreateTextNode(copMetadata.WorkUri));
            }
            AppendDocumentClassification(proprietary, copMetadata.DocumentMainType);
            if (!string.IsNullOrEmpty(copMetadata.Department)) {
                XmlElement department = doc.CreateElement("ukm", "Department", UKM_NS);
                proprietary.AppendChild(department);
                department.SetAttribute("Value", copMetadata.Department);
            }
            if (!string.IsNullOrEmpty(copMetadata.CopDate)) {
                XmlElement date = doc.CreateElement("dc", "date", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(date);
                date.AppendChild(doc.CreateTextNode(copMetadata.CopDate));
            }
        }
        // Add additional OD metadata from CSV mapping (for Other Documents)
        else if (data is OtherDocuments.ODMetadata odMetadata) {
            if (!string.IsNullOrEmpty(odMetadata.ShortUriComponent)) {
                XmlElement associatedId = doc.CreateElement("dc", "identifier", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(associatedId);
                associatedId.AppendChild(doc.CreateTextNode(odMetadata.WorkUri));
            }
            AppendDocumentClassification(proprietary, odMetadata.DocumentMainType);
            if (!string.IsNullOrEmpty(odMetadata.Department)) {
                XmlElement department = doc.CreateElement("ukm", "Department", UKM_NS);
                proprietary.AppendChild(department);
                department.SetAttribute("Value", odMetadata.Department);
            }
            if (!string.IsNullOrEmpty(odMetadata.OdDate)) {
                XmlElement date = doc.CreateElement("dc", "date", "http://purl.org/dc/elements/1.1/");
                proprietary.AppendChild(date);
                date.AppendChild(doc.CreateTextNode(odMetadata.OdDate));
            }
        }

        if (data.Statistics is not null) {
            XmlElement statistics = doc.CreateElement("ukm", "Statistics", UKM_NS);
            proprietary.AppendChild(statistics);

            XmlElement totalParas = doc.CreateElement("ukm", "TotalParagraphs", UKM_NS);
            totalParas.SetAttribute("Value", data.Statistics.TotalParagraphs.ToString());
            statistics.AppendChild(totalParas);

            XmlElement bodyParas = doc.CreateElement("ukm", "BodyParagraphs", UKM_NS);
            bodyParas.SetAttribute("Value", data.Statistics.BodyParagraphs.ToString());
            statistics.AppendChild(bodyParas);

            XmlElement scheduleParas = doc.CreateElement("ukm", "ScheduleParagraphs", UKM_NS);
            scheduleParas.SetAttribute("Value", data.Statistics.ScheduleParagraphs.ToString());
            statistics.AppendChild(scheduleParas);

            XmlElement totalImages = doc.CreateElement("ukm", "TotalImages", UKM_NS);
            totalImages.SetAttribute("Value", data.Statistics.TotalImages.ToString());
            statistics.AppendChild(totalImages);
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

    /// <summary>Adds an inline to an AKN element that does not allow br (e.g. docTitle, inline). Line breaks are emitted as a space.</summary>
    private void AddInlineNoBr(XmlElement parent, Judgments.IInline model) {
        if (model is Judgments.ILineBreak)
            parent.AppendChild(doc.CreateTextNode(" "));
        else
            base.AddInline(parent, model);
    }

    protected override void AddInline(XmlElement parent, Judgments.IInline model) {
        if (model is DocType2 docType) {
            XmlElement e = CreateAndAppend("docType", parent);
            foreach (Judgments.IInline child in docType.Contents)
                AddInlineNoBr(e, child);
        } else if (model is DocNumber2 docNum) {
            XmlElement e = CreateAndAppend("docNumber", parent);
            foreach (Judgments.IInline child in docNum.Contents)
                AddInlineNoBr(e, child);
        } else if (model is DocTitle docTitle) {
            XmlElement e = CreateAndAppend("docTitle", parent);
            foreach (Judgments.IInline child in docTitle.Contents)
                AddInlineNoBr(e, child);
        } else if (model is DocStage docStage) {
            XmlElement e = CreateAndAppend("docStage", parent);
            foreach (Judgments.IInline child in docStage.Contents)
                AddInlineNoBr(e, child);
        } else if (model is DocDate docDate) {
            XmlElement e = CreateAndAppend("docDate", parent);
            string dateValue = ExtractDateFromContent(docDate.Contents);
            if (!string.IsNullOrEmpty(dateValue)) {
                e.SetAttribute("date", dateValue);
            }
            foreach (Judgments.IInline child in docDate.Contents)
                AddInlineNoBr(e, child);
        } else if (model is LeadDepartment leadDept) {
            XmlElement e = CreateAndAppend("inline", parent);
            e.SetAttribute("name", "leadDepartment");
            foreach (Judgments.IInline child in leadDept.Contents)
                AddInlineNoBr(e, child);
        } else if (model is OtherDepartments otherDept) {
            XmlElement e = CreateAndAppend("inline", parent);
            e.SetAttribute("name", "otherDepartments");
            foreach (Judgments.IInline child in otherDept.Contents)
                AddInlineNoBr(e, child);
        } else if (model is DocDepartment docDept) {
            XmlElement e = CreateAndAppend("docProponent", parent);
            foreach (Judgments.IInline child in docDept.Contents)
                AddInlineNoBr(e, child);
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