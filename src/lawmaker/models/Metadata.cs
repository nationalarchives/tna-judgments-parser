#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Lawmaker.Headers;

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

    private Dictionary<(ReferenceKey, uint), Reference> References { get; } = [];

    public static void ExtractTitle(Document bill, ILogger logger, Metadata metadata) {
        string? title = "";
        try
        {
            if (bill.Header is NIHeader niHeader && niHeader?.CoverPage?.Blocks is not null)
            {
                title = Judgments.Util.Descendants<ShortTitle>(niHeader?.CoverPage?.Blocks)
                .Select(title => IInline.ToString(title.Contents))
                .FirstOrDefault();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "error converting EMF bitmap record");
        }
        if (!bill.Type.IsSecondaryDocName() && (title is null || title != ""))
        {
            ReferenceKey key = bill.Type.IsEnacted()
                ? ReferenceKey.varActTitle
                : ReferenceKey.varBillTitle;
            metadata.Register(new Reference(key, title ?? ""));
        }
    }

    public Reference? Register(Reference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        uint i = 0;
        while (References.ContainsKey((reference.Key, i)))
        {
            if (reference.ShowAs.Equals(References[(reference.Key, i)].ShowAs, StringComparison.CurrentCultureIgnoreCase))
            {
                reference.Num = i;
                return reference;
            }
            i++;
        }
        reference.Num = i;
        References.Add((reference.Key, i), reference);
        return reference;
    }

    public XNode Build(Document document) =>
        new XElement(akn + "meta",
            new XElement(akn + "references",
                new XAttribute("source", "#author"),
                References
                    .Where(it => !string.IsNullOrEmpty(it.Value.ShowAs))
                    .OrderBy(it => it.Key.Item1)
                    .ThenBy(it => it.Key.Item2)
                    .Select(r => r.Value.Build(document))
            )
        );
}