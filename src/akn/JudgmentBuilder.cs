

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.NationalArchives.CaseLaw {

class JudgmentBuilder : Builder {

    private HashSet<string> Ids = new HashSet<string>();

    override protected string UKNS => Metadata.ukns;

    override protected string MakeDivisionId(IDivision division) {
        if (division.Name != "paragraph")
            return null;
        if (division.Number is null)
            return null;
        Match match = Regex.Match(division.Number.Text, @"^(\d+)\.?$");
        if (!match.Success)
            return null;
        string id = "para_" + match.Groups[1].Value;
        bool isNew = this.Ids.Add(id);
        if (!isNew)
            return null;
        return id;
    }

    public static XmlDocument Build(IJudgment judgment) {
        JudgmentBuilder akn = new JudgmentBuilder();
        akn.Build1(judgment);
        AddHash(akn.doc, akn.UKNS);
        return akn.doc;
    }

}

}
