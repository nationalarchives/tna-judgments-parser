
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Common;
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
        
        // Add cover page if present
        if (document is IDocumentWithCoverPage docWithCover && docWithCover.CoverPage != null && docWithCover.CoverPage.Any()) {
            AddCoverPage(main, docWithCover.CoverPage);
        }
        
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
        maniThis.SetAttribute("value", data.ExpressionUri + "/data.akn");
        XmlElement maniURI = CreateAndAppend("FRBRuri", manifestation);
        maniURI.SetAttribute("value",  data.ExpressionUri + "/data.akn");
        XmlElement maniDate = CreateAndAppend("FRBRdate", manifestation);
        maniDate.SetAttribute("date", FormatDateAndTime(DateTime.UtcNow));
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
            foreach (Judgments.IInline child in docDate.Contents)
                base.AddInline(e, child);
        } else if (model is DocDepartment docDept) {
            XmlElement e = CreateAndAppend("docDepartment", parent);
            foreach (Judgments.IInline child in docDept.Contents)
                base.AddInline(e, child);
        } else {
            base.AddInline(parent, model);
        }
    }

    protected override string MakeDivisionId(IDivision div) {
        return null;
    }

    /// <summary>
    /// Adds cover page with title and table of contents
    /// </summary>
    private void AddCoverPage(XmlElement main, IEnumerable<IBlock> coverPageBlocks) {
        XmlElement coverPage = doc.CreateElement("coverPage", ns);
        main.AppendChild(coverPage);

        IBlock titleBlock = null;
        ITableOfContents2 tocBlock = null;

        // Separate title and TOC blocks
        foreach (var block in coverPageBlocks) {
            if (block is ITableOfContents2 toc) {
                tocBlock = toc;
            } else {
                titleBlock = block; // Assume first non-TOC block is title
            }
        }

        // Add title block
        if (titleBlock != null) {
            XmlElement titleContainer = doc.CreateElement("block", ns);
            titleContainer.SetAttribute("name", TableOfContentsConstants.TitleBlockName);
            coverPage.AppendChild(titleContainer);

            if (titleBlock is ILine titleLine) {
                XmlElement docTitle = doc.CreateElement("docTitle", ns);
                titleContainer.AppendChild(docTitle);
                AddInlineContents(docTitle, titleLine.Contents);
            }
        }

        // Add TOC
        if (tocBlock != null) {
            XmlElement toc = doc.CreateElement("toc", ns);
            coverPage.AppendChild(toc);

            int level = 1;
            foreach (var tocLine in tocBlock.Contents) {
                XmlElement tocItem = doc.CreateElement("tocItem", ns);
                tocItem.SetAttribute(TableOfContentsConstants.TocItemAttributes.Level, level.ToString());
                tocItem.SetAttribute(TableOfContentsConstants.TocItemAttributes.Href, string.Format(TableOfContentsConstants.SectionHrefTemplate, level));
                toc.AppendChild(tocItem);

                // Extract section number and heading
                string tocText = ExtractTextFromTocLine(tocLine);
                var parts = tocText.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length >= 2) {
                    // Add section number
                    XmlElement tocNum = doc.CreateElement("inline", ns);
                    tocNum.SetAttribute("name", TableOfContentsConstants.InlineNames.TocNum);
                    tocNum.AppendChild(doc.CreateTextNode(parts[0].Trim()));
                    tocItem.AppendChild(tocNum);

                    // Add section heading
                    XmlElement tocHeading = doc.CreateElement("inline", ns);
                    tocHeading.SetAttribute("name", TableOfContentsConstants.InlineNames.TocHeading);
                    tocHeading.AppendChild(doc.CreateTextNode(parts[1].Trim()));
                    tocItem.AppendChild(tocHeading);
                } else {
                    // Fallback: add entire text as heading
                    XmlElement tocHeading = doc.CreateElement("inline", ns);
                    tocHeading.SetAttribute("name", TableOfContentsConstants.InlineNames.TocHeading);
                    tocHeading.AppendChild(doc.CreateTextNode(tocText));
                    tocItem.AppendChild(tocHeading);
                }

                level++;
            }
        }
    }

    /// <summary>
    /// Adds inline contents to an XML element
    /// </summary>
    private void AddInlineContents(XmlElement parent, IEnumerable<IInline> contents) {
        foreach (var inline in contents) {
            if (inline is IFormattedText text) {
                parent.AppendChild(doc.CreateTextNode(text.Text ?? ""));
            }
            // Handle other inline types as needed
        }
    }

    /// <summary>
    /// Extracts text from a TOC line
    /// </summary>
    private string ExtractTextFromTocLine(ILine line) {
        if (line?.Contents == null) {
            return string.Empty;
        }

        var textParts = new List<string>();
        foreach (var inline in line.Contents) {
            if (inline is IFormattedText text) {
                textParts.Add(text.Text ?? string.Empty);
            }
        }

        return string.Join(" ", textParts).Trim();
    }

    /* annexes */

    protected void AddAnnexes(XmlElement main, IDocument document) {
        // Annexes/attachments are not supported in the IA subschema for doc elements
        // Skip them to maintain schema compliance
        // TODO: Consider alternative representation for annexes in doc elements
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