using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.DOCX;

readonly struct NumberInfo {
    internal string Number { get; init; }
    internal NumberingSymbolRunProperties Props { get; init; }
}

class Numbering2 {

    private static ILogger logger = Logging.Factory.CreateLogger<DOCX.Numbering2>();

    private sealed class ParagraphOrderCacheEntry {
        internal Paragraph[] Paragraphs { get; }
        internal Dictionary<Paragraph, int> Indices { get; }

        internal ParagraphOrderCacheEntry(MainDocumentPart main) {
            Paragraphs = main.Document?.Descendants<Paragraph>().ToArray() ?? Array.Empty<Paragraph>();
            Indices = new Dictionary<Paragraph, int>(ReferenceEqualityComparer<Paragraph>.Instance);
            for (int i = 0; i < Paragraphs.Length; i++)
                Indices[Paragraphs[i]] = i;
        }
    }

    private sealed class ParagraphMetadata {
        internal bool NumberingCached;
        internal int? NumberingId;
        internal int Ilvl;

        internal bool StyleCached;
        internal Style? Style;

        internal bool StyleNumberingCached;
        internal int? StyleNumberingId;

        internal bool SkipFlagsCached;
        internal bool ShouldSkip;
    }

    private static readonly ConditionalWeakTable<MainDocumentPart, ParagraphOrderCacheEntry> ParagraphOrderCache = new();
    private static readonly ConditionalWeakTable<Paragraph, ParagraphMetadata> ParagraphMetadataCache = new();

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class {
        internal static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private static ParagraphMetadata GetParagraphMetadata(Paragraph paragraph) {
        return ParagraphMetadataCache.GetValue(paragraph, static _ => new ParagraphMetadata());
    }

    private static Style? GetCachedStyle(MainDocumentPart main, Paragraph paragraph) {
        ParagraphMetadata metadata = GetParagraphMetadata(paragraph);
        if (!metadata.StyleCached) {
            metadata.Style = Styles.GetStyle(main, paragraph);
            metadata.StyleCached = true;
        }
        return metadata.Style;
    }

    public static bool HasOwnNumber(Paragraph paragraph) {
        MainDocumentPart main = Main.Get(paragraph);
        (int? numId, int ilvl) = Numbering.GetNumberingIdAndIlvl(main, paragraph);
        if (!numId.HasValue)
            return false;
        if (numId.Value == 0)
            return false;
        Level level = Numbering.GetLevel(main, numId.Value, ilvl);
        if (level == null)
            return false;
        var vanish = level.NumberingSymbolRunProperties?.ChildElements.OfType<Vanish>().FirstOrDefault();
        if (vanish is null)
            return true;
        if (vanish.Val is null)
            return false;
        return !vanish.Val.Value;
    }
    // has style number and that number is not canceled
    public static bool HasEffectiveStyleNumber(Paragraph paragraph) {
        int? numId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
        if (numId.HasValue && numId.Value == 0)
            return false;
        var main = Main.Get(paragraph);
        Style style = GetCachedStyle(main, paragraph);
        if (style is null)
            style = Styles.GetDefaultParagraphStyle(main);
        if (style is null)
            return false;
        numId = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);
        return numId.HasValue && numId.Value != 0;
    }

    public static NumberInfo? GetFormattedNumber(MainDocumentPart main, Paragraph paragraph) {

        (int? numId, int ilvl) = Numbering.GetNumberingIdAndIlvl(main, paragraph);
        if (!numId.HasValue)
            return null;

        Level level = Numbering.GetLevel(main, numId.Value, ilvl);

        string formatted = Magic2(main, paragraph, numId.Value, ilvl);
        if (string.IsNullOrEmpty(formatted))
            return null;

        return new NumberInfo() { Number = formatted, Props = level.NumberingSymbolRunProperties };
    }

    private static string Magic2(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl) {
        if (Numbering.GetNumbering(main, numberingId) is null)
            return null;
        var baseLevel = Numbering.GetLevel(main, numberingId, baseIlvl);
        if (baseLevel is null)  // [2023] UKFTT 00089 (TC), a very strange case
            return null;

        var numberingFormatVal = baseLevel.NumberingFormat?.Val;
        var docxLevelTextFormat = baseLevel.LevelText?.Val?.Value;

        /* None */
        if (numberingFormatVal == NumberFormatValues.None) { // EWHC/QB/2009/406
            if (string.IsNullOrEmpty(docxLevelTextFormat))
                return "";
            logger.LogDebug("None number format: {LevelFormat}", docxLevelTextFormat);
            return Regex.Replace(docxLevelTextFormat, @"%\d", "");  // EWHC/Ch/2014/4092, [2023] EWHC 1526 (Admin)
        }

        /* Bullet */
        if (numberingFormatVal == NumberFormatValues.Bullet)
        {
            if (baseLevel.NumberingSymbolRunProperties?.RunFonts?.Ascii?.Value is not null &&
                baseLevel.NumberingSymbolRunProperties.RunFonts.Ascii.Value.StartsWith("Wingdings"))

            {
                // EWHC/Comm/2016/2615
                return docxLevelTextFormat;
            }

            return TransformBullet(docxLevelTextFormat);
        }

        /* Other */
        if (string.IsNullOrEmpty(docxLevelTextFormat)) {
            logger.LogDebug("empty number");
            return "";
        }
        if (string.IsNullOrWhiteSpace(docxLevelTextFormat)) {    // EWCA/Civ/2015/1262, WHC/Ch/2008/1978
            logger.LogDebug("whitespace number: \"{WhitespaceNumber}\"", docxLevelTextFormat);
            return docxLevelTextFormat;
        }
        if (!docxLevelTextFormat.Contains('%')) {    // EWCA/Civ/2003/1769
            logger.LogDebug("static number: \"{StaticNumber}\"", docxLevelTextFormat);
            return docxLevelTextFormat;
        }

        /* Transform numbers */
        var result = Regex.Replace(docxLevelTextFormat, @"%(\d)+",
            m => TransformLevelNumber(m, main, paragraph, numberingId));

        return result;
    }

