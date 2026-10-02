#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;

using NationalArchives.FindCaseLaw.Utils;

namespace UK.Gov.Legislation.Judgments.Parse;

internal interface ICourtExtractor
{
    Court? FromCitationAndDate(string cite, DateOnly? decisionDate);
}

internal class CourtExtractor(TimeProvider dateTimeProvider) : ICourtExtractor
{
    internal CourtExtractor() : this(TimeProvider.System) { }

    private static readonly DateOnly DateBusinessPropertyDivisionCreated = new(2026, 10, 01);
    private static readonly CourtStore CourtStore = new();

    public Court? FromCitationAndDate(string cite, DateOnly? decisionDate)
    {
        var cleanedCite = cite.CleanWhitespace();

        if (string.IsNullOrWhiteSpace(cleanedCite))
        {
            return null;
        }

        var fclCourtMatchingCites = CourtStore
                                    .Where(c => c.NcnPattern is not null
                                                && Regex.IsMatch(cleanedCite, RegexHelpers.AddAnchors(c.NcnPattern)))
                                    .ToArray();

        if (fclCourtMatchingCites.Length == 0)
        {
            return null;
        }

        if (fclCourtMatchingCites.Length == 1)
        {
            return new Court(fclCourtMatchingCites.Single());
        }

        // If there was no date provided then assume a decision date of today
        decisionDate ??= DateOnly.FromDateTime(dateTimeProvider.GetUtcNow().Date);

        // The NCN pattern matches multiple courts so filter out any courts created in years after this decision occurred
        var filteredCourts = fclCourtMatchingCites.Where(c => c.StartYear <= decisionDate.Value.Year);

        // We have a precise date for the start of the business property division
        if (decisionDate < DateBusinessPropertyDivisionCreated)
        {
            filteredCourts = filteredCourts
                .Where(c => !c.Code.Contains("BPD"));
        }
        else
        {
            filteredCourts = filteredCourts
                .Where(c => !c.Code.Contains("KBD")
                    && !c.Code.Contains("QBD")
                    && !c.Code.Contains("Chancery"));
        }

        // There may still be more than one match so sort so the least specific match comes first (and also to prefer KBD over QBD)
        return new Court(filteredCourts.OrderBy(c => c.Code).First());
    }
}
