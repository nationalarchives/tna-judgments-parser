
using System;
using System.Collections.Generic;
using System.Linq;
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

    public static bool HasOwnNumber(Paragraph paragraph) {
        int? numId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
        if (!numId.HasValue)
            return false;
        if (numId.Value == 0)
            return false;
        var vanish = paragraph.ParagraphProperties?.ParagraphMarkRunProperties?.ChildElements.OfType<Vanish>().FirstOrDefault();
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
        Style style = Styles.GetStyle(main, paragraph);
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
        string magic = Magic2(main, paragraph, numId.Value, ilvl);
        if (string.IsNullOrEmpty(magic))
            return null;
        Level level = Numbering.GetLevel(main, numId.Value, ilvl);
        return new NumberInfo() { Number = magic, Props = level.NumberingSymbolRunProperties };
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
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.$");
        if (match.Success) {
            OneCombinator combine = num => num + ".";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\. $");   // EWHC/Comm/2012/1065
        if (match.Success) {
            OneCombinator combine = num => num + ". ";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)\\)$");
        if (match.Success) {
            OneCombinator combine = num => "(" + num + ")";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^%(\\d)\\)$");
        if (match.Success) {
            OneCombinator combine = num => num + ")";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.\)$");   // EWCA/Civ/2013/1686
        if (match.Success) {
            OneCombinator combine = num => num + ".)";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\"%(\\d)\\)$");   // EWCA/Civ/2006/939
        if (match.Success) {
            OneCombinator combine = num => "\"" + num + ")";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)\\)\\.$"); // EWCA/Civ/2012/1411
        if (match.Success) {
            OneCombinator combine = num => "(" + num + ").";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)\\.\\)$"); // EWCA/Crim/2005/1986
        if (match.Success) {
            OneCombinator combine = num => "(" + num + ".)";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)a\\)$");
        if (match.Success) {
            OneCombinator combine = num => "(" + num + "a)";
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }
        
        match = Regex.Match(format.Val.Value, @"^%(\d+)([^%]+)$");   // EWHC/Ch/2017/3634
        if (match.Success) {
            string suffix = match.Groups[2].Value;
            OneCombinator combine = num => num + suffix;
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
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
            return Two(main, paragraph, numberingId, baseIlvl, abstractNumberId, ilvl1, ilvl2, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)$");
        if (match.Success) {
            ThreeCombinator three = (num1, num2, num3) => { return num1 + "." + num2 + "." + num3; };
            return Three(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, three);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)[\.)]$");
        if (match.Success) {
            char last = format.Val.Value[^1];
            string three(string num1, string num2, string num3) { return num1 + "." + num2 + "." + num3 + last; }
            return Three(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, three);
        }
        match = Regex.Match(format.Val.Value, @"^\(%(\d)\.%(\d)\.%(\d)$");  // EWHC/Admin/2010/3192
        if (match.Success) {
            ThreeCombinator three = (num1, num2, num3) => "(" + num1 + "." + num2 + "." + num3;
            return Three(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, three);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)\.%(\d)$");
        if (match.Success) {
            FourCombinator four = (num1, num2, num3, num4) => { return num1 + "." + num2 + "." + num3 + "." + num4; };
            return Four(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, four);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)\.%(\d)\.$");
        if (match.Success) {
            FourCombinator four = (num1, num2, num3, num4) => { return num1 + "." + num2 + "." + num3 + "." + num4 + "."; };
            return Four(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, four);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)\.%(\d)\.%(\d)(\.)?$");
        if (match.Success) {
            FiveCombinator combine = (num1, num2, num3, num4, num5) => { return num1 + "." + num2 + "." + num3 + "." + num4 + "." + num5 + match.Groups[6].Value; };
            return Five(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, combine);
        }

        match = Regex.Match(format.Val.Value, @"^([^%]+)%(\d)$");    // EWHC/Comm/2015/150
        if (match.Success) {
            string prefix = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;
            OneCombinator combine = (num) => { return prefix + num; };
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, ilvl, combine);
        }
        match = Regex.Match(format.Val.Value, @"^([^%]+)%(\d+)([^%]+)$");    // EWHC/Ch/2012/1411
        if (match.Success) {
            string prefix = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;
            string suffix = match.Groups[3].Value;
            OneCombinator combine = num => prefix + num + suffix;
            return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, ilvl, combine);
        }

        throw new Exception("unsupported level text: " + format.Val.Value);
    }

    private static int GetAbstractStart(MainDocumentPart main, int absNumId, int ilvl) {
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

    private static int GetStart(MainDocumentPart main, int numberingId, int ilvl) {
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

    private static string One(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl, Int32Value abstractNumberId, Match match, OneCombinator combine) {
        int ilvl = int.Parse(match.Groups[1].Value) - 1;
        return One(main, paragraph, numberingId, baseIlvl, abstractNumberId, ilvl, combine);
    }
    private static string One(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl, Int32Value abstractNumberId, int ilvl, OneCombinator combine) {
        Level lvl = Numbering.GetLevel(main, numberingId, ilvl);
        int n = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl);
        n += Fields.CountPrecedingParagraphsWithListNum(numberingId, ilvl, paragraph);
        string num = FormatN(n, lvl.NumberingFormat);
        return combine(num);
    }

    private delegate string TwoCombinator(string num1, string num2);

    private static string Two(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl, Int32Value abstractNumberId, Match match, TwoCombinator combine) {
        int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
        int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
        return Two(main, paragraph, numberingId, baseIlvl, abstractNumberId, ilvl1, ilvl2, combine);
    }

    private static string Two(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl, Int32Value abstractNumberId, int ilvl1, int ilvl2, TwoCombinator combine) {
        Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
        Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
        int start1 = GetStart(main, numberingId, ilvl1);
        int start2 = GetStart(main, numberingId, ilvl2);
        int n1 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl1, true);
        if (ilvl1 < baseIlvl && n1 > start1)
            n1 -= 1;
        else if (ilvl1 > baseIlvl)
            throw new Exception();
        int n2 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl2);
        if (ilvl2 < baseIlvl && n2 > start2)
            n2 -= 1;
        else if (ilvl2 > baseIlvl)
            throw new Exception();
        string num1 = FormatN(n1, lvl1.NumberingFormat);
        string num2 = FormatN(n2, lvl2.NumberingFormat);
        return combine(num1, num2);
    }

    private delegate string ThreeCombinator(string num1, string num2, string num3);

    private static string Three(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl, Int32Value abstractNumberId, Match match, ThreeCombinator combine) {
        int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
        int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
        int ilvl3 = int.Parse(match.Groups[3].Value) - 1;
        Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
        Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
        Level lvl3 = Numbering.GetLevel(main, numberingId, ilvl3);
        int start1 = GetStart(main, numberingId, ilvl1);
        int start2 = GetStart(main, numberingId, ilvl2);
        int start3 = GetStart(main, numberingId, ilvl3);
        int n1 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl1, true);
        if (ilvl1 < baseIlvl && n1 > start1)
            n1 -= 1;
        else if (ilvl1 > baseIlvl)
            throw new Exception();
        int n2 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl2, true);
        if (ilvl2 < baseIlvl && n2 > start2)
            n2 -= 1;
        else if (ilvl2 > baseIlvl)
            throw new Exception();
        int n3 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl3);
        if (ilvl3 < baseIlvl && n3 > start3)
            n3 -= 1;
        else if (ilvl3 > baseIlvl)
            throw new Exception();
        string num1 = FormatN(n1, lvl1.NumberingFormat);
        string num2 = FormatN(n2, lvl2.NumberingFormat);
        string num3 = FormatN(n3, lvl3.NumberingFormat);
        return combine(num1, num2, num3);
    }

    private delegate string FourCombinator(string num1, string num2, string num3, string num4);

    private static string Four(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl, Int32Value abstractNumberId, Match match, FourCombinator combine) {
        int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
        int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
        int ilvl3 = int.Parse(match.Groups[3].Value) - 1;
        int ilvl4 = int.Parse(match.Groups[4].Value) - 1;
        Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
        Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
        Level lvl3 = Numbering.GetLevel(main, numberingId, ilvl3);
        Level lvl4 = Numbering.GetLevel(main, numberingId, ilvl4);
        int start1 = GetStart(main, numberingId, ilvl1);
        int start2 = GetStart(main, numberingId, ilvl2);
        int start3 = GetStart(main, numberingId, ilvl3);
        int start4 = GetStart(main, numberingId, ilvl4);
        int n1 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl1, true);
        if (ilvl1 < baseIlvl && n1 > start1)
            n1 -= 1;
        else if (ilvl1 > baseIlvl)
            throw new Exception();
        int n2 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl2, true);
        if (ilvl2 < baseIlvl && n2 > start2)
            n2 -= 1;
        else if (ilvl2 > baseIlvl)
            throw new Exception();
        int n3 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl3, true);
        if (ilvl3 < baseIlvl && n3 > start3)
            n3 -= 1;
        else if (ilvl3 > baseIlvl)
            throw new Exception();
        int n4 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl4);
        if (ilvl4 < baseIlvl && n4 > start4)
            n4 -= 1;
        else if (ilvl4 > baseIlvl)
            throw new Exception();
        string num1 = FormatN(n1, lvl1.NumberingFormat);
        string num2 = FormatN(n2, lvl2.NumberingFormat);
        string num3 = FormatN(n3, lvl3.NumberingFormat);
        string num4 = FormatN(n4, lvl4.NumberingFormat);
        return combine(num1, num2, num3, num4);
    }

    private delegate string FiveCombinator(string num1, string num2, string num3, string num4, string num5);

    private static string Five(MainDocumentPart main, Paragraph paragraph, int numberingId, int baseIlvl, Int32Value abstractNumberId, Match match, FiveCombinator combine) {
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
        int start1 = GetStart(main, numberingId, ilvl1);
        int start2 = GetStart(main, numberingId, ilvl2);
        int start3 = GetStart(main, numberingId, ilvl3);
        int start4 = GetStart(main, numberingId, ilvl4);
        int start5 = GetStart(main, numberingId, ilvl5);
        int n1 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl1, true);
        if (ilvl1 < baseIlvl && n1 > start1)
            n1 -= 1;
        else if (ilvl1 > baseIlvl)
            throw new Exception();
        int n2 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl2, true);
        if (ilvl2 < baseIlvl && n2 > start2)
            n2 -= 1;
        else if (ilvl2 > baseIlvl)
            throw new Exception();
        int n3 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl3, true);
        if (ilvl3 < baseIlvl && n3 > start3)
            n3 -= 1;
        else if (ilvl3 > baseIlvl)
            throw new Exception();
        int n4 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl4);
        if (ilvl4 < baseIlvl && n4 > start4)
            n4 -= 1;
        else if (ilvl4 > baseIlvl)
            throw new Exception();
        int n5 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl5);
        if (ilvl5 < baseIlvl && n5 > start5)
            n5 -= 1;
        else if (ilvl5 > baseIlvl)
            throw new Exception();
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

    private static int? GetStartOverride(MainDocumentPart main, int numberingId, int ilvl) {
        NumberingInstance numbering = Numbering.GetNumbering(main, numberingId);
        return GetStartOverride(numbering, ilvl);
    }
    private static int? GetStartOverride(NumberingInstance numbering, int ilvl) {
        LevelOverride over = numbering?.ChildElements.OfType<LevelOverride>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();
        return over?.StartOverrideNumberingValue?.Val?.Value;
    }

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
            (int? prevNumId, int prevIlvl) = Numbering.GetNumberingIdAndIlvl(main, prev);
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

    private static bool LevelFormatIsCompound(MainDocumentPart main, int numberingId, int ilvl) {
        LevelText format = Numbering.GetLevel(main, numberingId, ilvl)?.LevelText;
        if (format is null)
            return false;
        if (!format.Val.HasValue)
            return false;
        int c = format.Val.Value.Count(c => (c == '%'));
        return c > 1;
    }

    class PrevAbsStartAccumulator {

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

    /// <param name="isHigher">whether the number to be calculated is a higher-level component, such as the 1 in 1.2</param>
    internal static int CalculateN(MainDocumentPart main, Paragraph paragraph, int numberingId, int abstractNumId, int ilvl, bool isHigher = false) {

        int? thisNumIdWithoutStyle = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
        int? thisNumIdOfStyle = Styles.GetStyleProperty(Styles.GetStyle(main, paragraph), s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);

        int? start = null;
        int numIdOfStartOverride = -1;
        // -1 means not set
        // -2 meanss trumped, even numbering instance's own start value doesn't matter
        // any positive integer is the numbering id of the previous paragraph that set the value of 'start'

        bool prevContainsLowerCompound = false; // see setter below

        int absStart = GetAbstractStart(main, abstractNumId, ilvl);

        var prevAbsStarts = new PrevAbsStartAccumulator();
        int count = 0;
        foreach (Paragraph prev in paragraph.Root().Descendants<Paragraph>().TakeWhile(p => !object.ReferenceEquals(p, paragraph))) {

            if (Paragraphs.IsDeleted(prev))
                continue;
            if (Paragraphs.IsEmptySectionBreak(prev))
                continue;
            if (Paragraphs.IsMergedWithFollowing(prev))
                continue;
            (int? prevNumId, int prevIlvl) = Numbering.GetNumberingIdAndIlvl(main, prev);
            if (!prevNumId.HasValue)
                continue;
            NumberingInstance prevNumbering = Numbering.GetNumbering(main, prevNumId.Value);
            if (prevNumbering is null)
                continue;

            AbstractNum prevAbsNum = Numbering.GetAbstractNum(main, prevNumbering);
            int prevAbsNumId = prevAbsNum.AbstractNumberId;
            if (prevAbsNumId != abstractNumId)
                continue;

            int? prevNumIdWithoutStyle = prev.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
            int? prevNumIdOfStyle = Styles.GetStyleProperty(Styles.GetStyle(main, prev), s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);

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
                    Style prevStyle =  Styles.GetStyle(main, prev);
                    string prevBasedOn = prevStyle?.BasedOn?.Val?.Value;
                    string thisStyleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    int? prevStyleIlvl = prevStyle?.StyleParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
                    if (prevBasedOn is not null && prevBasedOn == thisStyleId && prevStyleIlvl.HasValue)
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
                    if (prevNumIdWithoutStyle.HasValue)
                        prevAbsStarts.Put(prevNumIdWithoutStyle.Value, prevIlvl, absStart);
                    bool forTest67 = prevNumIdOfStyle.HasValue && thisNumIdOfStyle.HasValue && prevNumIdOfStyle.Value != thisNumIdOfStyle.Value;
                    if (forTest67) {
                        start = absStart;
                        numIdOfStartOverride = -2;
                    }
                }

                if (prevNumIdWithoutStyle == numberingId && LevelFormatIsCompound(main, numberingId, prevIlvl))
                    prevContainsLowerCompound = true;
                continue;
            }

            if (prevNumIdWithoutStyle.HasValue) {
                var prevAbsStart = prevAbsStarts.Get(prevNumIdWithoutStyle.Value, prevIlvl + 2);
                if (prevAbsStart.HasValue) {
                    start = prevAbsStart.Value;
                    numIdOfStartOverride = -2;
                }
            }

            // prevIlvl == ilvl
            if (prevNumId.Value != numIdOfStartOverride && numIdOfStartOverride != -2) {  // true whenever start is null
                if (!isHigher || prevNumIdOfStyle.HasValue) {  // test68
                    int? prevOver = GetStartOverride(prevNumbering, ilvl);
                    if (prevOver.HasValue && StartOverrideIsOperative(main, prev, prevIlvl)) {
                        start = prevOver.Value;
                        numIdOfStartOverride = prevNumId.Value;
                        if (!prevContainsLowerCompound)  // only test37 needs this condition
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

        if (isHigher)
            prevContainsLowerCompound = true;

        if (numberingId != numIdOfStartOverride && numIdOfStartOverride != -2) {  // true whenever start is null
            int? over = GetStartOverride(main, numberingId, ilvl);
            if (start is null)
                start = GetStart(main, numberingId, ilvl);
            else if (over.HasValue)
                start = over.Value;
            if (over.HasValue && !prevContainsLowerCompound)  // only test37 needs second condition
                count = 0;
        }
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
            int n = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl);
            n -= 1;
            string num = FormatN(n, lvl.NumberingFormat);
            return num + ".";
        }
        throw new Exception();
    }
}

}
