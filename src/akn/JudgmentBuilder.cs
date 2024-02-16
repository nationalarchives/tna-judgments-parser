
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.NationalArchives.CaseLaw {

class JudgmentBuilder : Builder {

    override protected string UKNS => Metadata.ukns;

    override protected string MakeDivisionId(IDivision division) {
        int hash = division.GetHashCode();
        DivHashToId.TryGetValue(hash, out string id);
        return id;
    }

    public static XmlDocument Build(IJudgment judgment) {
        JudgmentBuilder builder = new(judgment);
        builder.Build1(judgment);
        AddHash(builder.doc, builder.UKNS);
        return builder.doc;
    }

    private readonly IJudgment Judgment;

    private JudgmentBuilder(IJudgment judgment) {
        Judgment = judgment;
        IEnumerable<IDivision> divisions = judgment.Body.SelectMany(dec => dec.Contents);
        GenerateIds(divisions);
    }

    /* generate ids */

    private readonly HashSet<string> UsedParagraphIds = new();

    private string MakeParagraphnId(IDivision division) {
        if (division.Name != "paragraph")
            return null;
        if (division.Number is null)
            return null;
        Match match = Regex.Match(division.Number.Text, @"^(\d+)\.?$");
        if (!match.Success)
            return null;
        string id = "para_" + match.Groups[1].Value;
        bool isNew = this.UsedParagraphIds.Add(id);
        if (!isNew)
            return null;
        return id;
    }

    readonly Dictionary<int, string> DivHashToId = new();

    private void GenerateIds(IEnumerable<IDivision> divisions, string parent = null) {
        int lvl = 0;
        foreach (var div in divisions) {
            int hash = div.GetHashCode();
            string para = MakeParagraphnId(div);
            if (para is not null) {
                DivHashToId.Add(hash, para);
                continue;
            }
            string prefix = parent ?? "lvl";
            lvl += 1;
            string id = prefix + "_"+ lvl;
            DivHashToId.Add(hash, id);
            if (div.Name == "paragraph")
                continue;
            if (div is not IBranch branch)
                continue;
            GenerateIds(branch.Children, id);
        }
    }

}

}
