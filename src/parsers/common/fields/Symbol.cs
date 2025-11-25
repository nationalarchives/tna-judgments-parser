
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-symbol-field-3f4fbf16-e592-4c27-92e0-676b1c5dd50e

class Symbol {

    private static readonly string pattern = @"^ SYMBOL (183) \\f ""(Symbol)"" \\s (\d+) \\h $";
    private static readonly string pattern2 = @"^ SYMBOL (\d+) \\\* MERGEFORMAT ";

    private static readonly Encoding ANSI;


    static Symbol() {
        // Win ANSI encoding not registered by default in on .NET Core
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        ANSI = Encoding.GetEncoding(1252);
    }

    internal static SpecialCharacter Convert(string fieldCode) {
        Match match = Regex.Match(fieldCode, pattern);
        if (match.Success) {
            string code = match.Groups[1].Value;
            string font = match.Groups[2].Value;
            float points = float.Parse(match.Groups[3].Value);
            int utf32 = int.Parse(code);    // NumberStyles.AllowHexSpecifier doesn't seem to work here
            string symbol = Char.ConvertFromUtf32(utf32);
            return new SpecialCharacter(symbol, null, font, points);
        }
        match = Regex.Match(fieldCode, pattern2);
        if (match.Success) {
            byte ansi = byte.Parse(match.Groups[1].Value);
            string utf8 = AnsiToUtf8(ansi);
            return new SpecialCharacter(utf8, null, null, null);
        }
        throw new Exception();
    }

    private static string AnsiToUtf8(byte ansi) {
        byte[] utf8 = Encoding.Convert(ANSI, Encoding.UTF8, new byte[] { ansi });
        return Encoding.UTF8.GetString(utf8);
    }

}

}
