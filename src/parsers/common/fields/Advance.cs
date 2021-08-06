

using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-advance-field-6379bd1b-49be-4c85-95e6-f42b44ab0e70

class Advance {

    internal static bool Is(string fieldCode) { // EWHC/QB/2004/2337
        return fieldCode.StartsWith(" ADVANCE ");
    }

    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, IEnumerable<OpenXmlElement> rest) {
        if (fieldCode != " ADVANCE \\d4 ")
            throw new Exception();
        if (rest.Any())
            throw new Exception();
        return Enumerable.Empty<IInline>();
    }
}

}
