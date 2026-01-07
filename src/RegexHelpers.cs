#nullable enable

using System;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments;

public static class RegexHelpers
{
    public static Match GetFirstMatch(string input, params string[] orderedRegexPatterns)
    {
        if (orderedRegexPatterns.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(orderedRegexPatterns));
        
        Match match = null!;
        foreach (var regexPattern in orderedRegexPatterns)
        {
            match = Regex.Match(input, regexPattern);
            if (match.Success)
            {
                return match;
            }
        }

        return match;
    }
}
