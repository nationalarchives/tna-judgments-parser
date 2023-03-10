
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class WDocType : IDocType {

    public IEnumerable<IInline> Contents { get; init; }

    public string Name() {
        string text = IInline.ToString(Contents);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return Regex.Replace(text, @"([A-Z])([A-Z]+)\b", m => m.Groups[1].Value + m.Groups[2].Value.ToLower());
    }

    public WDocType(IEnumerable<IFormattedText> contents) {
        Contents = contents;
    }

}

}