    private static string TransformBullet(string docxLevelTextFormat)
    {
        switch (docxLevelTextFormat)
        {
            case "-": // EWHC/QB/2018/2066
                return "-";

            case ".": // EWCA/Civ/2018/2098
                return ".";

            case "•": // EWCA/Civ/2013/923
                return "•";

            case "o": // EWCA/Civ/2013/1015
                return "◦";

            case "–": // \uf0b7 EWHC/QB/2018/2066
                return "–"; // en dash ??

            case "": // \uf0a7 EWCA/Civ/2013/11
            case "":
                return "•";

            case "·": // EWHC/Admin/2012/2542
            case "●": // "EWHC/Admin/2021/1249"
            case "*": // "EWHC/Admin/2021/710"
            case "“": // EWHC/Admin/2017/2461
                return docxLevelTextFormat;
        }

        // UTF32 character - replace
        if (docxLevelTextFormat == char.ConvertFromUtf32(0xf0a0)) // EWHC/Admin/2017/2768
        {
            return char.ConvertFromUtf32(0x2219); // small square "bullet operator"
        }

        if (docxLevelTextFormat == char.ConvertFromUtf32(0xf02d)) // EWHC/Patents/2008/2127
        {
            return char.ConvertFromUtf32(0x2013); // en dash (maybe it should be bold?)
        }

        if (docxLevelTextFormat == char.ConvertFromUtf32(0xf0de)) // EWHC/Ch/2013/3745
        {
            return char.ConvertFromUtf32(0x21d2); // Rightwards Double Arrow
        }

        if (docxLevelTextFormat == char.ConvertFromUtf32(0xad)) // "soft hyphen" EWHC/Admin/2017/1754
        {
            return "-";
        }

        // UTF32 character - keep original
        if (docxLevelTextFormat == char.ConvertFromUtf32(0xf0d8)) // EWHC/QB/2010/484
        {
            return docxLevelTextFormat;
        }

        if (docxLevelTextFormat == char.ConvertFromUtf32(0x2014)) // "em dash"
        {
            return docxLevelTextFormat;
        }

        if (docxLevelTextFormat == char.ConvertFromUtf32(0xf0d5)) // "right arrow?" EWCA/Civ/2004/1294
        {
            return docxLevelTextFormat;
        }

        // Misc bullets
        if (string.IsNullOrEmpty(docxLevelTextFormat))
        {
            // EWCA/Civ/2014/312
            logger.LogDebug("empty bullet");
            return "";
        }

        if (string.IsNullOrWhiteSpace(docxLevelTextFormat))
        {
            logger.LogDebug("whitespace bullet: \"{Bullet}\"", docxLevelTextFormat);
            return docxLevelTextFormat;
        }

        // Unsupported bullet
        if (docxLevelTextFormat == char.ConvertFromUtf32(0xf020))
        {
            // [2024] EWHC 2427 (Ch)
            logger.LogWarning("removing bullet xf020");
            return null;
        }

        // Unknown bullet
        logger.LogWarning("unknown bullet text: {Bullet}", docxLevelTextFormat);
        return char.ConvertFromUtf32(0x2022); // default bullet
    }

    internal static string TransformLevelNumber(Match m, MainDocumentPart main, Paragraph paragraph, int numberingId)
    {
        var levelIndex = int.Parse(m.Groups[1].Value) - 1;
        var lvl = Numbering.GetLevel(main, numberingId, levelIndex);
        var n = Numbering3.CalculateN(main, paragraph, levelIndex);

        return FormatN(n, lvl.NumberingFormat.Val.Value);
    }
    
    internal static int GetAbstractStart(MainDocumentPart main, int absNumId, int ilvl) {
        AbstractNum abs = main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<AbstractNum>()
            .Where(a => a.AbstractNumberId.Value == absNumId)
            .First();
        Level level = abs.ChildElements
            .OfType<Level>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();  // does not exist in EWHC/Ch/2003/2902
        return level?.StartNumberingValue?.Val ?? 1;
    }

