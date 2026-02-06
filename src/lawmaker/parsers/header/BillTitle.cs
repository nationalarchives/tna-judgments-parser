#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record GenericBillTitle
{
    internal static WLine? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        };

        if (parser.LanguageService.IsMatch(line.NormalizedContent, BillTitleStartPatterns)?.Count >= 1)
        {
            return line;
        }

        return null;
    }

    [GeneratedRegex(@"Bill$|Measure$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegexEnglish();

    [GeneratedRegex(@"^Bil ", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegexWelsh();

    private static readonly Dictionary<LanguageService.Lang, IEnumerable<Regex>> BillTitleStartPatterns = new()
    {
        [LanguageService.Lang.EN] = [ TitleRegexEnglish() ],
        [LanguageService.Lang.CY] = [ TitleRegexWelsh() ]
    };
}