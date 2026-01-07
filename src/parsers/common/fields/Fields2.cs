
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using Fieldss = UK.Gov.Legislation.Judgments.Parse.Fieldss;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

internal class Fields2 {

    /*
    " tc " // table of contents entry
    " TA " // table of authorities entry
    " XE " // index entry
    " INDEX "
    */

    private static string[] SkipAltogether = { " PRIVATE " };

    private static Regex[] UseCurrentContents = {
        new Regex(@"^ ="),
        new Regex(@"^ ADDIN "),
        new Regex(@"^ ADVANCE \\d4 $"),
        new Regex(@"^ ASK "),
        new Regex(@"^ FILENAME \\\* MERGEFORMAT $"),
        new Regex(@"^ FILLIN "),
        new Regex(@"^ FORMTEXT "),
        new Regex(@"^ KEYWORDS "),
        new Regex(@"^ MACROBUTTON "),
        new Regex(@"^ MERGEFIELD "),
        new Regex(@"^ NUMPAGES "),
        new Regex(@"^ NUMWORDS "),
        new Regex(@"^ QUOTE "), //  wrap in <quotedText>?
        new Regex(@"^ SEQ ", RegexOptions.IgnoreCase),
        new Regex(@"^ SUBJECT "),
        new Regex(@"^ =SUM(ABOVE) "),
        new Regex(@"^ =sum(left) ", RegexOptions.IgnoreCase),
        new Regex(@"^ TOC ")  // not ideal
    };

    private static Regex[] Critical = {
        new Regex(@"^ LINK Excel.Sheet")
    };

    private static bool ShouldSkipAltogether(string code) {
        return SkipAltogether.Any(s => code.StartsWith(s, StringComparison.InvariantCultureIgnoreCase));
    }
    private static bool ShouldUseContents(string code) {
        return UseCurrentContents.Any(re => re.IsMatch(code));
    }
    private static bool IsCritical(string code) {
        return Critical.Any(re => re.IsMatch(code));
    }

    internal static Func<MainDocumentPart, Run, string, List<IInline>, List<IInline>>[] FieldHandlers = {
        AutoNum, Date, FormDropdown, Hyperlink, IncludePicture, ListNum, NoteRef, Page, Ref, Symbol, Time
    };

    private static ILogger Logger = Logging.Factory.CreateLogger<Fields2>();

    internal static List<IInline> Parse(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        
        if (ShouldSkipAltogether(code))
            return new List<IInline>(0);
        if (ShouldUseContents(code))
            return _UseContents(code, contents);
        
        
        if (IsCritical(code)) {
            Logger.LogCritical("unsupported field code {}: static contents are {}", code, IInline.ToString(contents));
            return contents;
        }
        foreach (var f in FieldHandlers) {
            List<IInline> converted = f(main, run, code, contents);
            if (converted != null)
                return converted;
        }
        Logger.LogWarning("ignoring unrecognized field code: {}", code);
        return contents;
    }

    /* */

