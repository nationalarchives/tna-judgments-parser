
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Math = DocumentFormat.OpenXml.Math;

using Microsoft.Extensions.Logging;


namespace UK.Gov.Legislation.Judgments.Parse {

class AlternateContent2 {

    internal static IInline Map(MainDocumentPart main, RunProperties rprops, AlternateContent e) {
        if (e.ChildElements.Count != 2)
            throw new Exception();
        AlternateContentChoice choice = (AlternateContentChoice) e.FirstChild;
        AlternateContentFallback fallback = (AlternateContentFallback) e.ChildElements.Last();
        Fields.logger.LogDebug("alternate content, choice requires " + choice.Requires);
        // http://schemas.microsoft.com/office/word/2010/wordprocessingShape
        if (choice.Requires == "wps")   // EWHC/Fam/2017/3832
            return MapFallback(main, rprops, fallback);
        // http://schemas.microsoft.com/office/word/2010/wordprocessingGroup
        if (choice.Requires == "wpg")   // WCA/Crim/2017/281
            return MapFallback(main, rprops, fallback);
        throw new Exception();
    }

    private static IInline MapFallback(MainDocumentPart main, RunProperties rprops, AlternateContentFallback fallback) {
        if (fallback.ChildElements.Count != 1)
            throw new Exception();
        return Inline.MapRunChild(main, rprops, fallback.FirstChild);
    }

}

}