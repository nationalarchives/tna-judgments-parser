
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class RegulationNumber {

    internal static Regex[] Patterns = {
        new Regex(@"^SSI \d{4}/\d+$"),
        new Regex(@"^S\.?R\.? \d{4} No\. \d+"),
        new Regex(@"^\d{4} No\. \d+$")
    };

    internal static bool Is(string text) {
        return Patterns.Any(pattern => pattern.IsMatch(text));
    }

    internal static string MakeURI(string s) {
        Match match;
        match = Regex.Match(s, @"^SSI \d{4}/\d+$");
        if (match.Success)
            return AddYearAndNum("ssi", match);
        match = Regex.Match(s, @"^S\.?R\.? (\d{4}) No\. (\d+)$");
        if (match.Success)
            return AddYearAndNum("nisr", match);
        match = Regex.Match(s, @"^(\d{4}) No\. (\d+)$");
        if (match.Success)
            return AddYearAndNum("uksi", match);
        return null;
    }

    private static string AddYearAndNum(string type, Match match) {
        string year = match.Groups[1].Value;
        string num = match.Groups[2].Value.TrimStart('0');
        if (string.IsNullOrEmpty(num))
            num = "0";
        return $"{type}/{year}/{num}";
    }

}

}
