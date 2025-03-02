
using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder : AkN.Builder
    {

        protected override string MakeDivisionId(IDivision div)
        {
            if (quoteDepth > 0)
                return null;
            if (div is Part)
                return div.Number.Text.Replace("Part", "pt").Replace(' ', '_').ToLower();
            if (div is Prov1)
                return "sec_" + div.Number.Text.TrimEnd('.');
            return null;
        }

    }

}
