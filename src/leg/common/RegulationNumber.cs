using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Recognises UK / NI / Scottish statutory-instrument numbers in their
/// many real-world spellings. Used by the EM header splitter to decide
/// whether a paragraph is the regulation-number line that terminates
/// the header.
///
/// Two parallel sets of patterns:
///
///  * <see cref="Patterns"/> — fully-formed numbers ("2013 No. 1669",
///    "S.R. 2016 No. 32", "S.S.I. 2014/123"). Match here AND
///    <see cref="MakeURI"/> returns a legislation.gov.uk path.
///
///  * <see cref="UnassignedNumberPatterns"/> — number-shaped strings
///    that have no real number yet (drafts with "[XXXX]", "****",
///    "X+" placeholders, paired-EM "No." with no value, etc.).
///    Match here so <see cref="Is"/> accepts them, but MakeURI does
///    NOT consider them (drafts have no URI).
///
/// All matching is case-insensitive ("No." / "NO." / "no." all match)
/// and runs against the <see cref="Normalize"/>'d text — see that
/// method for the source-text repairs that happen before regex.
/// </summary>
class RegulationNumber {

    private const RegexOptions Opts = RegexOptions.IgnoreCase;

    // ----- fully-formed numbers (URI-generating) -----

    /// <summary>UK statutory instrument: "&lt;year&gt; No. &lt;num&gt;",
    /// optionally with an "(L. 17)" / "(S. 4)" suffix (opening paren
    /// sometimes omitted in production).</summary>
    private static readonly Tuple<Regex, string>[] UkSiPatterns = {
        Tuple.Create(new Regex(@"^(\d{4})\s+No\.\s*(\d+)$", Opts), "uksi"),
        Tuple.Create(new Regex(@"^(\d{4})\s+No\.\s*(\d+)\s+\(?[LS]\.\s*\d+\)?$", Opts), "uksi"),
    };

    /// <summary>NI statutory rule: "S.R. &lt;year&gt; No. &lt;num&gt;"
    /// or the year-first variant "&lt;year&gt; SR No. &lt;num&gt;".</summary>
    private static readonly Tuple<Regex, string>[] NiSrPatterns = {
        Tuple.Create(new Regex(@"^S\.?R\.?\s+(\d{4})\s+No\.\s*(\d+)$", Opts), "nisr"),
        Tuple.Create(new Regex(@"^(\d{4})\s+S\.?R\.?\s+No\.\s*(\d+)$", Opts), "nisr"),
    };

    /// <summary>Scottish statutory instrument: "S.S.I. &lt;year&gt;/&lt;num&gt;",
    /// optionally with a "(C. 12)" commencement clause.</summary>
    private static readonly Tuple<Regex, string>[] SsiPatterns = {
        Tuple.Create(new Regex(@"^S\.?S\.?I\.?\s+(\d{4})/(\d+)$", Opts), "ssi"),
        Tuple.Create(new Regex(@"^S\.?S\.?I\.?\s+(\d{4})/(\d+)\s+\(?C\.\s*\d+\)?$", Opts), "ssi"),
    };

    internal static readonly Tuple<Regex, string>[] Patterns =
        UkSiPatterns.Concat(NiSrPatterns).Concat(SsiPatterns).ToArray();

    // ----- alternate suffix parser (extracts L/S/C subseries number) -----

    private static readonly Regex[] AltNumPatterns = {
        new(@"^S\.?S\.?I\.?\s+\d{4}/\d+\s+\(?(C)\.\s*(\d+)\)?$", Opts),
        new(@"^\d{4}\s+No\.\s*\d+\s+\(?([LS])\.\s*(\d+)\)?$", Opts),
    };

