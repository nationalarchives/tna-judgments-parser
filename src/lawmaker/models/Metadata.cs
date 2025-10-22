#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

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

    public Dictionary<ReferenceKey, List<Reference>> References { get; } = [];

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
            metadata.AddReference(new Reference(key, title ?? ""));
        }
        foreach (Reference reference in bill
            .Preface
            .OfType<IMetadata>()
            .SelectMany(it => it.Metadata)
            .Where(it => !string.IsNullOrEmpty(it.ShowAs)))
        {
            metadata.AddReference(reference);
        }
        return metadata;
    }

    private Metadata AddReference(Reference reference)
    {
        if (References.TryGetValue(reference.EId, out var val))
        {
            val.Add(reference);
        } else
        {
            References.Add(reference.EId, [reference]);
        }
        return this;
    }


    public XNode Build() =>
        new XElement(akn + "meta",
            new XElement(akn + "references",
                new XAttribute("source", "#author"),
                References.Values
                    .Where(it => it.Count > 0)
                    .OrderBy(it => it.First().EId)
                    .Select(it => it
                        .Select((reference, i) =>
                            reference with {Num = Convert.ToUInt32(i)}))
                    .SelectMany(i => i)
                    .Select(r => r.Build())
            )
        );
}