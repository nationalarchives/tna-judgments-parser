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
        "dddd d MMMM yyyy"
    ];

    public WLine Enrich(WLine line, string name, int priority)
    {
        if (!line.Contents.All(inline => inline is ITextOrWhitespace))
        {
            return line;
        }

        var originalText = IInline.ToString(line.Contents);

        var startIndexOfDate = 0;
        var endIndexOfDate = originalText.TrimEnd().Length;

        while (startIndexOfDate < endIndexOfDate)
        {
            var potentialDateString = originalText[startIndexOfDate..endIndexOfDate];

            if (TryGetDateFromString(potentialDateString, out var date))
            {
                var enriched = EnrichFromBeginning.Enrich(
                    line.Contents.ToArray(),
                    startIndexOfDate,
                    endIndexOfDate,
                    inside => inside.All(inline => inline is IFormattedText)
                        ? new WDocDate(inside.Cast<IFormattedText>(), date.ToDateTime(TimeOnly.MinValue))
                        {
                            Name = name,
                            Priority = priority
                        }
                        : null);

                return enriched.Any(inline => inline is WDocDate) ? WLine.Make(line, enriched) : line;
            }

            startIndexOfDate += GetIndexOfNextWord(potentialDateString);
        }

        return line;
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
