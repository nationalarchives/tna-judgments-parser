
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class WDocType1 : WText, IDocType1 {

    public WDocType1(WText text) : base(text.Text, text.properties) { }

    public string Name() {
        string text = Regex.Replace(Text, @"\s+", " ").Trim();
        return Regex.Replace(text, @"([A-Z])([A-Z]+)\b", m => m.Groups[1].Value + m.Groups[2].Value.ToLower());
    }

    internal static string ToTitleCase(string text) {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return Regex.Replace(text, @"([A-Z])([A-Z]+)\b", m => m.Groups[1].Value + m.Groups[2].Value.ToLower());
    }

}

class WDocType2 : IDocType2 {

    public IEnumerable<IInline> Contents { get; init; }

    public WDocType2(IEnumerable<IFormattedText> contents) {
        Contents = contents;
    }

    public string Name() {
        string text = IInline.ToString(Contents);
        return WDocType1.ToTitleCase(text);
    }

}

}
