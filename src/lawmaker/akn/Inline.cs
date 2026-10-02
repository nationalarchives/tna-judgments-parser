
using System;
using System.Linq;
using System.Xml;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker;


partial class Builder
{

    protected override void AddInline(XmlElement parent, IInline model)
    {
        inlineDepth += 1;
        try
        {
            AddInlineDispatch(parent, model);
        }
        finally
        {
            inlineDepth -= 1;
        }
    }

    private void AddInlineDispatch(XmlElement parent, IInline model)
    {
        if (model is Def def)
        {
            AddDef(parent, def);
            return;
        }

        if (model is ShortTitle st)
        {
            XmlElement e = CreateAndAppend("shortTitle", parent);
            AddInlines(e, st.Contents);
            return;
        }
        if (model is QuotedText qt)
        {
            AddQuotedText(parent, qt);
            return;
        }
        if (model is InlineQuotedStructure qs)
        {
            AddInlineQuotedStructure(parent, qs);
            return;
        }
        if (model is AppendText at)
        {
            AddAppendText(parent, at);
            return;
        }
        if (model is IImageRef imageRef)
        {
            AddImageRef(parent, imageRef);
            return;
        }
        if (model is null || model is ITab)
        {
            return;
        }
        if (model is IHyperlink2 link && !DisplayTextMatchesHref(link))
        {
            // Links whose visible text is not the URL itself are imported as plain text (LCO-5084)
            AddInlines(parent, link.Contents);
            return;
        }
        base.AddInline(parent, model);
    }

    // Ignores a leading http:// or https:// and a trailing slash, so www.gov.wales matches http://www.gov.wales/
    private static bool DisplayTextMatchesHref(IHyperlink2 link)
    {
        var text = NormaliseUrl(IInline.ToString(link.Contents));
        var href = NormaliseUrl(Uri.UnescapeDataString(link.Href));
        return string.Equals(text, href, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormaliseUrl(string url)
    {
        var normalised = url.Trim();
        if (normalised.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            normalised = normalised["https://".Length..];
        else if (normalised.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            normalised = normalised["http://".Length..];
        return normalised.TrimEnd('/');
    }

    void AddDef(XmlElement parent, Def def)
    {
        XmlElement e = CreateAndAppend("def", parent);
        if (def.StartQuote is not null)
            e.SetAttribute("startQuote", UKNS, def.StartQuote);
        if (def.EndQuote is not null)
            e.SetAttribute("endQuote", UKNS, def.EndQuote);
        AddInlines(e, def.Contents);
    }

    void AddMod(XmlElement parent, Mod mod)
    {
        XmlElement p = CreateAndAppend("p", parent);
        XmlElement modElement = CreateAndAppend("mod", p);
        if (mod.Contents.Any(line => line is IUnknownLine))
        {
            p.SetAttribute("class", UKNS, "unknownImport");
        }

        foreach (IBlock block in mod.Contents)
        {
            if (block is ILine line)
            {
                AddInlines(modElement, line.Contents);
            }
            else
            {
                AddBlocks(modElement, [block]);
            }
        }
    }

    void AddQuotedText(XmlElement parent, QuotedText model)
    {
        XmlElement e = CreateAndAppend("quotedText", parent);
        if (model.StartQuote is not null)
            e.SetAttribute("startQuote", model.StartQuote);
        if (model.EndQuote is not null)
            e.SetAttribute("endQuote", model.EndQuote);
        AddInlines(e, model.Contents);
    }

    void AddInlineQuotedStructure(XmlElement parent, InlineQuotedStructure model)
    {
        XmlElement e = CreateAndAppend("quotedStructure", parent);
        if (model.StartQuote is not null)
            e.SetAttribute("startQuote", model.StartQuote);
        if (model.EndQuote is not null)
            e.SetAttribute("endQuote", model.EndQuote);
        quoteDepth += 1;
        AddDivisions(e, model.Contents);
        quoteDepth -= 1;
    }

    void AddAppendText(XmlElement parent, AppendText model)
    {
        XmlElement e = CreateAndAppend("inline", parent);
        e.SetAttribute("name", "AppendText");
        AddOrWrapText(e, model);
    }

}
