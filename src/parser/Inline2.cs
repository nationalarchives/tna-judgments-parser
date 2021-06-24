
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

internal class WText : UK.Gov.Legislation.Judgments.IFormattedText {

    public readonly RunProperties properties;
    private readonly string text;

    public WText(string text, RunProperties properties) {
        this.properties = properties;
        this.text = text;
    }
    public WText(Text text, RunProperties properties) {
        this.properties = properties;
        this.text = text.Text;
    }
    public WText(Run run) {
        this.properties = run.RunProperties;
        this.text = run.InnerText;
    }
    public WText(NoBreakHyphen hyphen, RunProperties properties) {
        this.properties = properties;
        this.text = "-";
    }

    public bool? Italic {
        get {
            Italic italic = properties?.Italic;
            if (italic == null)
                return null;
            OnOffValue val = italic.Val;
            if (val == null)
                return true;
            return val.Value;
        }
    }

    public bool? Bold {
        get {
            Bold bold = properties?.Bold;
            if (bold == null)
                return null;
            OnOffValue val = bold.Val;
            if (val == null)
                return true;
            return val.Value;
        }
    }

    public bool? Underline {
        get {
            Underline underline = properties?.Underline;
            if (underline is null)
                return null;
            EnumValue<UnderlineValues> val = underline.Val;
            if (val == null)
                return true;
            if (val.Equals(UnderlineValues.None))
                return false;
            return true;
        }
    }

    public SuperSubValues? SuperSub {
        get {
            VerticalTextAlignment valign = properties?.VerticalTextAlignment;
            if (valign is null)
                return null;
            EnumValue<VerticalPositionValues> val = valign.Val;
            if (val.Equals(VerticalPositionValues.Superscript))
                return SuperSubValues.Superscript;
            if (val.Equals(VerticalPositionValues.Subscript))
                return SuperSubValues.Subscript;
            return SuperSubValues.Baseline;
        }
    }

    public string FontName { get {
        return properties?.RunFonts?.Ascii?.Value;
    } }

    public float? FontSizePt { get {
        string fontSize = properties?.FontSize?.Val;
        if (fontSize is null)
            return null;
        return float.Parse(fontSize) / 2f;
    } }

    public string Text {
        get {
            return text;
        }
    }

}

class WTab : ITab {

    private readonly TabChar tab;

    internal WTab(TabChar tab) {
        this.tab = tab;
    }

}


internal class WNeutralCitation : WText, INeutralCitation {

    public WNeutralCitation(string text, RunProperties properties) : base(text, properties) { }

}

internal class WCourtType : WText, ICourtType {

    public WCourtType(string text, RunProperties props) : base(text, props) { }

    public string Code { get; init; }

}

internal class WCaseNo : WText, ICaseNo {

    public WCaseNo(string text, RunProperties props) : base(text, props) { }

}

internal class WDocDate : IDocDate {

    public WDocDate(IEnumerable<IFormattedText> contents, DateTime date) {
        Contents = contents;
        Date = date.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10);;
    }
    public WDocDate(IFormattedText text, DateTime date) {
        Contents = new List<IFormattedText>(1) { text };
        Date = date.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10);;
    }
    public WDocDate(string text, RunProperties properties, DateTime date) {
        WText wText = new WText(text, properties);
        Contents = new List<IFormattedText>(1) { wText };
        Date = date.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10);;
    }

    public IEnumerable<IFormattedText> Contents { get; }

    public string Date { get; }

}

internal class WParty : WText, IParty {

    public WParty(string text, RunProperties props) : base(text, props) { }

    public WParty(WText text) : base(text.Text, text.properties) { }

}


internal class WLineBreak : ILineBreak {

    internal WLineBreak(Break br) { }

}

}
