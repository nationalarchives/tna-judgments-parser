
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

// https://support.microsoft.com/en-us/office/field-codes-hyperlink-field-864f8577-eb2a-4e55-8c90-40631748ef53

internal class Hyperlink {

    private static string regex = @"^ HYPERLINK( ""([^""]+)"")?( \\l ""([^""]+)"")?( \\o ""([^""]*)"")?( \\t ""([^""]*)"")? $";

    internal static bool Is(string fieldCode) {
        return Regex.IsMatch(fieldCode, regex);
    }

    private static ILogger Logger = Logging.Factory.CreateLogger<Hyperlink>();

    internal static List<IInline> Parse(string fieldCode, List<IInline> contents) {
        Match match = Regex.Match(fieldCode, regex);
        if (!match.Success)
            throw new Exception();
        string href = match.Groups[2].Value;
        string location = match.Groups[4].Value;
        string screenTip = match.Groups[6].Value;
        // \t switch ???
        if (string.IsNullOrEmpty(href)) {
            if (string.IsNullOrEmpty(location)) {
                Logger.LogWarning("HYPERLINK with neither href nor location: {}", fieldCode);
                return contents;
            }
            InternalLink iLink = new() { Target = location, Contents = contents };
            return [ iLink ];
        }
        if (string.IsNullOrEmpty(location))
            href += "";
        else if (location.Contains('#'))    // EWHC/Fam/2011/586
            href += location.Substring(location.IndexOf('#'));
        else
            href += "#" + location;
        var valid = Uri.TryCreate(href, UriKind.Absolute, out _);
        if (!valid) {
            Logger.LogWarning("HYPERLINK URI is not valid: {}", href);
            return contents;
        }
        var mergedContents = UK.Gov.Legislation.Judgments.Parse.Merger.Merge(contents);
        WHyperlink2 hyperlink = new WHyperlink2() { Contents = mergedContents, Href = href, ScreenTip = screenTip };
        return new List<IInline>(1) { hyperlink };
    }

}

}
