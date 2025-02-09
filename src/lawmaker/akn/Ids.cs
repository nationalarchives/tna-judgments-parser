
using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.Legislation.Lawmaker
{

    partial class Builder : AkN.Builder
    {

        protected override string MakeDivisionId(IDivision div)
        {
            if (div is Part)
                return div.Number.Text.Replace("PART", "pt").Replace(' ', '_').ToLower();
            if (div is Prov1)
                return "sec_" + div.Number.Text.TrimEnd('.');
            return null;
        }

    }

}
