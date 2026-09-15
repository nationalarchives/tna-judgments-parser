using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments.DOCX;

using Fonts = UK.Gov.Legislation.Judgments.DOCX.Fonts;

namespace UK.Gov.Legislation.Judgments.Parse;

internal class WText : IFormattedText
{
    public readonly RunProperties properties;
    private readonly string text;

    public WText(string text, RunProperties properties)
    {
        this.properties = properties;
        this.text = text;
    }

    public WText(Text text, RunProperties properties)
    {
        this.properties = properties;
        this.text = text.Text;
    }

    public WText(Run run)
    {
        properties = run.RunProperties;
        text = run.InnerText;
    }

    public WText(NoBreakHyphen hyphen, RunProperties properties)
    {
        this.properties = properties;
        text = "-";
    }

    public static WText MakeHyphen(RunProperties properties)
    {
        return new WText("-", properties);
    }

#nullable enable
    public string? Style => properties?.RunStyle?.Val;
#nullable disable

    public bool? Italic
    {
        get
        {
            var italic = properties?.Italic;
            if (italic == null)
            {
                return null;
            }

            var val = italic.Val;
            if (val == null)
            {
                return true;
            }

            return val.Value;
        }
    }

    public bool? Bold
    {
        get
        {
            var bold = properties?.Bold;
            if (bold == null)
            {
                return null;
            }

            var val = bold.Val;
            if (val == null)
            {
                return true;
            }

            return val.Value;
        }
    }

    public UnderlineValues2? Underline
    {
        get
        {
            var underline = properties?.Underline;
            if (underline is null)
            {
                return null;
            }

            return Underline2.Get(underline);
        }
    }

    public bool? Uppercase
    {
        get
        {
            var caps = properties?.Caps;
            if (caps is null)
            {
                return null;
            }

            var val = caps.Val;
            if (val == null)
            {
                return true;
            }

            return val.Value;
        }
    }

    public static StrikethroughValue? GetStrikethrough(RunProperties props)
    {
        var single = DOCX.Util.OnOffToBool(props?.Strike);
        if (single.HasValue)
        {
            return single.Value ? StrikethroughValue.Single : StrikethroughValue.None;
        }

        var dbl = DOCX.Util.OnOffToBool(props?.DoubleStrike);
        if (dbl.HasValue)
        {
            return dbl.Value ? StrikethroughValue.Double : StrikethroughValue.None;
        }

        return null;
    }

    public StrikethroughValue? Strikethrough => GetStrikethrough(properties);

    public bool? SmallCaps
    {
        get
        {
            var caps = properties?.SmallCaps;
            if (caps is null)
            {
                return null;
            }

            var val = caps.Val;
            if (val == null)
            {
                return true;
            }

            return val.Value;
        }
    }

    public SuperSubValues? SuperSub
    {
        get
        {
            var valign = properties?.VerticalTextAlignment;
            if (valign is null)
            {
                return null;
            }

            var val = valign.Val;
            if (val.Equals(VerticalPositionValues.Superscript))
            {
                return SuperSubValues.Superscript;
            }

            if (val.Equals(VerticalPositionValues.Subscript))
            {
                return SuperSubValues.Subscript;
            }

            return SuperSubValues.Baseline;
        }
    }

    public virtual string FontName
    {
        get
        {
            if (properties is null)
            {
                return null;
            }

            return Fonts.GetFontName(properties);
        }
        // get {
        //     if (properties?.RunFonts?.AsciiTheme is not null) {
        //         MainDocumentPart main = properties.Ancestors<Document>().First().MainDocumentPart;
        //         return DOCX.Themes.GetFontName(main, properties.RunFonts.AsciiTheme);
        //     }
        //     return properties?.RunFonts?.Ascii?.Value;
        // }
    }

    public virtual float? FontSizePt
    {
        get
        {
            string fontSize = properties?.FontSize?.Val;
            if (fontSize is null)
            {
                return null;
            }

            return float.Parse(fontSize) / 2f;
        }
    }

    public string FontColor
    {
        get
        {
            var color = properties?.Color?.Val?.Value;
            if (color is not null)
            {
                return color;
            }

            return properties?.Shading?.Color?.Value;
        }
    }

    public string BackgroundColor
    {
        get
        {
            var highlight = properties?.Highlight?.Val?.Value;
            if (!highlight.HasValue)
            {
                return properties?.Shading?.Fill?.Value;
            }

            if (highlight == HighlightColorValues.None)
            {
                return null;
            }

            return highlight.ToString().ToLower();
        }
    }

    public bool IsHidden
    {
        get
        {
            var vanish = properties?.Vanish;
            if (vanish is null)
            {
                return false;
            }

            var val = vanish.Val;
            if (val is null)
            {
                return true;
            }

            return val.Value;
        }
    }

    public string Text => text.Replace('\uF020', ' '); // .Replace('\u00A0', ' ')

