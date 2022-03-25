
using System;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Util {

    internal static string ToLowerLetter(int n) {
        if (n < 1)
            return string.Empty;
        char digit = (char) (97 + ((n - 1) % 26));
        string rest = ToLowerLetter((n - 1) / 26);
        return rest + digit.ToString();
    }

    internal static string ToUpperLetter(int n) {
        return ToLowerLetter(n).ToUpper();
    }

    internal static string ToLowerRoman(int n) {
        return ToUpperRoman(n).ToLower();
    }

    internal static string ToUpperRoman(int n) {
        if (n > 3999)
            throw new ArgumentOutOfRangeException(n.ToString());
        if (n < 1)
            return string.Empty;            
        if (n >= 1000)
            return "M" + ToUpperRoman(n - 1000);
        if (n >= 900)
            return "CM" + ToUpperRoman(n - 900); 
        if (n >= 500)
            return "D" + ToUpperRoman(n - 500);
        if (n >= 400)
            return "CD" + ToUpperRoman(n - 400);
        if (n >= 100)
            return "C" + ToUpperRoman(n - 100);            
        if (n >= 90)
            return "XC" + ToUpperRoman(n - 90);
        if (n >= 50)
            return "L" + ToUpperRoman(n - 50);
        if (n >= 40)
            return "XL" + ToUpperRoman(n - 40);
        if (n >= 10)
            return "X" + ToUpperRoman(n - 10);
        if (n >= 9)
            return "IX" + ToUpperRoman(n - 9);
        if (n >= 5)
            return "V" + ToUpperRoman(n - 5);
        if (n >= 4)
            return "IV" + ToUpperRoman(n - 4);
        if (n >= 1)
            return "I" + ToUpperRoman(n - 1);
        throw new ArgumentOutOfRangeException("unknown error");
    }

    internal static float DxaToInches(string dxa) {
        return float.Parse(dxa) / 1440f;
    }
    internal static float? DxaToInches(StringValue dxa) {
        if (dxa is null)
            return null;
        return Util.DxaToInches(dxa.Value);
    }

    internal static bool? OnOffToBool(OnOffType onOff) {
        if (onOff is null)
            return null;
        OnOffValue val = onOff.Val;
        if (val == null)
            return true;
        return val.Value;
    }

}

}

