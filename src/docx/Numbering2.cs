
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

readonly struct NumberInfo {
    internal string Number { get; init; }
    internal NumberingSymbolRunProperties Props { get; init; }
}

class Numbering2 {

    public static NumberInfo? GetFormattedNumber(MainDocumentPart main, Paragraph paragraph) {
        NumberingProperties props = Numbering.GetNumberingPropertiesOrStyleNumberingProperties(main, paragraph);
        if (props is null)
            return null;
        NumberingId id = props.NumberingId;
        if (id?.Val?.Value is null)
            return null;
        // there may be no numbering instance that corresponds to this id, in which case Magic2 returns null
        int ilvl = props.NumberingLevelReference?.Val?.Value ?? 0;
        string magic = Magic2(main, paragraph, id.Val.Value, ilvl);
        if (magic is null)
            return null;
        Level level = Numbering.GetLevel(main, id, ilvl);
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

        // if (format.Val.Value == "-") {
        //     int baseStart = baseLevel.StartNumberingValue?.Val ?? 1;
        //     int n = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, baseIlvl, baseStart);
        //     string num = FormatN(n, baseLevel.NumberingFormat);
        //     return num;
        // }
        Match match = Regex.Match(format.Val.Value, "^%(\\d)$");
        if (match.Success) {
            int ilvl = int.Parse(match.Groups[1].Value) - 1;
            Level lvl = Numbering.GetLevel(main, numberingId, ilvl);
            int start = lvl.StartNumberingValue?.Val ?? 1;
            int n = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl, start);
            string num = FormatN(n, lvl.NumberingFormat);
            return num;
        }
        match = Regex.Match(format.Val.Value, "^%(\\d)\\.$");
        if (match.Success) {
            int ilvl = int.Parse(match.Groups[1].Value) - 1;
            Level lvl = Numbering.GetLevel(main, numberingId, ilvl);
            int start = lvl.StartNumberingValue?.Val ?? 1;
            int n = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl, start);
            string num = FormatN(n, lvl.NumberingFormat);
            return num + ".";
        }
        match = Regex.Match(format.Val.Value, "^\\(%(\\d)\\)$");
        if (match.Success) {
            int ilvl = int.Parse(match.Groups[1].Value) - 1;
            Level lvl = Numbering.GetLevel(main, numberingId, ilvl);
            int start = lvl.StartNumberingValue?.Val ?? 1;
            int n = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl, start);
            string num = FormatN(n, lvl.NumberingFormat);
            return "(" + num + ")";
        }
        match = Regex.Match(format.Val.Value, "%(\\d)\\)$");
        if (match.Success) {
            int ilvl = int.Parse(match.Groups[1].Value) - 1;
            Level lvl = Numbering.GetLevel(main, numberingId, ilvl);
            int start = lvl.StartNumberingValue?.Val ?? 1;
            int n = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl, start);
            string num = FormatN(n, lvl.NumberingFormat);
            return num + ")";
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.$");
        if (match.Success) {
            int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
            int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
            Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
            Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
            int start1 = lvl1.StartNumberingValue?.Val ?? 1;
            int start2 = lvl2.StartNumberingValue?.Val ?? 1;
            int n1 = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl1, start1);
            if (ilvl1 < baseIlvl)
                n1 -= 1;
            else if (ilvl1 > baseIlvl)
                throw new Exception();
            int n2 = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl2, start2);
            if (ilvl2 < baseIlvl)
                n2 -= 1;
            else if (ilvl2 > baseIlvl)
                throw new Exception();
            string num1 = FormatN(n1, lvl1.NumberingFormat);
            string num2 = FormatN(n2, lvl2.NumberingFormat);
            return num1 + "." + num2 + ".";
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)$");
        if (match.Success) {
            int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
            int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
            Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
            Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
            int start1 = lvl1.StartNumberingValue?.Val ?? 1;
            int start2 = lvl2.StartNumberingValue?.Val ?? 1;
            int n1 = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl1, start1);
            if (ilvl1 < baseIlvl)
                n1 -= 1;
            else if (ilvl1 > baseIlvl)
                throw new Exception();
            int n2 = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl2, start2);
            if (ilvl2 < baseIlvl)
                n2 -= 1;
            else if (ilvl2 > baseIlvl)
                throw new Exception();
            string num1 = FormatN(n1, lvl1.NumberingFormat);
            string num2 = FormatN(n2, lvl2.NumberingFormat);
            return num1 + "." + num2;
        }
        match = Regex.Match(format.Val.Value, @"^%(\d)\.%(\d)\.%(\d)$");
        if (match.Success) {
            int ilvl1 = int.Parse(match.Groups[1].Value) - 1;
            int ilvl2 = int.Parse(match.Groups[2].Value) - 1;
            int ilvl3 = int.Parse(match.Groups[3].Value) - 1;
            Level lvl1 = Numbering.GetLevel(main, numberingId, ilvl1);
            Level lvl2 = Numbering.GetLevel(main, numberingId, ilvl2);
            Level lvl3 = Numbering.GetLevel(main, numberingId, ilvl3);
            int start1 = lvl1.StartNumberingValue?.Val ?? 1;
            int start2 = lvl2.StartNumberingValue?.Val ?? 1;
            int start3 = lvl3.StartNumberingValue?.Val ?? 1;
            int n1 = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl1, start1);
            if (ilvl1 < baseIlvl)
                n1 -= 1;
            else if (ilvl1 > baseIlvl)
                throw new Exception();
            int n2 = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl2, start2);
            if (ilvl2 < baseIlvl)
                n2 -= 1;
            else if (ilvl2 > baseIlvl)
                throw new Exception();
            int n3 = GetNForLevelBasedOnAbstractId(main, paragraph, abstractNumberId, ilvl3, start3);
            if (ilvl3 < baseIlvl)
                n3 -= 1;
            else if (ilvl3 > baseIlvl)
                throw new Exception();
            string num1 = FormatN(n1, lvl1.NumberingFormat);
            string num2 = FormatN(n2, lvl2.NumberingFormat);
            string num3 = FormatN(n3, lvl3.NumberingFormat);
            return num1 + "." + num2 + "." + num3;
        }
        if (baseLevel.NumberingFormat.Val == NumberFormatValues.Bullet) {
            if (format.Val.Value == "-")
                return "-";
            if (format.Val.Value == ".")    // EWHC/QB/2018/2066
                return ".";
            if (format.Val.Value == "o")    // EWCA/Civ/2013/923.
                return "◦";
            if (format.Val.Value == "–")    // EWCA/Civ/2013/1015
                return "–"; // en dash ??
            if (format.Val.Value == "")    // EWHC/QB/2018/2066
                return "•";
            if (format.Val.Value == "")    // EWCA/Civ/2013/11
                return "•";
        }
        if (format.Val.Value == "")     // EWHC/QB/2018/2066
            return "";
        throw new Exception("unsupported level text: " + format.Val.Value);
    }

    private static string FormatN(int n, NumberingFormat format) {
        if (format.Val == NumberFormatValues.Decimal)
            return n.ToString();
        if (format.Val == NumberFormatValues.LowerLetter)
            return Util.ToLowerLetter(n);
        if (format.Val == NumberFormatValues.UpperLetter)
            return Util.ToUpperLetter(n);
        if (format.Val == NumberFormatValues.LowerRoman)
            return Util.ToLowerRoman(n);
        if (format.Val == NumberFormatValues.UpperRoman)
            return Util.ToUpperRoman(n);
        // if (format.Val == NumberFormatValues.Bullet)
        //     return "•";
        throw new Exception("unsupported numbering format: " + format.Val.ToString());
    }

    private static int GetNForLevel(MainDocumentPart main, Paragraph paragraph, int numberingId, int levelNum, int start) {
        int count = 0;
        Paragraph previous = paragraph.PreviousSibling<Paragraph>();
        while (previous != null) {
            NumberingProperties prevProps = Numbering.GetNumberingPropertiesOrStyleNumberingProperties(main, previous);
            if (prevProps is not null) {
                int? id2 = prevProps.NumberingId?.Val?.Value;
                if (numberingId.Equals(id2)) {
                    int level2 = prevProps.NumberingLevelReference?.Val?.Value ?? 0;
                    if (level2 == levelNum)
                        count += 1;
                    if (level2 < levelNum)
                        break;
                }
            }
            previous = previous.PreviousSibling<Paragraph>();
        }
        return start + count;
    }

    private static int GetNForLevelBasedOnAbstractId(MainDocumentPart main, Paragraph paragraph, int abstractNumId, int levelNum, int start) {
        int count = 0;
        Paragraph previous = paragraph.PreviousSibling<Paragraph>();
        while (previous != null) {
            NumberingProperties props2 = Numbering.GetNumberingPropertiesOrStyleNumberingProperties(main, previous);
            if (props2 is null) {
                previous = previous.PreviousSibling<Paragraph>();
                continue;
            }
            if (props2.NumberingId?.Val?.Value is null) {
                previous = previous.PreviousSibling<Paragraph>();
                continue;
            }
            int numberingId2 = props2.NumberingId.Val.Value;
            NumberingInstance numbering2 = Numbering.GetNumbering(main, numberingId2);
            if (numbering2 is null) {
                previous = previous.PreviousSibling<Paragraph>();
                continue;
            }
            AbstractNum abstractNum2 = Numbering.GetAbstractNum(main, numbering2);
            // if (abstractNum2?.AbstractNumberId is null)
            //     continue;
            int abstractNumId2 = abstractNum2.AbstractNumberId;
            if (abstractNumId.Equals(abstractNumId2)) {
                int level2 = props2.NumberingLevelReference?.Val?.Value ?? 0;
                if (level2 == levelNum)
                    count += 1;
                if (level2 < levelNum)
                    break;
            }
            previous = previous.PreviousSibling<Paragraph>();
        }
        return start + count;
    }

}

}