    internal static List<IInline> AutoNum(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" AUTONUM "))
            return null;
        return new List<IInline>(1) { Fields.Autonum(main, run) };
    }

    internal static List<IInline> Date(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" DATE ") && !code.StartsWith(" createDATE "))
            return null;
        return UK.Gov.Legislation.Judgments.Parse.Time.ConvertDate(contents);
    }

    internal static List<IInline> FormDropdown(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" FORMDROPDOWN"))
            return null;
        
        string selectedValue = ExtractDropdownValue(run);
        if (!string.IsNullOrEmpty(selectedValue)) {
            var wText = new WText(selectedValue, run.RunProperties);
            return new List<IInline> { wText };
        }
        
        return contents;
    }

    private static string ExtractDropdownValue(Run run) {
        const string wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        
        try {
            var formFieldData = FindFormFieldData(run);
            if (formFieldData == null) return null;

            var dropdownData = formFieldData.ChildElements.FirstOrDefault(e => e.LocalName == "ddList");
            if (dropdownData == null) return null;

            var resultElement = dropdownData.ChildElements.FirstOrDefault(e => e.LocalName == "result");
            if (resultElement == null) return null;

            string selectedIndexText = resultElement.GetAttribute("val", wordNamespace).Value;
            if (!int.TryParse(selectedIndexText, out int selectedIndex)) return null;

            var listItems = dropdownData.ChildElements.Where(e => e.LocalName == "listEntry").ToList();
            if (selectedIndex < 0 || selectedIndex >= listItems.Count) return null;

            return listItems[selectedIndex].GetAttribute("val", wordNamespace).Value;
        } catch (Exception ex) {
            Logger.LogWarning(ex, "Error extracting dropdown value");
            return null;
        }
    }

    private static FormFieldData FindFormFieldData(Run run) {
        var paragraph = run.Parent;
        if (paragraph == null) return null;

        var allRuns = paragraph.Elements<Run>().ToList();
        int currentIndex = allRuns.IndexOf(run);
        
        // Look backwards for the field begin marker
        for (int i = currentIndex - 1; i >= 0; i--) {
            var fieldChar = allRuns[i].GetFirstChild<FieldChar>();
            if (fieldChar?.FieldCharType == FieldCharValues.Begin) {
                return fieldChar.FormFieldData;
            }
        }
        
        return null;
    }

    internal static List<IInline> Hyperlink(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!Fieldss.Hyperlink.Is(code))
            return null;
        return Fieldss.Hyperlink.Parse(code, contents);
    }

    internal static List<IInline> IncludePicture(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" INCLUDEPICTURE "))
            return null;
        if (contents.OfType<WImageRef>().Any()) {
            Logger.LogDebug("ignoring INCLUDEPICTURE field because it contains a local image");
            return contents;
        }
        string pattern1 = @"^ INCLUDEPICTURE ""([^""]+)"" ";
        string pattern2 = @"^ INCLUDEPICTURE \\d ""([^""]+)"" ";
        Match match = Regex.Match(code, pattern1);
        if (!match.Success)
            match = Regex.Match(code, pattern2);
        if (!match.Success)
            throw new Exception(code);
        if (contents.Any())
            Logger.LogWarning("ignoring unexpected contents of INCLUDEPICTURE field: " + IInline.ToString(contents));
        string url = match.Groups[1].Value;
        WExternalImage image = new WExternalImage() { URL = url };
        return new List<IInline>(1) { image };
    }

    internal static List<IInline> ListNum(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" LISTNUM "))
            return null;
        if (contents.Any())
            Logger.LogCritical($" LISTNUM with conents: { IInline.ToString(contents) }");
        INumber number = Fieldss.ListNum.Parse(main, run, code);
        return new List<IInline>(1) { number };
    }

    internal static List<IInline> NoteRef(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" NOTEREF "))
            return null;
        if (contents.Any()) {
            Logger.LogDebug($"using contents of NOTEREF: { IInline.ToString(contents) }");
            return contents;
        }
        contents = UK.Gov.Legislation.Judgments.Parse.NoteRef.Construct(main, run, code);
        Logger.LogWarning($"constructed empty NOTEREF: { IInline.ToString(contents) }");
        return contents;
    }

    private static List<IInline> Page(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" PAGE ") && !code.StartsWith(" PAGEREF "))
            return null;
        if (!contents.Any())
            return contents;
        return new List<IInline>(1) { new WPageReference { Contents = contents } };
    }

    internal static List<IInline> Ref(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" REF ", StringComparison.InvariantCultureIgnoreCase))
            return null;
        if (contents.Any()) {
            Logger.LogDebug($"using contents of REF: { IInline.ToString(contents) }");
            return contents;
        }
        contents = UK.Gov.Legislation.Judgments.Parse.Ref.Construct(main, run, code);
        Logger.LogWarning($"constructed empty REF: { IInline.ToString(contents) }");
        return contents;
    }

    internal static List<IInline> Symbol(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!code.StartsWith(" SYMBOL "))
            return null;
        SpecialCharacter c = Fieldss.Symbol.Convert(code);
        if (contents.Any()) {
            if (IInline.ToString(contents) != c.Text)
                Logger.LogWarning("SYMBOL does not equal cached contents");
        }
        return new List<IInline>(1) { c };
    }

    internal static List<IInline> Time(MainDocumentPart main, Run run, string code, List<IInline> contents) {
        if (!UK.Gov.Legislation.Judgments.Parse.Time.Is(code))
            return null;
        return UK.Gov.Legislation.Judgments.Parse.Time.Parse(code, contents);
    }

    /*  */

    private static List<IInline> _UseContents(string code, List<IInline> contents) {
        if (!contents.Any()) {
            Logger.LogWarning($"field code { code } has no contents");
            return contents;
        }
        string s = IInline.ToString(contents);
        if (string.IsNullOrWhiteSpace(s))
            Logger.LogWarning($"field code { code } has no contents");
        else
            Logger.LogDebug($"using static contents of field code { code }: { s }");
        return contents;
    }

}

}
