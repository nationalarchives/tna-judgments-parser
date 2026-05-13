
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Relationships {

    private static ILogger logger = Logging.Factory.CreateLogger<UK.Gov.Legislation.Judgments.DOCX.Relationships>();

    // no longer needed in DocumentFormat.OpenXml 3.0; see https://github.com/dotnet/Open-XML-SDK/issues/1637
    // public static readonly RelationshipErrorHandler.Rewriter MalformedUriRewriter = (part, id, uri) => {
    //     logger.LogError("malformed URI: {uri}", uri);
    //     return "http://error?original=" + uri;
    // };

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
        var part = main.Parts.Where(part => part.RelationshipId == relationshipId.Value).FirstOrDefault();
        if (part != default)
            return part.OpenXmlPart?.Uri;
        // IdPartPair changed from class to a readonly struct in DocumentFormat.OpenXml 3.0
        logger.LogCritical("potentially missing image: relationship id = {}", relationshipId);
        return main.ExternalRelationships.Where(r => r.Id == relationshipId.Value).FirstOrDefault()?.Uri; // EWCA/Civ/2003/1067
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
        else if (root is Endnotes endnotes)
            relationships = endnotes.EndnotesPart.HyperlinkRelationships;
        else
            throw new Exception();
        return relationships.Where(r => r.Id == relationshipId).First().Uri;
    }

    public static Uri GetUriForHyperlink(Hyperlink link) {
        return GetUriForHyperlink(link, link.Id);
    }

}

}
