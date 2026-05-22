#nullable enable

using System;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.DOCX;

internal class Relationships
{
    private static readonly ILogger logger = Logging.Factory.CreateLogger<Relationships>();

    public static Uri? GetUriForImage(StringValue relationshipId, OpenXmlElement context)
    {
        var mainPart = GetMainPartFromElement(context);

        var uri = mainPart.Parts.FirstOrDefault(part => part.RelationshipId == relationshipId.Value).OpenXmlPart?.Uri;

        if (uri != null)
        {
            return uri;
        }

        logger.LogCritical("potentially missing image: relationship id = {RelationshipId}", relationshipId);
        return mainPart.ExternalRelationships.FirstOrDefault(r => r.Id == relationshipId.Value)
                       ?.Uri; // EWCA/Civ/2003/1067
    }

    private static OpenXmlPart GetMainPartFromElement(OpenXmlElement element)
    {
        var root = element.Ancestors<OpenXmlPartRootElement>().Single();

        return root switch
        {
            Document document => document.MainDocumentPart!,
            Header header => header.HeaderPart!,
            Footnotes footnotes => footnotes.FootnotesPart!,
            Endnotes endnotes => endnotes.EndnotesPart!,
            _ => throw new UnknownDocumentPartException(root.GetType().ToString())
        };
    }

    public static Uri GetUriForHyperlink(Hyperlink link)
    {
        var mainPart = GetMainPartFromElement(link);

        return mainPart.HyperlinkRelationships.First(r => r.Id == link.Id).Uri;
    }
}
