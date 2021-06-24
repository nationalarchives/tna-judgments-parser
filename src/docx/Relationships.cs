
using System;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Relationships {

    public static Uri GetUriForRelationshipId(StringValue relationshipId, OpenXmlElement context) {
        OpenXmlElement root = context;
        while (root.Parent is not null)
            root = root.Parent;
        if (root is Document doc) {
            MainDocumentPart main = doc.MainDocumentPart;
            return GetUriForRelationshipId(main, relationshipId);
        }
        if (root is Header header) {
            HeaderPart headerPart = header.HeaderPart;
            return GetUriForRelationshipId(headerPart, relationshipId);
        }
        throw new Exception();
    }

    public static Uri GetUriForRelationshipId(MainDocumentPart main, StringValue relationshipId) {
        OpenXmlPart part = main.Parts.Where(part => part.RelationshipId == relationshipId.Value).First().OpenXmlPart;
        return part.Uri;
    }

    // public static Uri GetUriForRelationshipId(MainDocumentPart main, StringValue relationshipId) {
    //     ExternalRelationship exRel = main.ExternalRelationships.Where(rel => rel.Id == relationshipId).First();
    //     return exRel.Uri;
    // }

    public static Uri GetUriForRelationshipId(HeaderPart header, StringValue relationshipId) {
        OpenXmlPart part = header.Parts.Where(part => part.RelationshipId == relationshipId.Value).First().OpenXmlPart;
        return part.Uri;
        // ExternalRelationship exRel = header.ExternalRelationships.Where(rel => rel.Id == relationshipId).First();
        // return exRel.Uri;
    }

}

}
