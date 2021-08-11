
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-symbol-field-3f4fbf16-e592-4c27-92e0-676b1c5dd50e

class Symbol {

    private static readonly string pattern = @"^ SYMBOL (183) \\f ""(Symbol)"" \\s (\d+) \\h $";

    internal static IInline Parse(MainDocumentPart main, string fieldCode, IEnumerable<OpenXmlElement> rest) {
        Match match = Regex.Match(fieldCode, pattern);
        if (!match.Success)
            throw new Exception();
        string code = match.Groups[1].Value;
        string font = match.Groups[2].Value;
        float points = float.Parse(match.Groups[3].Value);
        int utf32 = int.Parse(code);    // NumberStyles.AllowHexSpecifier doesn't seem to work here
        string symbol = Char.ConvertFromUtf32(utf32);
        return new SpecialCharacter(symbol, null, font, points);
    }
}

}
