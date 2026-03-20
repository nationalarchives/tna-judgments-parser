
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

    private static ILogger Logger = Logging.Factory.CreateLogger<AlternateContent2>();

    internal static IInline Map(MainDocumentPart main, RunProperties rprops, AlternateContent e) {
        if (e.ChildElements.Count == 1) {
            AlternateContentChoice choice1 = (AlternateContentChoice) e.FirstChild;
            if (choice1.Requires == "wpi") {
                Fields.logger.LogWarning("skipping 'wpi' content because no fallback");
                return null;
            }
        }
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
        // http://schemas.microsoft.com/office/word/2010/wordprocessingInk"
        if (choice.Requires == "wpi")   // [2021] EWCA Civ 1768
            return MapFallback(main, rprops, fallback);
        // http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas
        if (choice.Requires == "wpc") // [2023] EWHC 1495 (Pat)
            return MapFallback(main, rprops, fallback);
        // https://docs.microsoft.com/en-us/openspecs/office_standards/ms-docx/c2aa01cb-206d-4875-afef-5feabb8f124d
        if (choice.Requires == "w16se") {
            string ns = "http://schemas.microsoft.com/office/word/2015/wordml/symex";
            OpenXmlElement symEx = choice.ChildElements.FirstOrDefault();
            if (symEx is not null && symEx.LocalName == "symEx" && symEx.NamespaceUri == ns && !choice.ChildElements.Skip(1).Any())
                return SpecialCharacter.MakeSymEx(symEx, rprops);
        }
        // https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.vml?view=openxml-2.8.1
        if (choice.Requires == "v") {
            if (choice.FirstChild is Picture pict)
                return WImageRef.Make(main, pict);
            if (fallback.FirstChild is Drawing drawing)
                return new WImageRef(main, drawing);
        }
        // Chart Extensions 1.0 - Microsoft Office 2016 Chart Extensions
        // https://docs.microsoft.com/en-us/openspecs/office_standards/ms-docx/
        if (choice.Requires == "cx1") {
            Logger.LogDebug("Skipping cx1 (Chart Extensions 1.0) content, using fallback");
            return MapFallback(main, rprops, fallback);
        }
        throw new Exception();
    }

    private static IInline MapFallback(MainDocumentPart main, RunProperties rprops, AlternateContentFallback fallback) {
        if (fallback.ChildElements.Count != 1)
            throw new Exception();
        var child = fallback.FirstChild;
        Logger.LogDebug($"using fallback for alternate content: <{ child.Prefix }:{ child.LocalName }>");
        IInline mapped = Inline.MapRunChild(main, rprops, child);
        if (mapped is null)
            Logger.LogWarning("fallback is null");
        else
            Logger.LogDebug($"fallback is { mapped.GetType().Name }");
        return mapped;
    }

}

}