    private static string FormatN(int n, NumberFormatValues format) {
        if (format == NumberFormatValues.Decimal)
            return n.ToString();
        if (format == NumberFormatValues.LowerLetter)
            return Util.ToLowerLetter(n);
        if (format == NumberFormatValues.UpperLetter)
            return Util.ToUpperLetter(n);
        if (format == NumberFormatValues.LowerRoman)
            return Util.ToLowerRoman(n);
        if (format == NumberFormatValues.UpperRoman)
            return Util.ToUpperRoman(n);
        if (format == NumberFormatValues.DecimalZero)
            return n.ToString("D2");
        if (format == NumberFormatValues.None)  // EWHC/Ch/2015/3490
            return "";
        throw new Exception("unsupported numbering format: " + format.ToString());
    }

    public static string FormatNumberAbstract(int absNumId, int ilvl, int n, MainDocumentPart main) {
        Level level = Numbering.GetLevelAbstract(main, absNumId, ilvl);
        NumberFormatValues numFormat = level.NumberingFormat.Val.Value;
        string lvlText = level.LevelText.Val.Value;
        return Format(n, numFormat, lvlText);
    }

    public static string FormatNumber(int numId, int ilvl, int n, MainDocumentPart main) {
        Level level = Numbering.GetLevel(main, numId, ilvl);
        NumberFormatValues numFormat = level.NumberingFormat.Val.Value;
        string lvlText = level.LevelText.Val.Value;
        return Format(n, numFormat, lvlText);
    }

    public static string FormatNumber(string name, int ilvl, int n, MainDocumentPart main) {
        AbstractNum absNum = Numbering.GetAbstractNum(main, name);
        Level level = absNum.ChildElements.OfType<Level>().Where(l => l.LevelIndex.Value == ilvl).First();
        NumberFormatValues numFormat = level.NumberingFormat.Val.Value;
        string lvlText = level.LevelText.Val.Value;
        return Format(n, numFormat, lvlText);
    }

    private static string Format(int n, NumberFormatValues numFormat, string lvlText) {
        string num = FormatN(n, numFormat);
        Match match = Regex.Match(lvlText, "^%(\\d)$");
        if (match.Success)
            return num;
        match = Regex.Match(lvlText, "^%(\\d)\\.$");
        if (match.Success)
            return num + ".";
        match = Regex.Match(lvlText, "^\\(%(\\d)\\)$");
        if (match.Success)
            return "(" + num + ")";
        match = Regex.Match(lvlText, "^%(\\d)\\)$");
        if (match.Success)
            return num + ")";
        throw new Exception("unsupported level text: " + lvlText);
    }

    internal static int? GetStartOverride(MainDocumentPart main, int numberingId, int ilvl) {
        NumberingInstance numbering = Numbering.GetNumbering(main, numberingId);
        return GetStartOverride(numbering, ilvl);
    }
    private static int? GetStartOverride(NumberingInstance numbering, int ilvl) {
        LevelOverride over = numbering?.ChildElements.OfType<LevelOverride>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();
        return over?.StartOverrideNumberingValue?.Val?.Value;
    }

    internal static int CalculateN(MainDocumentPart main, Paragraph paragraph, int ilvl) {
        return Numbering3.CalculateN(main, paragraph, ilvl);
    }

    // for REF \w -- see EWHC/Comm/2018/1368
    public static string GetNumberInFullContext(MainDocumentPart main, Paragraph paragraph) {
        (int? numId, int ilvl) = Numbering.GetNumberingIdAndIlvl(main, paragraph);
        if (!numId.HasValue)
            return null;
        string formatted = Magic2(main, paragraph, numId.Value, ilvl);
        while (ilvl > 0) {
            ilvl -= 1;
            string higherLevel = Magic3(main, paragraph, numId.Value, ilvl);  // assumes number format involves only one level
            formatted = higherLevel + formatted;
        }
        return formatted;
    }
    /* gets formatting number of parent level (by subtracting 1 from n) */
    /* assumes number format involves only one level */
    private static string Magic3(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl) {
        NumberingInstance instance = Numbering.GetNumbering(main, numberingId);
        AbstractNum abstractNum = Numbering.GetAbstractNum(main, instance);
        Int32Value abstractNumberId = abstractNum.AbstractNumberId;
        Level baseLevel = Numbering.GetLevel(main, numberingId, baseIlvl);
        LevelText format = baseLevel.LevelText;
        Match match = Regex.Match(format.Val.Value, "^%(\\d)\\.$");
        if (match.Success) {
            int ilvl = int.Parse(match.Groups[1].Value) - 1;
            if (ilvl != baseIlvl)
                throw new Exception();
            Level lvl = Numbering.GetLevel(main, numberingId, ilvl);    // this is redundant
            int n = Numbering3.CalculateN(main, paragraph, ilvl);
            n -= 1;
            string num = FormatN(n, lvl.NumberingFormat.Val.Value);
            return num + ".";
        }
        throw new Exception();
    }
}
