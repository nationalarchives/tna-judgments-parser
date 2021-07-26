


using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class SpecialCharacter : WText {

    private readonly StringValue font;

    private SpecialCharacter(string text, RunProperties rProps, StringValue font) : base(text, rProps) {
        this.font = font;
    }

    internal static SpecialCharacter Make(SymbolChar sym, RunProperties rProps) {
        char ch = (char) Int16.Parse(sym.Char, NumberStyles.AllowHexSpecifier);
        string text = ch.ToString();
        return new SpecialCharacter(text, rProps, sym.Font);
    }
    
    override public string FontName {
        get {
            if (font.HasValue)
                return font.Value;
            if (properties is null)
                return null;
            return DOCX.Fonts.GetFontName(properties);
        }
    }

}

}