    // ----- number-shaped-but-unassigned (accepted by Is, never by MakeURI) -----
    //
    // Drafts in production use varied placeholder conventions: bracketed
    // text ([XXXX], [DRAFT]), bare X+, asterisks (****), broken brackets
    // ([1751 without closing). Real docs sometimes drop the "." after No
    // ("NO" instead of "No."). Each variant is a separate pattern so each
    // one is easy to grep for from a test failure.
    private static readonly Regex[] UnassignedNumberPatterns = {
        // UK SI shapes
        new(@"^\d{4}\s+No\.?(\s*\[[^\]]+\])?\s*$", Opts),  // year + No[.] + optional [PLACEHOLDER]
        new(@"^\d{4}\s+No\.\s*X+$",          Opts),        // year + No. + bare X+
        new(@"^\d{4}\s+No\.\s*\*+$",         Opts),        // year + No. + asterisks
        new(@"^\d{4}\s+No\.\s*\[\d+$",       Opts),        // year + No. + "[<digits>" (broken bracket)
        new(@"^\d{4}\s+\[[^\]]+\]$",         Opts),        // year + bracketed placeholder, no "No."

        // NI SR shapes
        new(@"^S\.?R\.?\s+\d{4}\s+No\.?(\s*\[[^\]]+\])?\s*$", Opts),
        new(@"^S\.?R\.?\s+\d{4}\s+No\.\s*X+$",                Opts),
        new(@"^S\.?R\.?\s+No\.\s*\d+$",                       Opts),  // year-less SR

        // Paired-EM where the second number is just "No."
        new(@"^No\.\s*$", Opts),
    };

    // ----- public API -----

    internal static bool Is(string text) {
        string n = Normalize(text);
        return Patterns.Any(t => t.Item1.IsMatch(n))
            || UnassignedNumberPatterns.Any(p => p.IsMatch(n));
    }

    internal static string MakeURI(string text) {
        string n = Normalize(text);
        foreach (var t in Patterns) {
            Match match = t.Item1.Match(n);
            if (match.Success)
                return AddYearAndNum(t.Item2, match);
        }
        return null;
    }

    internal static Tuple<string, int> ExtractAltNumber(string text) {
        if (string.IsNullOrEmpty(text))
            return null;
        string n = Normalize(text);
        foreach (var pat in AltNumPatterns) {
            Match match = pat.Match(n);
            if (match.Success)
                return Tuple.Create(match.Groups[1].Value, int.Parse(match.Groups[2].Value));
        }
        return null;
    }

    private static string AddYearAndNum(string type, Match match) {
        string year = match.Groups[1].Value;
        string num = match.Groups[2].Value.TrimStart('0');
        if (string.IsNullOrEmpty(num))
            num = "0";
        return $"{type}/{year}/{num}";
    }

    // ----- normalisation -----
    //
    // Production .docx files have inconsistent inner spacing in their SI
    // numbers. Normalize repairs the most common corruptions before regex
    // matching so the patterns can stay declarative. Each step has its
    // own unit-test row in TestRegulationNumber.

    /// <summary>
    /// Apply each repair step in order and return the cleaned string.
    /// </summary>
    internal static string Normalize(string text) {
        if (string.IsNullOrEmpty(text))
            return text;
        string s = CollapseIntraDigitWhitespace(text);
        s = CollapseDottedInitials(s);
        s = RepairInterruptedNo(s);
        s = CollapseMultipleSpaces(s);
        return s;
    }

    /// <summary>"201 6" → "2016". A space inside a four-digit year (a
    /// common Word-style corruption) is meaningless and must be removed
    /// before regex matching.</summary>
    internal static string CollapseIntraDigitWhitespace(string text) =>
        Regex.Replace(text, @"(\d)\s+(\d)", "$1$2");

    /// <summary>"S. R." → "S.R.". Collapses whitespace between two
    /// adjacent *dotted* initials (e.g. SR / SSI / L. S.). The trailing
    /// dot on the second initial is REQUIRED — otherwise "R. No." would
    /// be collapsed to "R.No." (treating N as a single-letter initial).
    /// </summary>
    internal static string CollapseDottedInitials(string text) =>
        Regex.Replace(text, @"([A-Z]\.)\s+([A-Z]\.)", "$1$2");

    /// <summary>"N o." → "No.". Some production files have the "No"
    /// run broken across two text runs with whitespace in the middle.
    /// </summary>
    internal static string RepairInterruptedNo(string text) =>
        Regex.Replace(text, @"\bN\s+o\.", "No.", RegexOptions.IgnoreCase);

    /// <summary>Multiple spaces / tabs / newlines → single space, then
    /// trim. Run last so earlier steps see the original whitespace
    /// structure.</summary>
    internal static string CollapseMultipleSpaces(string text) =>
        Regex.Replace(text, @"\s+", " ").Trim();

}

}
