
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

    internal static IInline Map(MainDocumentPart main, Run run, AlternateContent e) {
        if (e.ChildElements.Count != 2)
            throw new Exception();
        AlternateContentChoice choice = (AlternateContentChoice) e.FirstChild;
        AlternateContentFallback fallback = (AlternateContentFallback) e.ChildElements.Last();
        if (choice.ChildElements.Count != 1)
            throw new Exception();
        if (fallback.ChildElements.Count != 1)
            throw new Exception();
        if (fallback.FirstChild is Picture pict) {
            if (pict.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().Any(id => id.RelationshipId is not null))
                return WImageRef.Make(main, pict);
            if (pict.ChildElements.Count == 1 && pict.FirstChild.NamespaceUri == "urn:schemas-microsoft-com:vml"  && pict.FirstChild.LocalName == "line")
                return null;
        }
        // EWHC/Fam/2017/3832 contains a textBox
        // don't know what to do with EWHC/Admin/2011/1403
        // EWCA/Crim/2017/281
        // return null;
        // EWCA/Civ/2018/2026
        throw new Exception();
        // throw new Exception();
    }

}

}