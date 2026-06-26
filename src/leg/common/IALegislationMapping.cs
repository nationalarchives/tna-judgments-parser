
using System;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Provides lookup functionality for Impact Assessment to Legislation mappings.
/// Reads from the combined associated document mapping CSV.
/// </summary>
internal static partial class IALegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(IALegislationMapping));

    /// <summary>
    /// The components of an IA filename: the series prefix (ukia/ssifia/sdsifia), the
    /// IA's own year/number when it carries a ukia-style YYYYNNNN identity, and the
    /// version from any trailing _NNN suffix. <see cref="HasYearNumber"/> is false for
    /// ISBN-numbered drafts (e.g. sdsifia), which have no independent year/number.
    /// </summary>
    public record IAFileId(string Series, int? Year, string Number, int Version, bool HasYearNumber);

    /// <summary>
    /// Gets the full mapping record for the given IA filename. Looks up the CSV by the
    /// actual filename (consistent with the EM/EN/TN/CoP/OD mappings) so every IA
    /// jurisdiction resolves, not just UK ukia.
    /// </summary>
    public static IAMappingRecord GetMappingRecord(string filename) {
        IAFileId fileId = ParseIAFilename(filename);
        var record = AssociatedDocumentMapping.GetRecord(filename);

        if (record is null) {
            // Not in the CSV: a ukia still has a standalone ukia/{year}/{number} URI, so
            // build a minimal record from the filename for that fallback. Other schemes
            // have no standalone form and stay unmapped.
            if (fileId is not { HasYearNumber: true } || fileId.Series != "ukia")
                return null;
            return new IAMappingRecord {
                IaSeries = fileId.Series,
                UkiaYear = fileId.Year,
                UkiaNumber = int.TryParse(fileId.Number, out int fn) ? (int?) fn : null,
                ImpactsYear = fileId.Year.Value.ToString(),
                ImpactsNumber = fileId.Number,
            };
        }

        // The IA's identity within /impacts: a ukia IA has its own series year/number
        // (independent of the parent legislation); a Scottish SI/Draft IA is identified
        // by its parent legislation (SI number or draft ISBN), so fall back to that.
        string impactsYear = null, impactsNumber = null;
        if (fileId is { HasYearNumber: true }) {
            impactsYear = fileId.Year.Value.ToString();
            impactsNumber = fileId.Number;
        } else {
            var leg = ParseLegislationUri(record.LegislationUri);
            if (leg.HasValue) {
                impactsYear = leg.Value.year.ToString();
                impactsNumber = leg.Value.number;
            }
        }

        int? ukiaNumber = null;
        if (fileId is { HasYearNumber: true } && int.TryParse(fileId.Number, out int n))
            ukiaNumber = n;

        return new IAMappingRecord {
            UkiaUri = record.DocumentUri,
            UkiaYear = fileId is { HasYearNumber: true } ? fileId.Year : null,
            UkiaNumber = ukiaNumber,
            IaSeries = fileId?.Series,
            ImpactsYear = impactsYear,
            ImpactsNumber = impactsNumber,
            Title = record.DocumentTitle,
            IADate = record.DocumentDate,
            DocumentStage = record.DocumentStage,
            DocumentMainType = record.DocumentType,
            Department = record.Department,
            ModifiedDate = record.ModifiedDate,
            LegislationUri = record.LegislationUri,
            LegislationClass = record.LegislationClass,
            LegislationYear = record.LegislationYear,
            LegislationNumber = record.LegislationNumber
        };
    }

    /// <summary>
    /// Parses an IA filename into its series prefix, optional ukia-style year/number,
    /// and version. Handles ukia_YYYYNNNN_en, ssifia_YYYYNNNN_en, sdsifia_&lt;ISBN&gt;_en,
    /// each optionally with a trailing _NNN version suffix.
    /// </summary>
    public static IAFileId ParseIAFilename(string filename) {
        if (string.IsNullOrEmpty(filename))
            return null;

        string name = Path.GetFileNameWithoutExtension(filename.Trim());

        int version = 1;
        Match vm = VersionSuffixRegex().Match(name);
        if (vm.Success) {
            name = vm.Groups["base"].Value;
            int.TryParse(vm.Groups["v"].Value, out version);
        }

        Match m = SeriesRegex().Match(name);
        if (!m.Success) {
            logger.LogWarning("IA filename '{Filename}' does not match <series>_<id>_en", filename);
            return null;
        }

        string series = m.Groups["series"].Value.ToLowerInvariant();
        string id = m.Groups["id"].Value;

        // A ukia-style identity is 8 digits (4-digit year + 4-digit number). ISBNs used
        // by Scottish/UK drafts are 13 digits and carry no independent year/number.
        if (Regex.IsMatch(id, "^[0-9]{8}$")) {
            int year = int.Parse(id[..4]);
            int number = int.Parse(id[4..]);
            if (year is >= 1900 and <= 2099)
                return new IAFileId(series, year, number.ToString(), version, true);
        }

        return new IAFileId(series, null, id, version, false);
    }

    [GeneratedRegex(@"^(?<base>.+_en)_(?<v>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionSuffixRegex();

    [GeneratedRegex(@"^(?<series>[a-z]+)_(?<id>[0-9a-z]+)_en$", RegexOptions.IgnoreCase)]
    private static partial Regex SeriesRegex();

    /// <summary>
    /// Builds a UKIA URI from year and number.
    /// </summary>
    public static string BuildUkiaUri(int year, int number) {
        return $"http://www.legislation.gov.uk/id/ukia/{year}/{number}";
    }

    /// <summary>
    /// Normalizes a document stage value to URL-safe lowercase format.
    /// </summary>
    public static string NormalizeStage(string stage) {
        if (string.IsNullOrWhiteSpace(stage))
            return null;

        string normalized = stage.Trim().ToLowerInvariant();

        return normalized switch {
            "final" => "final",
            "enactment" => "enactment",
            "consultation" => "consultation",
            "development" => "development",
            "implementation" => "implementation",
            "options" => "options",
            "post-implementation" or "postimplementation" or "post implementation" => "post-implementation",
            _ => null
        };
    }

    /// <summary>
    /// Builds the short URI component for an Impact Assessment.
    /// Format: {legislation-type}/{leg-year}/{leg-number}/impacts/{ia-year}/{ia-number}
    /// where the impacts identity is the IA's ukia year/number (UK) or the parent
    /// legislation's year/number (Scottish). Falls back to ukia/{year}/{number} for a
    /// UK standalone IA with no legislation link.
    /// </summary>
    public static string BuildShortUriComponent(IAMappingRecord record) {
        if (record is null)
            return null;

        var components = ParseLegislationUri(record.LegislationUri);
        if (components.HasValue) {
            var (type, legYear, legNumber) = components.Value;
            if (!string.IsNullOrEmpty(record.ImpactsYear) && !string.IsNullOrEmpty(record.ImpactsNumber))
                return $"{type}/{legYear}/{legNumber}/impacts/{record.ImpactsYear}/{record.ImpactsNumber}";
            logger.LogWarning("IA for legislation {Uri} has no impacts identity; using bare /impacts path", record.LegislationUri);
            return $"{type}/{legYear}/{legNumber}/impacts";
        }

        // No legislation link: a UK ukia IA still has a standalone URI; a Scottish IA
        // has no standalone scheme, so there is nothing valid to build.
        if (record.UkiaYear.HasValue && !string.IsNullOrEmpty(record.ImpactsNumber)) {
            logger.LogWarning("No legislation mapping for ukia {Year}/{Number}, using standalone fallback URI", record.ImpactsYear, record.ImpactsNumber);
            return $"ukia/{record.ImpactsYear}/{record.ImpactsNumber}";
        }

        logger.LogWarning("IA has no legislation mapping and no ukia identity; cannot build short URI");
        return null;
    }

    /// <summary>
    /// Parses a legislation URI to extract type, year, and number.
    /// </summary>
    public static (string type, int year, string number)? ParseLegislationUri(string legislationUri) {
        if (string.IsNullOrEmpty(legislationUri))
            return null;

        Match match = LegislationUriRegex().Match(legislationUri);
        if (!match.Success) {
            logger.LogWarning("Legislation URI '{Uri}' does not match expected pattern", legislationUri);
            return null;
        }

        string type = match.Groups[1].Value;
        string yearStr = match.Groups[2].Value;
        string number = match.Groups[3].Value;

        if (!int.TryParse(yearStr, out int year)) {
            logger.LogWarning("Legislation URI '{Uri}' has non-numeric year", legislationUri);
            return null;
        }

        return (type, year, number);
    }

    [GeneratedRegex(@"^https?://www\.legislation\.gov\.uk/id/([^/]+)/(\d{4})/(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LegislationUriRegex();

}

}
