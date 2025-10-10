#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Representation for the metadata included in Lawmaker AkomaNtoso xml.
/// </summary>
/// <remarks>
/// This parser currently only adds metadata reference fields which it can parse
/// from the document body. Any references which the parser cannot determine
/// are <b>omitted</b>.
/// </remarks>
public class Metadata : IBuildable<XNode>
{

    public Dictionary<ReferenceKey, Reference> References { get; } = [];

    public static Metadata Extract(Document bill, ILogger logger) {
        string? title = "";
        try
        {
            title = Judgments.Util.Descendants<ShortTitle>(bill.CoverPage)
            .Select(title => IInline.ToString(title.Contents))
            .FirstOrDefault();
        }
        catch (Exception e)
        {
            logger.LogError(e, "error converting EMF bitmap record");
        }
        Metadata metadata = new();
        if (!bill.Type.IsSecondaryDocName() && (title is null || title != ""))
        {
            ReferenceKey key = bill.Type.IsEnacted()
                ? ReferenceKey.varActTitle
                : ReferenceKey.varBillTitle;
            metadata.References[key] = new Reference(key, title ?? "");
        }
        foreach (Reference reference in bill
            .Preface
            .OfType<IMetadata>()
            .SelectMany(it => it.GetMetadata())
            .Where(it => !string.IsNullOrEmpty(it.ShowAs)))
        {
            metadata.References[reference.EId] = reference;
        }
        return metadata;
    }


    private readonly static XNamespace akn = XmlExt.AknNamespace;
    public XNode Build() =>
        new XElement(akn + "meta",
            // new XAttribute("xmlns", akn),
            new XElement(akn + "references",
                new XAttribute("source", "#author"),
                References.Values
                    .OrderBy(it => it.EId)
                    .Select(r => r.Build())
            )
        );
}