    internal Tuple<WText, WText> Split(int i)
    {
        var first = text.Substring(0, i);
        var second = text.Substring(i);
        return new Tuple<WText, WText>(new WText(first, properties), new WText(second, properties));
    }

    /// <summary>
    /// This functions adds CSS formatting values derived from a paragraph style to override those from a run style
    /// </summary>
    /// <param name="formatting">a dictionary of CSS key-values pairs, including some ad hoc inline formatting from a run (which is not to be overridden)</param>
    /// <param name="paragraphStyle">the paragraph style</param>
    /// <param name="runStyle">the character style</param>
    /// <param name="main"></param>
    public static void OverrideRunStyleWithParagraphStyle(Dictionary<string, string> formatting, string paragraphStyle,
        string runStyle, MainDocumentPart main)
    {
        if (paragraphStyle is null)
        {
            return;
        }

        if (runStyle is null)
        {
            return;
        }

        var formattingFromParagraphStyle = DOCX.CSS.ExtractCharacterFormatting(main, paragraphStyle);
        var formattingFromCharacterStyle = DOCX.CSS.ExtractCharacterFormatting(main, runStyle);
        MergeParagraphStyleOntoCharacterStyle(formatting, formattingFromParagraphStyle, formattingFromCharacterStyle);
    }

    /// <summary>
    /// Resolves disagreements between a run's character style and its
    /// containing paragraph style into <paramref name="formatting"/>.
    /// <para>
    /// Legacy cascade in this codebase: paragraph style wins. That inverts
    /// the CSS / Word order (a character style is more specific and should
    /// win), but other properties' fixtures have come to depend on it, so
    /// only colour properties — where the inversion produced visibly wrong
    /// output, e.g. a black <c>IARPCChar</c> run overridden to white inside
    /// a <c>Title</c> paragraph — follow the correct character-style-wins
    /// rule. Everything else keeps paragraph-style-wins.
    /// </para>
    /// Ad-hoc inline formatting already in <paramref name="formatting"/> is
    /// never overridden.
    /// </summary>
    internal static void MergeParagraphStyleOntoCharacterStyle(
        Dictionary<string, string> formatting,
        Dictionary<string, string> fromParagraphStyle,
        Dictionary<string, string> fromCharacterStyle)
    {
        foreach (var entry in fromCharacterStyle)
        {
            if (formatting.ContainsKey(entry.Key))
            {
                continue;
            }

            if (!fromParagraphStyle.TryGetValue(entry.Key, out var valueFromParagraphStyle))
            {
                continue;
            }

            if (entry.Value == valueFromParagraphStyle)
            {
                continue;
            }

            var characterStyleWins = entry.Key is "color" or "background-color";
            formatting.Add(entry.Key, characterStyleWins ? entry.Value : valueFromParagraphStyle);
        }
    }

    public virtual Dictionary<string, string> GetCSSStyles(string paragraphStyle)
    {
        var formatting = CSS.GetCSSStyles(this);
        if (properties is not null)
        {
            var main = Main.Get(properties);
            OverrideRunStyleWithParagraphStyle(formatting, paragraphStyle, Style, main);
        }

        return formatting;
    }

    public override string ToString()
    {
        return "WText: " + text;
    }
}

internal class WTab : ITab
{
    internal WTab(TabChar tab) { }
    internal WTab(PositionalTab pTab) { }
    internal WTab(OpenXmlElement e) { }
}

internal class WNeutralCitation(string text, RunProperties properties) : WText(text, properties), INeutralCitation;

internal class WNeutralCitation2 : INeutralCitation2
{
    public IEnumerable<IFormattedText> Contents { get; init; }

    public string Text => IInline.ToString(Contents);
}

internal class WCourtType : WText, ICourtType1
{
    public WCourtType(string text, RunProperties props) : base(text, props) { }

    public WCourtType(WText text, Court court) : base(text.Text, text.properties)
    {
        Code = court.Code;
    }

    public string Code { get; init; }
}

internal class WCourtType2 : ICourtType2
{
    public string Code { get; init; }

    public IEnumerable<IInline> Contents { get; init; }
}

internal class WAppealNo(string text, RunProperties props) : WText(text, props), IAppealNo;

internal class WCaseNo(string text, RunProperties props) : WText(text, props), ICaseNo;

internal class WDate(IEnumerable<IFormattedText> contents, DateTime date) : IDate
{
    public IEnumerable<IFormattedText> Contents { get; } = contents;

    public string Date => date.ToString("s", CultureInfo.InvariantCulture).Substring(0, 10);
}

internal class WDocDate : IDocDate
{
    public WDocDate(IEnumerable<IFormattedText> contents, DateTime date)
    {
        Contents = contents;
        Date = date.ToString("s", CultureInfo.InvariantCulture).Substring(0, 10);
    }

    public WDocDate(IFormattedText text, DateTime date)
    {
        Contents = new List<IFormattedText>(1) { text };
        Date = date.ToString("s", CultureInfo.InvariantCulture).Substring(0, 10);
    }

