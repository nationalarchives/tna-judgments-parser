
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-includepicture-field-a3aac6dc-4e08-4d62-9aac-794279d02de9

class IncludedPicture {

    private static readonly string pattern = @"^ INCLUDEPICTURE \\d ""([^""]+)"" \\x \\y $";

    internal static IInline Parse(MainDocumentPart main, string fieldCode, IEnumerable<OpenXmlElement> rest) {
        Match match = Regex.Match(fieldCode, pattern);
        if (!match.Success)
            throw new Exception();
        string uri = match.Groups[1].Value;
        // \d switch means URI should be absolute?
        if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
            throw new Exception();
        return new WExternalImage() { URL = uri };
    }
}

}
