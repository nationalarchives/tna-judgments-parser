
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.DOCX {

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

    private static IEnumerable<Paragraph> EnumeratePreviousParagraphs(MainDocumentPart main, Paragraph paragraph) {
        if (TryGetParagraphSequence(main, paragraph, out Paragraph[] paragraphs, out int stopExclusive)) {
            for (int i = 0; i < stopExclusive; i++)
                yield return paragraphs[i];
            yield break;
        }

        foreach (Paragraph prev in paragraph.Root().Descendants<Paragraph>()) {
            if (ReferenceEquals(prev, paragraph))
                yield break;
            yield return prev;
        }
    }

    private static bool TryGetParagraphSequence(MainDocumentPart main, Paragraph paragraph, out Paragraph[] paragraphs, out int stopExclusive) {
        ParagraphOrderCacheEntry entry = ParagraphOrderCache.GetValue(main, static part => new ParagraphOrderCacheEntry(part));
        if (entry.Indices.TryGetValue(paragraph, out int idx)) {
            paragraphs = entry.Paragraphs;
            stopExclusive = idx;
            return true;
        }
        paragraphs = Array.Empty<Paragraph>();
        stopExclusive = 0;
        return false;
    }

    private static ParagraphMetadata GetParagraphMetadata(Paragraph paragraph) {
        return ParagraphMetadataCache.GetValue(paragraph, static _ => new ParagraphMetadata());
    }

    private static (int? numId, int ilvl) GetCachedNumbering(MainDocumentPart main, Paragraph paragraph) {
        ParagraphMetadata metadata = GetParagraphMetadata(paragraph);
        if (!metadata.NumberingCached) {
            (metadata.NumberingId, metadata.Ilvl) = Numbering.GetNumberingIdAndIlvl(main, paragraph);
            metadata.NumberingCached = true;
        }
        return (metadata.NumberingId, metadata.Ilvl);
    }

    private static Style? GetCachedStyle(MainDocumentPart main, Paragraph paragraph) {
        ParagraphMetadata metadata = GetParagraphMetadata(paragraph);
        if (!metadata.StyleCached) {
            metadata.Style = Styles.GetStyle(main, paragraph);
            metadata.StyleCached = true;
        }
        return metadata.Style;
    }

    private static int? GetCachedStyleNumberingId(MainDocumentPart main, Paragraph paragraph) {
        ParagraphMetadata metadata = GetParagraphMetadata(paragraph);
        if (!metadata.StyleNumberingCached) {
            metadata.StyleNumberingId = Styles.GetStyleProperty(
                GetCachedStyle(main, paragraph),
                s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);
            metadata.StyleNumberingCached = true;
        }
        return metadata.StyleNumberingId;
    }

    private static bool ShouldSkipHistoryParagraph(Paragraph paragraph) {
        ParagraphMetadata metadata = GetParagraphMetadata(paragraph);
        if (!metadata.SkipFlagsCached) {
            bool skip = Paragraphs.IsDeleted(paragraph)
                || Paragraphs.IsEmptySectionBreak(paragraph)
                || Paragraphs.IsMergedWithFollowing(paragraph);
            metadata.ShouldSkip = skip;
            metadata.SkipFlagsCached = true;
        }
        return metadata.ShouldSkip;
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
        NumberingInstance instance = Numbering.GetNumbering(main, numberingId);
        if (instance is null)
            return null;
        AbstractNum abstractNum = Numbering.GetAbstractNum(main, instance);
        Int32Value abstractNumberId = abstractNum.AbstractNumberId;
        Level baseLevel = Numbering.GetLevel(main, numberingId, baseIlvl);
        if (baseLevel is null)  // [2023] UKFTT 00089 (TC), a very strange case
            return null;
        LevelText format = baseLevel.LevelText;

        /* None */
        if (baseLevel.NumberingFormat.Val == NumberFormatValues.None) { // EWHC/QB/2009/406
            if (string.IsNullOrEmpty(format.Val.Value))
                return "";
            logger.LogDebug("None number format: " + format.Val.Value);
            return Regex.Replace(format.Val.Value, @"%\d", "");  // EWHC/Ch/2014/4092, [2023] EWHC 1526 (Admin)
        }

        /* Bullet */
        if (baseLevel.NumberingFormat.Val == NumberFormatValues.Bullet) {
            if (format.Val.Value == "-")
                return "-";
            if (format.Val.Value == ".")    // EWHC/QB/2018/2066
                return ".";
            if (format.Val.Value == "•")    // EWCA/Civ/2018/2098
                return "•";
            if (format.Val.Value == "·")    // EWHC/Admin/2012/2542
                return format.Val.Value;
            if (format.Val.Value == "o")    // EWCA/Civ/2013/923
                return "◦";
            if (format.Val.Value == "–")    // EWCA/Civ/2013/1015
                return "–"; // en dash ??
            if (format.Val.Value == "")    // \uf0b7 EWHC/QB/2018/2066
                return "•";
            if (format.Val.Value == "")    // \uf0a7 EWCA/Civ/2013/11
                return "•";
            if (format.Val.Value == Char.ConvertFromUtf32(0xf0a0))    // EWHC/Admin/2017/2768
                return Char.ConvertFromUtf32(0x2219);   // small square "bullet operator"
            if (format.Val.Value == Char.ConvertFromUtf32(0xf02d))    // EWHC/Patents/2008/2127
                return Char.ConvertFromUtf32(0x2013);   // en dash (maybe it should be bold?)
            if (format.Val.Value == Char.ConvertFromUtf32(0xf0d8))  // EWHC/QB/2010/484
                return format.Val.Value;
            if (format.Val.Value == Char.ConvertFromUtf32(0xf0de))  // EWHC/Ch/2013/3745
                return Char.ConvertFromUtf32(0x21d2); // Rightwards Double Arrow
            if (format.Val.Value == Char.ConvertFromUtf32(0x2014))    // "em dash"
                return format.Val.Value;
            if (format.Val.Value == Char.ConvertFromUtf32(0xad))    // "soft hyphen" EWHC/Admin/2017/1754
                return "-";
            if (format.Val.Value == "“")    // EWHC/Admin/2017/2461
                return format.Val.Value;
            if (string.IsNullOrEmpty(format.Val.Value)) { // EWCA/Civ/2014/312
                logger.LogDebug("empty bullet");
                return "";
            }
            if (string.IsNullOrWhiteSpace(format.Val.Value)) {
                logger.LogDebug("whitespace bullet: \"" + format.Val.Value + "\"");
                return format.Val.Value;
            }
            if (baseLevel.NumberingSymbolRunProperties?.RunFonts?.Ascii?.Value is not null && baseLevel.NumberingSymbolRunProperties.RunFonts.Ascii.Value.StartsWith("Wingdings"))    // EWHC/Comm/2016/2615
                return format.Val.Value;
            if (format.Val.Value == "●")    // "EWHC/Admin/2021/1249"
                return format.Val.Value;
            if (format.Val.Value == "*")    // "EWHC/Admin/2021/710"
                return format.Val.Value;
            if (format.Val.Value == Char.ConvertFromUtf32(0xf0d5))    // "right arrow?" EWCA/Civ/2004/1294
                return format.Val.Value;
            // if (format.Val.Value == "%1")  // 00223_ukut_iac_2015_mk_sierra leone

            if (format.Val.Value == Char.ConvertFromUtf32(0xf020)) { // [2024] EWHC 2427 (Ch)
                logger.LogWarning("removing bullet xf020");
                return null;
            }

            logger.LogWarning("unknown bullet text: {}", format.Val.Value);
            return Char.ConvertFromUtf32(0x2022);  // default bullet
        }

        /* Other */
        if (string.IsNullOrEmpty(format.Val.Value)) {
            logger.LogDebug("empty number");
            return "";
        }
        if (string.IsNullOrWhiteSpace(format.Val.Value)) {    // EWCA/Civ/2015/1262, WHC/Ch/2008/1978
            logger.LogDebug("whitespace number: \"" + format.Val.Value + "\"");
            return format.Val.Value;
        }
        if (!format.Val.Value.Contains('%')) {    // EWCA/Civ/2003/1769
            logger.LogDebug("static number: \"" + format.Val.Value + "\"");
            return format.Val.Value;
        }

        Match match = Regex.Match(format.Val.Value, "^%(\\d)$");
        if (match.Success) {
            OneCombinator combine = num => num;
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.$");
        if (match.Success) {
            OneCombinator combine = num => num + ".";
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\. $");   // EWHC/Comm/2012/1065
        if (match.Success) {
            OneCombinator combine = num => num + ". ";
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)\\)$");
        if (match.Success) {
            OneCombinator combine = num => "(" + num + ")";
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^%(\\d)\\)$");
        if (match.Success) {
            OneCombinator combine = num => num + ")";
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.\)$");   // EWCA/Civ/2013/1686
        if (match.Success) {
            OneCombinator combine = num => num + ".)";
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\"%(\\d)\\)$");   // EWCA/Civ/2006/939
        if (match.Success) {
            OneCombinator combine = num => "\"" + num + ")";
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)\\)\\.$"); // EWCA/Civ/2012/1411
        if (match.Success) {
            OneCombinator combine = num => "(" + num + ").";
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)\\.\\)$"); // EWCA/Crim/2005/1986
        if (match.Success) {
            OneCombinator combine = num => "(" + num + ".)";
            return One(main, paragraph, numberingId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)a\\)$");
        if (match.Success) {
            OneCombinator combine = num => "(" + num + "a)";
            return One(main, paragraph, numberingId, match, combine);
        }
        
        match = Regex.Match(format.Val.Value, @"^%(\d+)([^%]+)$");   // EWHC/Ch/2017/3634
        if (match.Success) {
            string suffix = match.Groups[2].Value;
            OneCombinator combine = num => num + suffix;
            return One(main, paragraph, numberingId, match, combine);
        }
        // match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)$");   // EWHC/Admin/2015/3437
        // if (match.Success) {
        //     TwoCombinator combine = (num1, num2) => num1 + "." + num2;
        //     return Two(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        // }
        // match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.$");
        // if (match.Success) {
        //     TwoCombinator combine = (num1, num2) => num1 + "." + num2 + ".";
        //     return Two(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        // }
        // match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)$");
        // if (match.Success) {
        //     TwoCombinator combine = (num1, num2) => { return num1 + "." + num2; };
        //     return Two(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        // }
        match = Regex.Match(format.Val.Value, @"^([^%]*)%(\d)([^%]*)%(\d)([^%]*)$");
        if (match.Success) {
            string prefix = match.Groups[1].Value;
            int ilvl1 = int.Parse(match.Groups[2].Value) - 1;
            string middle = match.Groups[3].Value;
            int ilvl2 = int.Parse(match.Groups[4].Value) - 1;
            string suffix = match.Groups[5].Value;
            TwoCombinator combine = (num1, num2) => { return prefix + num1 + middle + num2 + suffix; };
            return Two(main, paragraph, numberingId, ilvl1, ilvl2, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)$");
        if (match.Success) {
            ThreeCombinator three = (num1, num2, num3) => { return num1 + "." + num2 + "." + num3; };
            return Three(main, paragraph, numberingId, match, three);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)[\.)]$");
        if (match.Success) {
            char last = format.Val.Value[^1];
            string three(string num1, string num2, string num3) { return num1 + "." + num2 + "." + num3 + last; }
            return Three(main, paragraph, numberingId, match, three);
        }
        match = Regex.Match(format.Val.Value, @"^\(%(\d)\.%(\d)\.%(\d)$");  // EWHC/Admin/2010/3192
        if (match.Success) {
            ThreeCombinator three = (num1, num2, num3) => "(" + num1 + "." + num2 + "." + num3;
            return Three(main, paragraph, numberingId, match, three);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)\.%(\d)$");
        if (match.Success) {
            FourCombinator four = (num1, num2, num3, num4) => { return num1 + "." + num2 + "." + num3 + "." + num4; };
            return Four(main, paragraph, numberingId,  match, four);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)-%(\d)-%(\d)-%(\d)$");
        if (match.Success) {
            FourCombinator four = (num1, num2, num3, num4) => { return num1 + "-" + num2 + "-" + num3 + "-" + num4; };
            return Four(main, paragraph, numberingId, match, four);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)\.%(\d)\.$");
        if (match.Success) {
            FourCombinator four = (num1, num2, num3, num4) => { return num1 + "." + num2 + "." + num3 + "." + num4 + "."; };
            return Four(main, paragraph, numberingId, match, four);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)\.%(\d)\.%(\d)(\.)?$");
        if (match.Success) {
            FiveCombinator combine = (num1, num2, num3, num4, num5) => { return num1 + "." + num2 + "." + num3 + "." + num4 + "." + num5 + match.Groups[6].Value; };
            return Five(main, paragraph, numberingId, match, combine);
        }

        match = Regex.Match(format.Val.Value, @"^([^%]+)%(\d)$");    // EWHC/Comm/2015/150
        if (match.Success) {
            string prefix = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;
            OneCombinator combine = (num) => { return prefix + num; };
            return One(main, paragraph, numberingId, ilvl, combine);
        }
        match = Regex.Match(format.Val.Value, @"^([^%]+)%(\d+)([^%]+)$");    // EWHC/Ch/2012/1411
        if (match.Success) {
            string prefix = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;
            string suffix = match.Groups[3].Value;
            OneCombinator combine = num => prefix + num + suffix;
            return One(main, paragraph, numberingId, ilvl, combine);
        }

        throw new Exception("unsupported level text: " + format.Val.Value);
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

    internal static int GetStart(MainDocumentPart main, int numberingId, int ilvl) {
        NumberingInstance numbering = Numbering.GetNumbering(main, numberingId);
        int? start = numbering.Descendants<Level>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault()?.StartNumberingValue?.Val?.Value;
        if (start.HasValue)
            return start.Value;
        int? lvlOver = GetStartOverride(numbering, ilvl);
        if (lvlOver.HasValue)
            return lvlOver.Value;
        return GetAbstractStart(main, numbering.AbstractNumId.Val.Value, ilvl);
    }

    private delegate string OneCombinator(string num1);

    private static string One(MainDocumentPart main, Paragraph paragraph, int numberingId, Match match, OneCombinator combine) {
        int ilvl = int.Parse(match.Groups[1].Value) - 1;
        return One(main, paragraph, numberingId, ilvl, combine);
    }
    private static string One(MainDocumentPart main, Paragraph paragraph, int numberingId, int ilvl, OneCombinator combine) {
        Level lvl = Numbering.GetLevel(main, numberingId, ilvl);
        int n = CalculateN(main, paragraph, ilvl);
        string num = FormatN(n, lvl.NumberingFormat);
        return combine(num);
    }

    private delegate string TwoCombinator(string num1, string num2);

    private static string Two(MainDocumentPart main, Paragraph paragraph, int numberingId, Match match, TwoCombinator combine) {
        int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
        int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
        return Two(main, paragraph, numberingId, ilvl1, ilvl2, combine);
    }

    private static string Two(MainDocumentPart main, Paragraph paragraph, int numberingId, int ilvl1, int ilvl2, TwoCombinator combine) {
        Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
        Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
        int n1 = CalculateN(main, paragraph, ilvl1);
        int n2 = CalculateN(main, paragraph, ilvl2);
        string num1 = FormatN(n1, lvl1.NumberingFormat);
        string num2 = FormatN(n2, lvl2.NumberingFormat);
        return combine(num1, num2);
    }

    private delegate string ThreeCombinator(string num1, string num2, string num3);

    private static string Three(MainDocumentPart main, Paragraph paragraph, int numberingId, Match match, ThreeCombinator combine) {
        int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
        int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
        int ilvl3 = int.Parse(match.Groups[3].Value) - 1;
        Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
        Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
        Level lvl3 = Numbering.GetLevel(main, numberingId, ilvl3);
        int n1 = CalculateN(main, paragraph, ilvl1);
        int n2 = CalculateN(main, paragraph, ilvl2);
        int n3 = CalculateN(main, paragraph, ilvl3);
        string num1 = FormatN(n1, lvl1.NumberingFormat);
        string num2 = FormatN(n2, lvl2.NumberingFormat);
        string num3 = FormatN(n3, lvl3.NumberingFormat);
        return combine(num1, num2, num3);
    }

    private delegate string FourCombinator(string num1, string num2, string num3, string num4);

    private static string Four(MainDocumentPart main, Paragraph paragraph, int numberingId, Match match, FourCombinator combine) {
        int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
        int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
        int ilvl3 = int.Parse(match.Groups[3].Value) - 1;
        int ilvl4 = int.Parse(match.Groups[4].Value) - 1;
        Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
        Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
        Level lvl3 = Numbering.GetLevel(main, numberingId, ilvl3);
        Level lvl4 = Numbering.GetLevel(main, numberingId, ilvl4);
        int n1 = CalculateN(main, paragraph, ilvl1);
        int n2 = CalculateN(main, paragraph, ilvl2);
        int n3 = CalculateN(main, paragraph, ilvl3);
        int n4 = CalculateN(main, paragraph, ilvl4);
        string num1 = FormatN(n1, lvl1.NumberingFormat);
        string num2 = FormatN(n2, lvl2.NumberingFormat);
        string num3 = FormatN(n3, lvl3.NumberingFormat);
        string num4 = FormatN(n4, lvl4.NumberingFormat);
        return combine(num1, num2, num3, num4);
    }

    private delegate string FiveCombinator(string num1, string num2, string num3, string num4, string num5);

    private static string Five(MainDocumentPart main, Paragraph paragraph, int numberingId, Match match, FiveCombinator combine) {
        int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
        int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
        int ilvl3 = int.Parse(match.Groups[3].Value) - 1;
        int ilvl4 = int.Parse(match.Groups[4].Value) - 1;
        int ilvl5 = int.Parse(match.Groups[5].Value) - 1;
        Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
        Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
        Level lvl3 = Numbering.GetLevel(main, numberingId, ilvl3);
        Level lvl4 = Numbering.GetLevel(main, numberingId, ilvl4);
        Level lvl5 = Numbering.GetLevel(main, numberingId, ilvl5);
        int n1 = CalculateN(main, paragraph, ilvl1);
        int n2 = CalculateN(main, paragraph, ilvl2);
        int n3 = CalculateN(main, paragraph, ilvl3);
        int n4 = CalculateN(main, paragraph, ilvl4);
        int n5 = CalculateN(main, paragraph, ilvl5);
        string num1 = FormatN(n1, lvl1.NumberingFormat);
        string num2 = FormatN(n2, lvl2.NumberingFormat);
        string num3 = FormatN(n3, lvl3.NumberingFormat);
        string num4 = FormatN(n4, lvl4.NumberingFormat);
        string num5 = FormatN(n5, lvl5.NumberingFormat);
        return combine(num1, num2, num3, num4, num5);
    }

    private static string FormatN(int n, NumberingFormat format) {
        return FormatN(n, format.Val.Value);
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

    [Obsolete]
    private static bool StartOverrideIsOperative(MainDocumentPart main, Paragraph target, int ilvl) {
        (int? targetNumId, int targetIlvl) = Numbering.GetNumberingIdAndIlvl(main, target);
        if (!targetNumId.HasValue)
            throw new Exception();
        if (targetIlvl != ilvl)
            throw new Exception();
        NumberingInstance targetNumbering = Numbering.GetNumbering(main, targetNumId.Value);

        int numIdOfStartOverride = -1;
        var allPrev = target.Root().Descendants<Paragraph>().TakeWhile(p => !object.ReferenceEquals(p, target));
        foreach (Paragraph prev in allPrev) {
            // if (Paragraphs.IsEmptySectionBreak(prev))
            //     continue;
            (int? prevNumId, int prevIlvl) = GetCachedNumbering(main, prev);
            if (!prevNumId.HasValue)
                continue;
            NumberingInstance prevNumbering = Numbering.GetNumbering(main, prevNumId.Value);
            if (prevNumbering is null)
                continue;
            AbstractNum prevAbsNum = Numbering.GetAbstractNum(main, prevNumbering);
            if (prevAbsNum.AbstractNumberId.Value != targetNumbering.AbstractNumId.Val.Value)
                continue;

            if (prevIlvl < ilvl) {
                if (numIdOfStartOverride == targetNumId.Value)
                    return false;
                continue;
            }
            if (prevIlvl > ilvl) {
                // maybe add something here, see below for test38
                continue;
            }
            // prevIlvl == ilvl
            if (prevNumId != numIdOfStartOverride) {
                int? prevOver = GetStartOverride(prevNumbering, ilvl);
                if (prevOver.HasValue)
                    numIdOfStartOverride = prevNumId.Value;
            }
        }
        return true;
    }

    [Obsolete]
    class StartAccumulator {

        private readonly Dictionary<int, Dictionary<int, int>> Map = new();

        internal int? Get(int pevNumId, int prevIlvl) {
            if (!Map.TryGetValue(pevNumId, out Dictionary<int, int> prevAbsStartsByIlvl))
                return null;
            if (!prevAbsStartsByIlvl.TryGetValue(prevIlvl, out int prevAbsStart))
                return null;
            return prevAbsStart;
        }

        internal void Put(int prevNumId, int prevIlvl, int prevAbsStart) {
            if (!Map.ContainsKey(prevNumId))
                Map.Add(prevNumId, new Dictionary<int, int>());
            if (!Map[prevNumId].ContainsKey(prevIlvl))
                Map[prevNumId].Add(prevIlvl, prevAbsStart);
        }

    }

    internal static int CalculateN(MainDocumentPart main, Paragraph paragraph, int ilvl) {
        return Numbering3.CalculateN(main, paragraph, ilvl);
    }

    [Obsolete]
    /// <param name="isHigher">whether the number to be calculated is a higher-level component, such as the 1 in 1.2</param>
    internal static int LegacyCalculateN_KeptForDocumentationPurposesOnly(MainDocumentPart main, Paragraph paragraph, int numberingId, int abstractNumId, int ilvl, bool isHigher = false) {

        int? thisNumIdWithoutStyle = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
        int? thisNumIdOfStyle = GetCachedStyleNumberingId(main, paragraph);
        Style thisStyle = GetCachedStyle(main, paragraph);

        int? start = null;
        int numIdOfStartOverride = -1;
        // -1 means not set
        // -2 meanss trumped, even numbering instance's own start value doesn't matter
        // any positive integer is the numbering id of the previous paragraph that set the value of 'start'
        int? numIdOfStartOverrideStyle = null;
        int? numIdOfStartOverrideWithoutStyle = null;
        bool numOverrideShouldntApplyToStyleOnly = false;
        bool numOverrideShouldntApplyToStyleAndAdHoc = false;

        bool prevContainsNumOverrideAtLowerLevel = false;

        int absStart = GetAbstractStart(main, abstractNumId, ilvl);

        var prevAbsStarts = new StartAccumulator();
        var prevAbsStartsStyle = new StartAccumulator();
        var prevStarts = new StartAccumulator();
        int count = 0;

        foreach (Paragraph prev in EnumeratePreviousParagraphs(main, paragraph)) {

            if (ShouldSkipHistoryParagraph(prev))
                continue;
            (int? prevNumId, int prevIlvl) = Numbering.GetNumberingIdAndIlvl(main, prev);
            if (!prevNumId.HasValue)
                continue;
            NumberingInstance prevNumbering = Numbering.GetNumbering(main, prevNumId.Value);
            if (prevNumbering is null)
                continue;

            // [2024] EWHC 3163 (Comm)
            if (prev.Parent is TableCell tc) {
                var merge = tc.TableCellProperties?.VerticalMerge;
                if (merge is not null) {
                    if (merge.Val is null || merge.Val == MergedCellValues.Continue) {
                        if (string.IsNullOrEmpty(prev.InnerText))
                            continue;
                    }
                }
            }

            AbstractNum prevAbsNum = Numbering.GetAbstractNum(main, prevNumbering);
            int prevAbsNumId = prevAbsNum.AbstractNumberId;
            if (prevAbsNumId != abstractNumId)
                continue;

            int? prevNumIdWithoutStyle = prev.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
            int? prevNumIdOfStyle = GetCachedStyleNumberingId(main, prev);
            Style prevStyle =  GetCachedStyle(main, prev);

            if (prevIlvl < ilvl) {
                if (numIdOfStartOverride == -2) {
                    ;
                } else if (numIdOfStartOverride == numberingId) {
                    start = absStart;
                    numIdOfStartOverride = -2;
                } else if (start is null) {
                    ;
                } else {
                    start = null;
                    numIdOfStartOverride = -1;
                }

                // the strange case of [2023] EWCA Civ 657 (test60)
                // prevIlvl > 0 needed for test76
                if (prevIlvl > 0 && !prevNumIdWithoutStyle.HasValue && !thisNumIdWithoutStyle.HasValue) {
                    string prevBasedOn = prevStyle?.BasedOn?.Val?.Value;
                    string thisStyleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    int? prevStyleIlvl = prevStyle?.StyleParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
                    if (prevBasedOn is not null && prevBasedOn == thisStyleId && prevStyleIlvl.HasValue)
                        continue;
                }
                // for test 94
                if (prevIlvl > 0) {
                    bool prevIsOutlineNumbered = prevStyle?.StyleParagraphProperties?.OutlineLevel?.Val?.Value is not null;
                    bool thisIsOutlineNumbered = thisStyle?.StyleParagraphProperties?.OutlineLevel?.Val?.Value is not null;
                    if (prevIsOutlineNumbered && !thisIsOutlineNumbered)
                        continue;
                }

                count = 0;
                continue;
            }

            if (prevIlvl > ilvl) {

                if (count == 0) // test35
                    count += 1;

                int? prevStartOverride = GetStartOverride(prevNumbering, prevIlvl);
                if (prevStartOverride.HasValue) { // see tests 38, 47, 66, 67, & 77

                    if (prevNumIdWithoutStyle.HasValue && prevNumIdWithoutStyle.Value > 1)
                        prevAbsStarts.Put(prevNumIdWithoutStyle.Value, prevIlvl, absStart);
                    if (prevNumIdOfStyle.HasValue && prevNumIdOfStyle.Value > 1)
                        prevAbsStartsStyle.Put(prevNumIdOfStyle.Value, prevIlvl, absStart);

                    bool styleNumbersDontMatch = prevNumIdOfStyle.HasValue && thisNumIdOfStyle.HasValue && prevNumIdOfStyle.Value != thisNumIdOfStyle.Value;
                    if (styleNumbersDontMatch) { // test 67
                        start = absStart;
                        numIdOfStartOverride = -2;
                    }

                    // prevNumIdWithoutStyle.HasValue && ... is not good enough
                    if (prevNumIdWithoutStyle == numberingId && prevStartOverride.Value > 1)
                        prevContainsNumOverrideAtLowerLevel = true;
                    if (!isHigher && prevNumIdWithoutStyle.HasValue) // for test 91, !isHight for test 68
                        prevStarts.Put(prevNumIdWithoutStyle.Value, prevIlvl, prevStartOverride.Value);
                }

                continue;
            }

            // now prevIlvl == ilvl

            if (prevNumIdWithoutStyle.HasValue) {
                var prevAbsStart = prevAbsStarts.Get(prevNumIdWithoutStyle.Value, prevIlvl + 2);
                if (prevAbsStart.HasValue) {
                    start = prevAbsStart.Value;
                    numIdOfStartOverride = -2;
                }
            }
            if (prevNumIdWithoutStyle.HasValue) {
                var prevStart = prevStarts.Get(prevNumIdWithoutStyle.Value, ilvl + 1);
                if (prevStart.HasValue) {
                    start = prevStart.Value;
                    numIdOfStartOverride = -2;
                }
            }

            bool numOverrideShouldntApplyToPrev1 = numOverrideShouldntApplyToStyleOnly && !prevNumIdWithoutStyle.HasValue && prevNumIdOfStyle == numIdOfStartOverrideStyle;
            bool numOverrideShouldntApplyToPrev2 = numOverrideShouldntApplyToStyleAndAdHoc &&
                prevNumIdOfStyle.HasValue && prevNumIdOfStyle == numIdOfStartOverrideStyle &&
                prevNumIdWithoutStyle.HasValue && numIdOfStartOverrideWithoutStyle.HasValue && prevNumIdWithoutStyle.Value != numIdOfStartOverrideWithoutStyle.Value;

            int? prevOver = GetStartOverride(prevNumbering, ilvl);
            if (prevOver.HasValue && prevOver.Value > 1) {
                numOverrideShouldntApplyToPrev1 = false;
                numOverrideShouldntApplyToPrev2 = false;
            }

            if (!numOverrideShouldntApplyToPrev1 && !numOverrideShouldntApplyToPrev2 && prevNumId.Value != numIdOfStartOverride && numIdOfStartOverride != -2) {
                if (!isHigher || prevNumIdOfStyle.HasValue) {  // test68
                    if (prevOver.HasValue && StartOverrideIsOperative(main, prev, prevIlvl)) {
                        start = prevOver.Value;
                        numIdOfStartOverride = prevNumId.Value;
                        if (prevNumIdOfStyle == numIdOfStartOverrideStyle && prevNumIdWithoutStyle.HasValue && !numIdOfStartOverrideWithoutStyle.HasValue) {
                            numOverrideShouldntApplyToStyleOnly = true;
                            // When the number override comes from a paragraph that has a style but also numbering of its own,
                            // then the number override shouldn't apply to paragraphs with only that style. See test 86.
                        }
                        if (prevIlvl == 0 && prevOver.Value > 1 && prevNumIdOfStyle.HasValue && prevNumIdOfStyle.Value == thisNumIdOfStyle && prevNumIdWithoutStyle.HasValue && thisNumIdWithoutStyle.HasValue && prevNumIdWithoutStyle.Value != thisNumIdWithoutStyle.Value)
                            numOverrideShouldntApplyToStyleAndAdHoc = true;

                        numIdOfStartOverrideStyle = prevNumIdOfStyle;
                        numIdOfStartOverrideWithoutStyle = prevNumIdWithoutStyle;
                        if (!prevContainsNumOverrideAtLowerLevel)  // tests 37 and 87 need this condition
                            count = 0;
                    }
                }
            }

            count += 1;
        }

        if (thisNumIdWithoutStyle.HasValue) {
            var prevAbsStart = prevAbsStarts.Get(thisNumIdWithoutStyle.Value, ilvl + 2);
            if (prevAbsStart.HasValue) {
                start = prevAbsStart.Value;
                numIdOfStartOverride = -2;
            }
        }
        if (thisNumIdWithoutStyle.HasValue) {
            var prevStart = prevStarts.Get(thisNumIdWithoutStyle.Value, ilvl + 1);
            if (prevStart.HasValue) {
                start = prevStart.Value;
                numIdOfStartOverride = -2;
            }
        }

        if (isHigher) // why ???
            prevContainsNumOverrideAtLowerLevel = true;

        bool numOverrideShouldntApply1 = numOverrideShouldntApplyToStyleOnly && !thisNumIdWithoutStyle.HasValue && thisNumIdOfStyle == numIdOfStartOverrideStyle;
        bool numOverrideShouldntApply2 = numOverrideShouldntApplyToStyleAndAdHoc &&
            thisNumIdOfStyle.HasValue && thisNumIdOfStyle == numIdOfStartOverrideStyle &&
            thisNumIdWithoutStyle.HasValue && numIdOfStartOverrideWithoutStyle.HasValue && thisNumIdWithoutStyle.Value != numIdOfStartOverrideWithoutStyle.Value;

        int? over = GetStartOverride(main, numberingId, ilvl);
        if (over.HasValue && over.Value > 1) {
            numOverrideShouldntApply1 = false;
            numOverrideShouldntApply2 = false;
        }

        if (!numOverrideShouldntApply1 && !numOverrideShouldntApply2 && numberingId != numIdOfStartOverride && numIdOfStartOverride != -2) {
            if (start is null)
                start = GetStart(main, numberingId, ilvl);
            else if (over.HasValue)
                start = over.Value;
            if (over.HasValue && !prevContainsNumOverrideAtLowerLevel)  // test 37 (and 87?) needs second condition
                count = 0;
        }
        if (!start.HasValue)
            start = 1;
        return count + start.Value;
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
            int n = CalculateN(main, paragraph, ilvl);
            n -= 1;
            string num = FormatN(n, lvl.NumberingFormat);
            return num + ".";
        }
        throw new Exception();
    }
}

}
