
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
            if (model is QuotedText qt)
            {
                XmlElement e = CreateAndAppend("quotedText", parent);
                if (qt.StartQuote is not null)
                    e.SetAttribute("startQuote", qt.StartQuote);
                if (qt.EndQuote is not null)
                    e.SetAttribute("endQuote", qt.EndQuote);
                AddInlines(e, qt.Contents);
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

    }

}