    public WDocDate(string text, RunProperties properties, DateTime date)
    {
        var wText = new WText(text, properties);
        Contents = new List<IFormattedText>(1) { wText };
        Date = date.ToString("s", CultureInfo.InvariantCulture).Substring(0, 10);
    }

    public WDocDate(WDate wDate)
    {
        Contents = wDate.Contents;
        Date = wDate.Date;
    }

    public IEnumerable<IFormattedText> Contents { get; }

    public string Date { get; }

    public string Name { get; init; } = "judgment";

    public int Priority { get; init; } = 0;
}

internal class WDateTime : IDateTime
{
    public IEnumerable<IFormattedText> Contents { get; init; }

    public DateTime DateTime { get; init; }
}

internal class WParty(string text, RunProperties props) : WText(text, props), IParty1
{
    public WParty(WText text) : this(text.Text, text.properties) { }

    public string Name
    {
        get
        {
            var uppercase = Uppercase ?? false;
            var text = uppercase ? Text.ToUpper() : Text;
            return Party.MakeName(text);
        }
    }

    public PartyRole? Role { get; set; }

    public bool Suppress { get; set; }

    // public bool RonTheApplicationOf { get; set; }
}

internal class WParty2(IEnumerable<ITextOrWhitespace> contents) : IParty2
{
    public string Text => string.Join("", Contents.Select(inline => inline is IFormattedText text ? text.Text : " "));

    public string Name => Party.MakeName(Text);

    public PartyRole? Role { get; set; }

    public IEnumerable<ITextOrWhitespace> Contents { get; init; } = contents;

    public bool Suppress { get; set; }

    // public bool RonTheApplicationOf { get; set; }
}

internal class WRole : IRole
{
    public IEnumerable<IInline> Contents { get; init; }

    public PartyRole Role { get; init; }
}

internal class WSignatureBlock : ISignatureBlock
{
    public string Name { get; init; }

    public IEnumerable<IInline> Content { get; init; }
}

internal class WDocTitle(string text, RunProperties rProps) : WText(text, rProps), IDocTitle
{
    public WDocTitle(WText text) : this(text.Text, text.properties) { }
}

internal class WDocTitle2 : IDocTitle2
{
    public IEnumerable<IInline> Contents { get; init; }

    public static WLine ConvertContents(WLine line)
    {
        if (line.Contents.Count() == 1 && line.Contents.First() is WText first)
        {
            var title1 = new WDocTitle(first);
            return WLine.Make(line, new List<IInline>(1) { title1 });
        }

        var title = new WDocTitle2 { Contents = line.Contents };
        return WLine.Make(line, new List<IInline>(1) { title });
    }
}

internal class WJudge(string text, RunProperties props) : WText(text, props), IJudge
{
    public WJudge(WText text) : this(text.Text, text.properties) { }
}

internal class WLawyer(string text, RunProperties props) : WText(text, props), ILawyer
{
    public WLawyer(WText text) : this(text.Text, text.properties) { }
}

internal abstract class WInlineContainer : IInlineContainer
{
    public IEnumerable<IInline> Contents { get; internal init; }
}

internal class WDocJurisdiction : WInlineContainer, IDocJurisdiction
{
    public string Id => "jurisdiction-" + ShortName.ToLower();

    public string LongName { get; internal init; }

    public string ShortName { get; internal init; }

    public bool Overridden => false;
}

internal class WLocation(string text, RunProperties props) : WText(text, props), ILocation;

internal class WHyperlink1(string text, RunProperties props) : WText(text, props), IHyperlink1
{
    public WHyperlink1(WText text) : this(text.Text, text.properties) { }

    public string Href { get; init; }

    public string ScreenTip { get; init; }
}

internal class WHyperlink2 : IHyperlink2
{
    public IEnumerable<IInline> Contents { get; init; }

    public string Href { get; init; }

    public string ScreenTip { get; init; }
}

internal class InternalLink : IInternalLink
{
    public string Target { get; internal init; }

    public List<IInline> Contents { get; internal init; }

    IList<IInline> IInternalLink.Contents => Contents;
}

internal class WRef(string text, RunProperties props) : WHyperlink1(text, props), IRef
{
    public string Canonical { get; internal init; } // required

    public bool? IsNeutral { get; internal init; }

    public RefType? Type { get; internal init; }
}

// This doesn't fit in with the rest of the design with Refs and is also
// Lawmaker-specific. There is some additional work to be done to
// make this less of a band-aid
public class WInvalidRef : IInvalidRef
{
}

internal abstract class InlineContainer : IInlineContainer
{
    public IEnumerable<IInline> Contents { get; internal init; }
}

internal class WPageReference : InlineContainer, IPageReference
{
}

/* whitespace */

internal class WLineBreak : ILineBreak
{
    internal WLineBreak() { }

    internal WLineBreak(Break br) { }

    internal WLineBreak(CarriageReturn cr) { }

    internal WLineBreak(OpenXmlElement e) { } // should be an unknown br
}

/* markers */

internal class WBookmark : IBookmark
{
    public string Name { get; internal init; }
}
