
using System.Xml;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder
    {

        override protected void AddInline(XmlElement parent, IInline model)
        {
            if (model is ShortTitle st)
            {
                XmlElement e = CreateAndAppend("shortTitle", parent);
                AddInlines(e, st.Contents);
                return;
            }
            if (model is Mod mod)
            {
                AddMod(parent, mod);
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
            if (model is Def def)
            {
                XmlElement e = CreateAndAppend("def", parent);
                if (def.StartQuote is not null)
                    e.SetAttribute("startQuote", UKNS, def.StartQuote);
                if (def.EndQuote is not null)
                    e.SetAttribute("endQuote", UKNS, def.EndQuote);
                AddInlines(e, def.Contents);
                return;
            }
            base.AddInline(parent, model);
        }

        void AddMod(XmlElement parent, Mod model)
        {
            XmlElement mod = CreateAndAppend("mod", parent);
            AddInlines(mod, model.Contents);
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
