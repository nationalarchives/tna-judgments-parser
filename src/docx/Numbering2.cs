
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

    public static NumberInfo? GetFormattedNumber(MainDocumentPart main, Paragraph paragraph) {
        NumberingProperties props = Numbering.GetNumberingPropertiesOrStyleNumberingProperties(main, paragraph);
        if (props is null)
            return null;
        int? numId = Numbering.GetNumberingIdOrNumberingChangeId(props);
        if (numId is null)
            return null;
        // there may be no numbering instance that corresponds to this id, in which case Magic2 returns null
        int ilvl = props.NumberingLevelReference?.Val?.Value ?? 0;
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
        LevelText format = baseLevel.LevelText;

        /* None */
        if (baseLevel.NumberingFormat.Val == NumberFormatValues.None) { // EWHC/QB/2009/406
            if (!string.IsNullOrEmpty(format.Val.Value)) {  // EWHC/Ch/2014/4092 contains %
                logger.LogDebug("None number format: " + format.Val.Value);
            }
            return "";
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
            logger.LogWarning("unknown bullet text: " + format.Val.Value);
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
            OneCombinator combine = num => num ;
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
        match = Regex.Match(format.Val.Value, @"^([^%]*)%(\d)([\.\-\(])%(\d+)([^%]*)$");
        if (match.Success) {
            string prefix = match.Groups[1].Value;
            int ilvl1 = int.Parse(match.Groups[2].Value) - 1;
            string middle = match.Groups[3].Value;
            int ilvl2 = int.Parse(match.Groups[4].Value) - 1;
            if (ilvl2 > 9)
                logger.LogWarning("two-digit numbering level: " + ilvl2);
            string suffix = match.Groups[5].Value;
            TwoCombinator combine = (num1, num2) => { return prefix + num1 + middle + num2 + suffix; };
            return Two(main, paragraph, numberingId, baseIlvl, abstractNumberId, ilvl1, ilvl2, combine);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)$");
        if (match.Success) {
            ThreeCombinator three = (num1, num2, num3) => { return num1 + "." + num2 + "." + num3; };
            return Three(main, paragraph, numberingId, baseIlvl, abstractNumberId, match, three);
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)\.$");
        if (match.Success) {
            ThreeCombinator three = (num1, num2, num3) => { return num1 + "." + num2 + "." + num3 + "."; };
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

    private static int GetStart(MainDocumentPart main, int numberingId, int ilvl) {
        NumberingInstance numbering = Numbering.GetNumbering(main, numberingId);
        Level level = numbering.Descendants<Level>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();
        if (level?.StartNumberingValue?.Val is not null)
            return level.StartNumberingValue.Val;
        LevelOverride lvlOver = numbering.ChildElements
            .OfType<LevelOverride>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();
        if (lvlOver?.StartOverrideNumberingValue?.Val is not null)
            return lvlOver.StartOverrideNumberingValue.Val;
        AbstractNum abs = main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<AbstractNum>()
            .Where(a => a.AbstractNumberId.Value == numbering.AbstractNumId.Val.Value)
            .First();
        level = abs.ChildElements
            .OfType<Level>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();  // does not exist in EWHC/Ch/2003/2902
        if (level?.StartNumberingValue?.Val is not null)
            return level.StartNumberingValue.Val;
        return 1;
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
        int n1 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl1);
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
        int n1 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl1);
        if (ilvl1 < baseIlvl && n1 > start1)
            n1 -= 1;
        else if (ilvl1 > baseIlvl)
            throw new Exception();
        int n2 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl2);
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
        int n1 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl1);
        if (ilvl1 < baseIlvl && n1 > start1)
            n1 -= 1;
        else if (ilvl1 > baseIlvl)
            throw new Exception();
        int n2 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl2);
        if (ilvl2 < baseIlvl && n2 > start2)
            n2 -= 1;
        else if (ilvl2 > baseIlvl)
            throw new Exception();
        int n3 = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl3);
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

    private enum CountingAction { Skip, Count, Stop, Override }

    private static CountingAction ShouldCount(MainDocumentPart main, Paragraph paragraph, int abstractNumId, int levelNum) {
        NumberingProperties props2;
        if (paragraph.ChildElements.All(child => child is ParagraphProperties)) {   // EWHC/Ch/2011/3553 ?? && paragraph.ChildElements.OfType<ParagraphProperties>().First().SectionProperties is not null
            props2 = paragraph.ParagraphProperties?.NumberingProperties;    // ukut/iac/2021/130
        } else {
            props2 = Numbering.GetNumberingPropertiesOrStyleNumberingProperties(main, paragraph);
        }

        if (props2 is null)
            return CountingAction.Skip;
        int? numberingId2 = Numbering.GetNumberingIdOrNumberingChangeId(props2);
        if (numberingId2 is null)
            return CountingAction.Skip;
        NumberingInstance numbering2 = Numbering.GetNumbering(main, numberingId2.Value);
        if (numbering2 is null)
            return CountingAction.Skip;
        AbstractNum abstractNum2 = Numbering.GetAbstractNum(main, numbering2);
        int abstractNumId2 = abstractNum2.AbstractNumberId;
        if (abstractNumId2 != abstractNumId)
            return CountingAction.Skip;
        int level2 = props2.NumberingLevelReference?.Val?.Value ?? 0;
        if (level2 < levelNum)
            return CountingAction.Stop;
        if (level2 > levelNum)
            return CountingAction.Skip;

        NumberingInstance ownNumbering = Numbering.GetNumbering(main, paragraph);
        LevelOverride lvlOver = ownNumbering?.ChildElements
            .OfType<LevelOverride>()
            .Where(l => l.LevelIndex.Value == levelNum)
            .FirstOrDefault();
        if (lvlOver?.StartOverrideNumberingValue?.Val is not null)
            return CountingAction.Override;
        else
            return CountingAction.Count;
    }

    private static CountingAction ShouldCountWhenHasNumOverride(MainDocumentPart main, Paragraph paragraph, int numberingId, int levelNum) {
        NumberingProperties props2 = paragraph.ParagraphProperties?.NumberingProperties;    // don't consider style numbering here, unlike above
        if (props2 is null)
            return ShouldCountWhenHasNumOverride2(main, paragraph, numberingId, levelNum);
        int? numberingId2 = Numbering.GetNumberingIdOrNumberingChangeId(props2);
        if (numberingId2 is null)
            return ShouldCountWhenHasNumOverride2(main, paragraph, numberingId, levelNum);
        if (numberingId2 != numberingId)
            return CountingAction.Skip;
        int level2 = props2.NumberingLevelReference?.Val?.Value ?? 0;
        if (level2 < levelNum)
            return CountingAction.Stop;
        if (level2 > levelNum)
            return CountingAction.Skip;
        return CountingAction.Count;
    }

    private static IEnumerable<Paragraph> GetPreviousParagraphs(Paragraph paragraph) {
        return paragraph.Root()
            .Descendants<Paragraph>()
            .Reverse()
            .SkipWhile(e => !object.ReferenceEquals(e, paragraph))
            .Skip(1);
    }

    private static CountingAction ShouldCountWhenHasNumOverride2(MainDocumentPart main, Paragraph paragraph, int numberingId, int levelNum) {
        NumberingInstance num1 = Numbering.GetNumbering(main, numberingId);
        if (num1.AbstractNumId is null)
            return CountingAction.Skip;
        // if (num1.AbstractNumId.Val is null)
        //     return CountingAction.Skip;
        if (!num1.AbstractNumId.Val.HasValue)
            return CountingAction.Skip;
        int abstractNumId1 = num1.AbstractNumId.Val.Value;

        NumberingProperties props2 = Numbering.GetNumberingPropertiesOrStyleNumberingProperties(main, paragraph);
        if (props2 is null)
            return CountingAction.Skip;
        int? numberingId2 = Numbering.GetNumberingIdOrNumberingChangeId(props2);
        if (numberingId2 is null)
            return CountingAction.Skip;
        NumberingInstance numbering2 = Numbering.GetNumbering(main, numberingId2.Value);
        if (numbering2 is null)
            return CountingAction.Skip;
        AbstractNum abstractNum2 = Numbering.GetAbstractNum(main, numbering2);
        int abstractNumId2 = abstractNum2.AbstractNumberId;
        if (abstractNumId2 != abstractNumId1)
            return CountingAction.Skip;

        int ilvl2 = props2.NumberingLevelReference?.Val?.Value ?? 0;
        // if (ilvl2 < levelNum)
        //     return CountingAction.Stop;
        if (ilvl2 != levelNum)
            return CountingAction.Skip;

        IEnumerable<Paragraph> previous = GetPreviousParagraphs(paragraph);
        System.Func<Paragraph, Boolean> f = (p) => {
            NumberingProperties ownProps = p.ParagraphProperties?.NumberingProperties;
            if (ownProps is null)
                return false;
            int? ownNumId = Numbering.GetNumberingIdOrNumberingChangeId(ownProps);
            if (ownNumId is null)
                return false;
            NumberingInstance ownNumbering = Numbering.GetNumbering(main, ownNumId.Value);
            if (ownNumbering is null)
                return false;
            AbstractNumId ownAbsId = ownNumbering.AbstractNumId;
            if (ownAbsId is null)
                return false;
            if (!ownAbsId.Val.HasValue)
                return false;
            if (ownAbsId.Val.Value != abstractNumId1)
                return false;
            LevelOverride lvlOver = ownNumbering.ChildElements
                .OfType<LevelOverride>()
                .Where(l => l.LevelIndex.Value == levelNum)
                .FirstOrDefault();
            if (lvlOver is null)
                return false;
            if (lvlOver?.StartOverrideNumberingValue?.Val is null)
                return false;
            return true;
        };
        if (previous.Any(f))
            return CountingAction.Count;
        return CountingAction.Skip;
    }

    internal static int CalculateN(MainDocumentPart main, Paragraph paragraph, int numberingId, int abstractNumId, int levelNum) {
        int? start = null;
        bool hasNumOverride;
        int relevantNumberingId = numberingId;
        CountingAction a1 = ShouldCount(main, paragraph, abstractNumId, levelNum);
        if (a1 == CountingAction.Override) {
            hasNumOverride = true;
            // relevantNumberindId =  Numbering.GetNumbering(main, paragraph).NumberID; // no need
            LevelOverride lvlOver = Numbering.GetNumbering(main, paragraph).ChildElements
                .OfType<LevelOverride>()
                .Where(l => l.LevelIndex.Value == levelNum)
                .First();
            start = lvlOver.StartOverrideNumberingValue.Val;
        } else {
             hasNumOverride = false;
        }
        int count = 0;
        IEnumerator<Paragraph> previous = GetPreviousParagraphs(paragraph).GetEnumerator();
        while (previous.MoveNext()) {
            CountingAction a;
            if (hasNumOverride)
                a = ShouldCountWhenHasNumOverride(main, previous.Current, relevantNumberingId, levelNum);
            else
                a = ShouldCount(main, previous.Current, abstractNumId, levelNum);
            if (a == CountingAction.Stop)
                break;
            if (a == CountingAction.Count)
                count += 1;
            if (a == CountingAction.Override) {
                count += 1;
                if (start is null) {
                    relevantNumberingId =  Numbering.GetNumbering(main, previous.Current).NumberID;
                    LevelOverride lvlOver = Numbering.GetNumbering(main, previous.Current).ChildElements
                        .OfType<LevelOverride>()
                        .Where(l => l.LevelIndex.Value == levelNum)
                        .First();
                    start = lvlOver.StartOverrideNumberingValue.Val;
                    hasNumOverride = true;
                }
            }
        }
        if (start is null)
            start = GetStart(main, numberingId, levelNum);
        return start.Value + count;
    }

    // for REF \w -- see EWHC/Comm/2018/1368
    public static string GetNumberInFullContext(MainDocumentPart main, Paragraph paragraph) {
        NumberingProperties props = Numbering.GetNumberingPropertiesOrStyleNumberingProperties(main, paragraph);
        int numId = (int) Numbering.GetNumberingIdOrNumberingChangeId(props);
        int ilvl = props.NumberingLevelReference?.Val?.Value ?? 0;
        string formatted = Magic2(main, paragraph, numId, ilvl);
        while (ilvl > 0) {
            ilvl -= 1;
            string higherLevel = Magic3(main, paragraph, numId, ilvl);  // assumes number format involves only one level
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
            int start = lvl.StartNumberingValue?.Val ?? 1;
            int n = CalculateN(main, paragraph, numberingId, abstractNumberId, ilvl);
            n -= 1;
            string num = FormatN(n, lvl.NumberingFormat);
            return num + ".";
        }
        throw new Exception();
    }
}

}
