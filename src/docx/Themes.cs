
using System;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Themes {

    internal static StringValue GetFont(MainDocumentPart main, ThemeFontValues theme) {
        if (theme == ThemeFontValues.MinorHighAnsi)
            return main.ThemePart?.Theme?.ThemeElements?.FontScheme?.MinorFont?.LatinFont.Typeface;
        if (theme == ThemeFontValues.MinorBidi)
            return main.ThemePart?.Theme?.ThemeElements?.FontScheme?.MinorFont?.ChildElements
                .OfType<SupplementalFont>().Where(font => font.Script == "Arab").First().Typeface;
        if (theme == ThemeFontValues.MajorHighAnsi) // EWHC/Comm/2015/1308
            return main.ThemePart?.Theme?.ThemeElements?.FontScheme?.MajorFont?.LatinFont.Typeface;
        if (theme == ThemeFontValues.MajorBidi)    // EWHC/Admin/2018/2941
            return main.ThemePart?.Theme?.ThemeElements?.FontScheme?.MajorFont?.ChildElements
                .OfType<SupplementalFont>().Where(font => font.Script == "Arab").First().Typeface;
        throw new Exception();
    }

    internal static string GetFontName(MainDocumentPart main, ThemeFontValues theme) {
        return GetFont(main, theme)?.Value;
    }

}

}
