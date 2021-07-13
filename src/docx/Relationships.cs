
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Relationships {

    public static Uri GetUriForImage(StringValue relationshipId, OpenXmlElement context) {
        OpenXmlElement root = context;
        while (root.Parent is not null)
            root = root.Parent;
        if (root is Document doc) {
            MainDocumentPart main = doc.MainDocumentPart;
            return GetUriForImage(main, relationshipId);
        }
        if (root is Header header) {
            HeaderPart headerPart = header.HeaderPart;
            return GetUriForImage(headerPart, relationshipId);
        }
        throw new Exception();
    }

    public static Uri GetUriForImage(MainDocumentPart main, StringValue relationshipId) {
        OpenXmlPart part = main.Parts.Where(part => part.RelationshipId == relationshipId.Value).First().OpenXmlPart;
        return part.Uri;
    }

    public static Uri GetUriForImage(HeaderPart header, StringValue relationshipId) {
        OpenXmlPart part = header.Parts.Where(part => part.RelationshipId == relationshipId.Value).First().OpenXmlPart;
        return part.Uri;
    }

    public static Uri GetUriForHyperlink(OpenXmlElement e, StringValue relationshipId) {
        OpenXmlPartRootElement root = e.Ancestors<OpenXmlPartRootElement>().First();
        IEnumerable<HyperlinkRelationship> relationships;
        if (root is Document document)
            relationships = document.MainDocumentPart.HyperlinkRelationships;
        else if (root is Header header)
            relationships = header.HeaderPart.HyperlinkRelationships;
        else if (root is Footnotes footnotes)
            relationships = footnotes.FootnotesPart.HyperlinkRelationships;
        else
            throw new Exception();
        return relationships.Where(r => r.Id == relationshipId).First().Uri;
    }

    public static Uri GetUriForHyperlink(Hyperlink link) {
        return GetUriForHyperlink(link, link.Id);
    }

}

}
