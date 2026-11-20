#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers;

internal interface ILineDateEnricher
{
    WLine Enrich(WLine line, string name, int priority);
}

internal partial class LineDateEnricher : ILineDateEnricher
{
    private readonly CultureInfo enGbCulture = CultureInfo.GetCultureInfo("en-GB");

    private readonly string[] supportedDatePatterns =
    [
        "d/MM/yyyy",
        "d MMM yyyy",
        "d MMMM yyyy",
        "ddd d MMM yyyy",
        "ddd d MMMM yyyy",
        "dddd d MMM yyyy",
        "dddd d MMMM yyyy",
        "d \"day of\" MMM yyyy",
        "d \"day of\" MMMM yyyy"
    ];

    public WLine Enrich(WLine line, string name, int priority)
    {
        var (inlinesBefore, lastTextGroupInlines, inlinesAfter) = SplitAroundLastTextGroup(line);

        var lastTextGroupText = IInline.ToString(lastTextGroupInlines);

        // Ignore whitespace at the beginning and end of the string
        var potentialDateStartIndex = lastTextGroupText.Length - lastTextGroupText.TrimStart().Length;
        var potentialDateEndIndex = lastTextGroupText.TrimEnd().Length;

        while (potentialDateStartIndex < potentialDateEndIndex)
        {
            var potentialDateText = lastTextGroupText[potentialDateStartIndex..potentialDateEndIndex];

            if (TryGetDateFromString(potentialDateText, out var date))
            {
                var enrichedInlines = EnrichFromBeginning.Enrich(
                    lastTextGroupInlines,
                    potentialDateStartIndex,
                    potentialDateEndIndex,
                    dateInlines => dateInlines.All(inline => inline is IFormattedText)
                        ? new WDocDate(dateInlines.Cast<IFormattedText>(), date.ToDateTime(TimeOnly.MinValue))
                        {
                            Name = name,
                            Priority = priority
                        }
                        : null);

                if (enrichedInlines.Any(inline => inline is WDocDate))
                {
                    return WLine.Make(line, [.. inlinesBefore, .. enrichedInlines, .. inlinesAfter]);
                }

                return line;
            }

            potentialDateStartIndex += GetIndexOfNextWord(potentialDateText);
        }

        return line;
    }

    /// <summary>
    /// We can only enrich IFormattedText with a DocDate so skip all non IFormattedText at the beginning and end of the line.
    /// If there are multiple runs of IFormattedText in the line, we only enrich the last one.
    /// </summary>
    /// <returns>The inlines before the last group of consecutive IFormattedText, the group itself, and the inlines after it.</returns>
    private static (IInline[] before, IInline[] lastTextGroupInlines, IInline[] after) SplitAroundLastTextGroup(WLine line)
    {
        IInline[] allInlines = [.. line.Contents];

        var lastTextInline = Array.FindLastIndex(allInlines, inline => inline is IFormattedText);
        if (lastTextInline == -1)
        {
            // There are no `IFormattedText`s in the line.
            return (allInlines, [], []);
        }

        var firstTextInline = Array.FindLastIndex(allInlines, lastTextInline, inline => inline is not IFormattedText) + 1;

        return (allInlines[..firstTextInline], allInlines[firstTextInline..(lastTextInline + 1)], allInlines[(lastTextInline + 1)..]);
    }

    private bool TryGetDateFromString(string input, out DateOnly date)
    {
        var cleanedDateToParse = OrdinalRegex().Replace(input, "${dayNumber}").CleanWhitespace();

        return DateOnly.TryParseExact(cleanedDateToParse, supportedDatePatterns, enGbCulture.DateTimeFormat,
            DateTimeStyles.AllowWhiteSpaces, out date);
    }

    private static int GetIndexOfNextWord(string s)
    {
        var index = 0;

        // go to end of the current word
        while (index < s.Length && char.IsLetterOrDigit(s[index]))
        {
            index++;
        }

        // go to start of the next word
        while (index < s.Length && !char.IsLetterOrDigit(s[index]))
        {
            index++;
        }

        return index;
    }

    [GeneratedRegex(@"(?<dayNumber>\d{1,2}\s*)(?<ordinal>st|nd|rd|th)", RegexOptions.IgnoreCase)]
    private partial Regex OrdinalRegex();
}
