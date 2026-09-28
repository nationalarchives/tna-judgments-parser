


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse;


class SpecialCharacter : WText
{

    private readonly StringValue font;
    private readonly float? fontSize;

    private SpecialCharacter(string text, RunProperties rProps, StringValue font) : base(text, rProps)
    {
        this.font = font;
    }
    internal SpecialCharacter(string text, RunProperties rProps, string font, float? fSize) : base(text, rProps)
    {
        this.font = font;
        fontSize = fSize;
    }

    internal static SpecialCharacter Make(SymbolChar sym, RunProperties rProps)
    {
        var ch = (char)Int16.Parse(sym.Char, NumberStyles.AllowHexSpecifier);
        if (ch == 0x1E && sym.Font == "Arial Unicode MS")
            return new SpecialCharacter("-", rProps, new StringValue());
        var text = ch.ToString();
        return new SpecialCharacter(text, rProps, sym.Font);
    }

    internal static SpecialCharacter MakeSymEx(OpenXmlElement symEx, RunProperties rProps)
    {
        var ns = "http://schemas.microsoft.com/office/word/2015/wordml/symex";
        var font = symEx.GetAttribute("font", ns).Value;
        var hex = symEx.GetAttribute("char", ns).Value;
        var text = System.Char.ConvertFromUtf32(int.Parse(hex, System.Globalization.NumberStyles.HexNumber));
        return new SpecialCharacter(text, rProps, font);
    }

    public override string FontName
    {
        get
        {
            if (font.HasValue)
                return font.Value;
            if (properties is null)
                return null;
            return DOCX.Fonts.GetFontName(properties);
        }
    }

    public override float? FontSizePt
    {
        get
        {
            if (fontSize.HasValue)
                return fontSize.Value;
            return base.FontSizePt;
        }
    }

}
