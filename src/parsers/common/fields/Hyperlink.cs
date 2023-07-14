
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-hyperlink-field-864f8577-eb2a-4e55-8c90-40631748ef53

internal class Hyperlink {

    private static string regex = @"^ HYPERLINK( ""([^""]+)"")?( \\l ""([^""]+)"")?( \\o ""([^""]*)"")?( \\t ""([^""]*)"")? $";

    internal static bool Is(string fieldCode) {
        return Regex.IsMatch(fieldCode, regex);
    }

    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, List<OpenXmlElement> withinField, int i) {
        Match match = Regex.Match(fieldCode, regex);
        if (!match.Success)
            throw new Exception();
        string href = match.Groups[2].Value;
        string location = match.Groups[4].Value;
        string screenTip = match.Groups[6].Value;
        // \t switch ???
        if (string.IsNullOrEmpty(href)) {   // EWHC/Ch/2018/2285
            Fields.logger.LogWarning("cross-references are not yet supported: " + fieldCode);
            return Fields.RestOptional(main, withinField, i);   // optional for EWCA/Civ/2009/296
        }
        if (string.IsNullOrEmpty(location))
            href += "";
        else if (location.Contains('#'))    // EWHC/Fam/2011/586
            href += location.Substring(location.IndexOf('#'));
        else
            href += "#" + location;
        if (i == withinField.Count) {
            RunProperties rProps = withinField.OfType<Run>().FirstOrDefault()?.RunProperties;
            // log?
            WText wText = new WText(screenTip, rProps);
            WHyperlink1 hyperlink = new WHyperlink1(wText) { Href = href };
            return new List<IInline>(1) { hyperlink };
        } else {
            OpenXmlElement next = withinField[i];
            if (next is InsertedRun && !next.ChildElements.Any()) { // EWCA/Civ/2017/8.rtf
                next = withinField[++i];
            }
            if (!Fields.IsFieldSeparater(next))
                throw new Exception();
            IEnumerable<OpenXmlElement> rest = withinField.Skip(i + 1);
            IEnumerable<IInline> contents = Inline.ParseRuns(main, rest);
            WHyperlink2 hyperlink = new WHyperlink2() { Contents = contents, Href = href, ScreenTip = screenTip };
            return new List<IInline>(1) { hyperlink };
        }
    }

    internal static List<IInline> Parse(string fieldCode, List<IInline> contents) {
        Match match = Regex.Match(fieldCode, regex);
        if (!match.Success)
            throw new Exception();
        string href = match.Groups[2].Value;
        string location = match.Groups[4].Value;
        string screenTip = match.Groups[6].Value;
        // \t switch ???
        if (string.IsNullOrEmpty(href)) {   // EWHC/Ch/2018/2285
            Fields.logger.LogWarning("cross-references are not yet supported: " + fieldCode);
            return contents;
        }
        if (string.IsNullOrEmpty(location))
            href += "";
        else if (location.Contains('#'))    // EWHC/Fam/2011/586
            href += location.Substring(location.IndexOf('#'));
        else
            href += "#" + location;
        var mergedContents = UK.Gov.Legislation.Judgments.Parse.Merger.Merge(contents);
        WHyperlink2 hyperlink = new WHyperlink2() { Contents = mergedContents, Href = href, ScreenTip = screenTip };
        return new List<IInline>(1) { hyperlink };
    }

}

}
