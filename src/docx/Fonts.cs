
using System;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Fonts {

    internal static string GetFontName(RunProperties rProps) {
        MainDocumentPart main = Main.Get(rProps);
        if (rProps?.RunFonts?.AsciiTheme is not null)
            return DOCX.Themes.GetFontName(main, rProps.RunFonts.AsciiTheme);
        if (rProps.RunStyle?.Val is not null) {
            Style style = Styles.GetStyle(main, rProps.RunStyle?.Val.Value);
            var theme = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.RunFonts?.AsciiTheme);
            if (theme is not null)
                return DOCX.Themes.GetFontName(main, theme);
        }
        if (rProps.RunFonts?.Ascii is not null)
            return rProps.RunFonts.Ascii.Value;
        if (rProps.RunFonts?.ComplexScript is not null)
            return rProps.RunFonts.ComplexScript.Value;
        if (rProps.RunStyle?.Val is not null) {
            Style style = Styles.GetStyle(main, rProps.RunStyle?.Val.Value);
            var value = style.GetInheritedProperty(s => s.StyleRunProperties?.RunFonts?.Ascii);
            if (value is not null)
                return value.Value;
        }
        return null;
    }

    internal static string GetFontName(NumberingSymbolRunProperties rProps) {
        MainDocumentPart main = Main.Get(rProps);
        if (rProps?.RunFonts?.AsciiTheme is not null)
            return DOCX.Themes.GetFontName(main, rProps.RunFonts.AsciiTheme);
        if (rProps.RunFonts?.Ascii is not null)
            return rProps.RunFonts.Ascii.Value;
        if (rProps.RunFonts?.ComplexScript is not null)
            return rProps.RunFonts.ComplexScript.Value;
        return null;
    }

}

}
