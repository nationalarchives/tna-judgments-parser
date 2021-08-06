
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-hyperlink-field-864f8577-eb2a-4e55-8c90-40631748ef53

internal class Hyperlink {

    private static string regex = @"^ HYPERLINK ""([^""]+)""( \\l ""([^""]+)"")?( \\o ""([^""]*)"")?( \\t ""([^""]*)"")? $";

    internal static bool Is(string fieldCode) {
        return Regex.IsMatch(fieldCode, regex);
    }

    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, List<OpenXmlElement> withinField, int i) {
        Match match = Regex.Match(fieldCode, regex);
        if (!match.Success)
            throw new Exception();
        string href = match.Groups[1].Value;
        string location = match.Groups[3].Value;
        string screenTip = match.Groups[5].Value;
        // \t switch ???
        if (string.IsNullOrEmpty(location))
            href += "";
        else if (location.Contains('#'))    // EWHC/Fam/2011/586
            href += location.Substring(location.IndexOf('#'));
        else
            href += "#" + location;
        if (i == withinField.Count) {
            RunProperties rProps = withinField.OfType<Run>().FirstOrDefault()?.RunProperties;
            WText wText = new WText(screenTip, rProps);
            WHyperlink1 hyperlink = new WHyperlink1(wText) { Href = href };
            return new List<IInline>(1) { hyperlink };
        } else {
            OpenXmlElement next = withinField[i];
            if (!Fields.IsFieldSeparater(next))
                throw new Exception();
            IEnumerable<OpenXmlElement> rest = withinField.Skip(i + 1);
            IEnumerable<IInline> contents = Inline.ParseRuns(main, rest);
            WHyperlink2 hyperlink = new WHyperlink2() { Contents = contents, Href = href, ScreenTip = screenTip };
            return new List<IInline>(1) { hyperlink };
        }
    }

}

}
