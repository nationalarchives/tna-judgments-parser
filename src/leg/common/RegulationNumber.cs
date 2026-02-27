using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Common {

class RegulationNumber {

    internal static Tuple<Regex, string>[] Patterns = new Tuple<Regex, string>[] {
        Tuple.Create( new Regex(@"^S\.?S\.?I\.? (\d{4})/(\d+)$"), "ssi" ),
        Tuple.Create( new Regex(@"^S\.?S\.?I\.? (\d{4})/(\d+) \(?C\. \d+\)?$"), "ssi" ),
        Tuple.Create( new Regex(@"^S\.?R\.? (\d{4}) No\. (\d+)$"), "nisr" ),
        Tuple.Create( new Regex(@"^(\d{4}) No\. (\d+)$"), "uksi" ),
        Tuple.Create( new Regex(@"^(\d{4}) No\. (\d+) \([LS]\. \d+\)$"), "uksi" )
    };

    private static Regex[] AltNumPatterns = new Regex[] {
        new Regex(@"^S\.?S\.?I\.? \d{4}/\d+ \(?(C)\. (\d+)\)?$"),
        new Regex(@"^\d{4} No\. \d+ \(([LS])\. (\d+)\)$")
    };

    internal static bool Is(string text) {
        return Patterns.Any(t => t.Item1.IsMatch(text));
    }

    internal static string MakeURI(string s) {
        foreach (var t in Patterns) {
            Match match = t.Item1.Match(s);
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
        foreach (var pat in AltNumPatterns) {
            Match match = pat.Match(s);
            if (match.Success)
                return Tuple.Create(match.Groups[1].Value, int.Parse(match.Groups[2].Value));
        }
        return null;
    }

}

}
