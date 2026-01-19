
using System.Linq;
using System.Xml;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder
    {

        override protected void AddInline(XmlElement parent, IInline model)
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
            if (model is ITab)
            {
                return;
            }
            base.AddInline(parent, model);
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
            if (mod.Contents.Any(line => line is IUnknownLine)) {
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

}
