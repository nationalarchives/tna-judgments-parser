
using System.Xml;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder
    {

        override protected void AddInline(XmlElement parent, IInline model)
        {
            if (model is not ShortTitle st)
            {
                base.AddInline(parent, model);
                return;
            }
            XmlElement e = CreateAndAppend("shortTitle", parent);
            AddInlines(e, st.Contents);
        }

    }

}
