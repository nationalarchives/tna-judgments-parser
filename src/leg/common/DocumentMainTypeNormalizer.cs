
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Shared jurisdiction detection for ukm:DocumentMainType values.
/// </summary>
/// <remarks>
/// "Draft" in a source label qualifies the parent instrument, not the associated document:
/// "UK Draft SI Explanatory Memorandum" is the memorandum of a draft SI, and the memorandum
/// itself is final. It is therefore never carried into the main type; the parent's status is
/// recorded on ukm:Legislation/@Class.
/// </remarks>
internal static partial class DocumentMainTypeNormalizer {

    /// <summary>
    /// Returns the CLML jurisdiction prefix for a source label, or null if none matches.
    /// </summary>
    public static string Jurisdiction(string label) {
        if (string.IsNullOrWhiteSpace(label))
            return null;
        if (ScottishRegex().IsMatch(label))
            return "Scottish";
        if (NorthernIrelandRegex().IsMatch(label))
            return "NorthernIreland";
        if (WelshRegex().IsMatch(label))
            return "Welsh";
        if (UnitedKingdomRegex().IsMatch(label))
            return "UnitedKingdom";
        return null;
    }

    /// <summary>
    /// Builds a main type value as jurisdiction + noun, falling back when no jurisdiction
    /// can be determined.
    /// </summary>
    public static string Normalize(string label, string noun, string fallback) {
        string jurisdiction = Jurisdiction(label);
        return jurisdiction is null ? fallback : jurisdiction + noun;
    }

    [GeneratedRegex(@"\b(scottish|scotland)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScottishRegex();

    // The word boundaries matter: a bare "ni" substring test also matches the "ni" inside
    // "UnitedKingdom", which previously classified UK documents as Northern Irish.
    [GeneratedRegex(@"\b(ni|northern\s+ireland)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NorthernIrelandRegex();

    [GeneratedRegex(@"\b(welsh|wales)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WelshRegex();

    [GeneratedRegex(@"\b(uk|united\s+kingdom)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UnitedKingdomRegex();

}

}
