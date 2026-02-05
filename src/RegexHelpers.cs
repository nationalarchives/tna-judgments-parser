#nullable enable

using System;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments;

public static partial class RegexHelpers
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

    /// <summary>
    /// Returns a string with only single spaces instead of arbitrary whitespace, and no spaces at the start or end
    /// </summary>
    public static string CleanWhitespace(this string input)
    {
        return WhitespaceRegex().Replace(input, " ").Trim();
    }

    [GeneratedRegex(@"\s+")] private static partial Regex WhitespaceRegex();
    
    /// <summary>
    /// Returns the regex pattern with '^' at the start and '$' at the end
    /// </summary>
    public static string AddAnchors(string pattern)
    {
        var anchoredPattern = pattern;
        
        if (!anchoredPattern.StartsWith('^'))
            anchoredPattern = $"^{anchoredPattern}";
        
        if (!anchoredPattern.EndsWith('$'))
            anchoredPattern += "$";
        
        return anchoredPattern;
    }

}
