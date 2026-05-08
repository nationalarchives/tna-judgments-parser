using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Common {

class RegulationNumber {

    // Whitespace tolerance: production .docx files have inconsistent
    // spacing in their SI numbers — "S.R. 2016" vs "S. R. 2016", "201 6"
    // (year with internal space), "No.1473" with no space, "No.   XXX".
    // Patterns use \s+ and \s* liberally, and Normalize() collapses
    // intra-digit whitespace + dotted-initial whitespace before matching.
    // Case-insensitive throughout: real docs use "No.", "NO.", "no.".
    private const RegexOptions Opts = RegexOptions.IgnoreCase;

    internal static Tuple<Regex, string>[] Patterns = new Tuple<Regex, string>[] {
        Tuple.Create( new Regex(@"^S\.?S\.?I\.?\s+(\d{4})/(\d+)$", Opts), "ssi" ),
        Tuple.Create( new Regex(@"^S\.?S\.?I\.?\s+(\d{4})/(\d+)\s+\(?C\.\s*\d+\)?$", Opts), "ssi" ),
        Tuple.Create( new Regex(@"^S\.?R\.?\s+(\d{4})\s+No\.\s*(\d+)$", Opts), "nisr" ),
        Tuple.Create( new Regex(@"^(\d{4})\s+No\.\s*(\d+)$", Opts), "uksi" ),
        // L./S. clause is sometimes written without an opening paren.
        Tuple.Create( new Regex(@"^(\d{4})\s+No\.\s*(\d+)\s+\(?[LS]\.\s*\d+\)?$", Opts), "uksi" )
    };

    private static Regex[] AltNumPatterns = new Regex[] {
        new Regex(@"^S\.?S\.?I\.?\s+\d{4}/\d+\s+\(?(C)\.\s*(\d+)\)?$", Opts),
        new Regex(@"^\d{4}\s+No\.\s*\d+\s+\(?([LS])\.\s*(\d+)\)?$", Opts)
    };

    // Patterns for *draft* SI/SR/SSI numbers — accepted by Is() so the EM
    // header splitter can still recognise the heading block, but never used
    // by MakeURI() (a draft has no real number to put in a URI).
    private static Regex[] DraftPatterns = new Regex[] {
        // Draft UK SI: "<year> No.", optionally followed by a [PLACEHOLDER]
        new Regex(@"^\d{4}\s+No\.(\s*\[[^\]]+\])?\s*$", Opts),
        // Draft NI SR: "S.R. <year> No.", optional [PLACEHOLDER]
        new Regex(@"^S\.?R\.?\s+\d{4}\s+No\.(\s*\[[^\]]+\])?\s*$", Opts),
        // Draft variant where placeholder is bare X+ (no brackets, optional
        // space — production sometimes writes "No.xxx" with no separator).
        new Regex(@"^\d{4}\s+No\.\s*X+$", Opts),
        new Regex(@"^S\.?R\.?\s+\d{4}\s+No\.\s*X+$", Opts),
        // Year + placeholder, no "No." at all (e.g. "2014 [XXXX]").
        new Regex(@"^\d{4}\s+\[[^\]]+\]$", Opts),
        // Paired-EM placeholder where the year is also missing.
        new Regex(@"^No\.\s*$", Opts)
    };

    /// <summary>
    /// Pre-normalise a regulation-number line for tolerant matching:
    ///  - "201 6"   -> "2016"      (intra-digit whitespace)
    ///  - "S. R."   -> "S.R."      (whitespace between dotted initials)
    ///  - multiple spaces collapsed to single, trimmed
    /// Used by Is() and MakeURI(); regex captures end up clean.
    /// </summary>
    internal static string Normalize(string text) {
        if (string.IsNullOrEmpty(text))
            return text;
        string s = Regex.Replace(text, @"(\d)\s+(\d)", "$1$2");
        s = Regex.Replace(s, @"([A-Z]\.)\s+([A-Z]\.?)", "$1$2");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    internal static bool Is(string text) {
        string n = Normalize(text);
        if (Patterns.Any(t => t.Item1.IsMatch(n)))
            return true;
        return DraftPatterns.Any(p => p.IsMatch(n));
    }

    internal static string MakeURI(string s) {
        string n = Normalize(s);
        foreach (var t in Patterns) {
            Match match = t.Item1.Match(n);
            if (match.Success)
                return AddYearAndNum(t.Item2, match);
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

    internal static Tuple<string, int> ExtractAltNumber(string s) {
        if (string.IsNullOrEmpty(s))
            return null;
        string n = Normalize(s);
        foreach (var pat in AltNumPatterns) {
            Match match = pat.Match(n);
            if (match.Success)
                return Tuple.Create(match.Groups[1].Value, int.Parse(match.Groups[2].Value));
        }
        return null;
    }

}

}
