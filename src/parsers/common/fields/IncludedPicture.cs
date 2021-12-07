
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-includepicture-field-a3aac6dc-4e08-4d62-9aac-794279d02de9

class IncludedPicture {

    private static ILogger logger = Logging.Factory.CreateLogger<IncludedPicture>();

    private static readonly string pattern1 = @"^ INCLUDEPICTURE \\d ""([^""]+)"" \\x \\y $";
    private static readonly string pattern2 = @"^ INCLUDEPICTURE ""([^""]+)"" \\\* MERGEFORMAT \\d \\x \\y $";
    private static readonly string pattern3 = @"^ INCLUDEPICTURE ""([^""]+)"" \\\* MERGEFORMAT \\d \\z $";

    internal static IInline Parse(MainDocumentPart main, string fieldCode, IEnumerable<OpenXmlElement> rest) {
        Match match = Regex.Match(fieldCode, pattern1);
        if (!match.Success)
            match = Regex.Match(fieldCode, pattern2);
        if (!match.Success)
            match = Regex.Match(fieldCode, pattern3);
        if (!match.Success)
            throw new Exception();
        string uri = match.Groups[1].Value;
        // \d switch means URI should be absolute?
        if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute)) {
            logger.LogWarning("ignoring included picture: " + uri);
            return null;
        }
        return new WExternalImage() { URL = uri };
    }
}

}
