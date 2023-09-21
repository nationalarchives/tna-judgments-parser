
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-seq-sequence-field-062a387b-dfc9-4ef8-8235-29ee113d59be

class Seq {

// " SEQ CHAPTER \\h \\r 1";   // EWHC/Admin/2018/288
// seq level0 \*arabic -- EWCA/Civ/2009/701

    internal static bool Is(string fieldCode) {
        return fieldCode.StartsWith(" SEQ ") || fieldCode.StartsWith(" seq ");
    }

    [Obsolete]
    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, IEnumerable<OpenXmlElement> rest) {
        return Fields.RestOptional(main, rest);
    }
}

}
