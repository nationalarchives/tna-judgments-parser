
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments {

internal class Citations {

    internal static string Normalize(string cite) {
        cite = Regex.Replace(cite, @"\s+", " ").Trim();
        Match match;
        match = Regex.Match(cite, @"^\[(\d{4})\] (UKSC|UKPC) (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[3].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { match.Groups[2].Value.ToUpper() } { num }";
        }
        match = Regex.Match(cite, @"^\[(\d{4})[\]\[] (EWCA) (Civ|Crim) (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string sub = match.Groups[3].Value.Substring(0,1).ToUpper() + match.Groups[3].Value.Substring(1).ToLower();
            string num = match.Groups[4].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { match.Groups[2].Value.ToUpper() } { sub } { num }";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] (EWCA) (\d+) \(?(Civ|Crim)\)?$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[3].Value.TrimStart('0');
            string sub = match.Groups[4].Value.Substring(0,1).ToUpper() + match.Groups[4].Value.Substring(1).ToLower();
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { match.Groups[2].Value.ToUpper() } { sub } { num }";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] (EWHC|EWCH) \[?(\d+)\]? \(?(Admin|Admlty|Ch|Comm|Costs|Fam|Pat)\)?$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[3].Value.TrimStart('0');
            string sub = match.Groups[4].Value.Substring(0,1).ToUpper() + match.Groups[4].Value.Substring(1).ToLower();
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { "EWHC" } { num } ({ sub })";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] (EWHC|EWCH) \[?(\d+)\]? \(?(IPEC|QB|TCC)\)?$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[3].Value.TrimStart('0');
            string sub = match.Groups[4].Value.ToUpper();
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { "EWHC" } { num } ({ sub })";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] (EWFC|EWCOP) (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[3].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { match.Groups[2].Value.ToUpper() } { num }";
        }
        return null;
    }

    private static string[] ExtractUriComponents(string normalized) {
        Match match;
        match = Regex.Match(normalized, @"^\[(\d{4})\] (UKSC|UKPC) (\d+)$");
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[1].Value, match.Groups[3].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] (EWCA) (Civ|Crim) (\d+)$");
        if (match.Success) 
            return new string[] { match.Groups[2].Value, match.Groups[3].Value, match.Groups[1].Value, match.Groups[4].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] (EWHC) (\d+) \((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|Pat|QB|TCC)\)$", RegexOptions.IgnoreCase);
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[4].Value, match.Groups[1].Value, match.Groups[3].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] (EWFC|EWCOP) (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[1].Value, match.Groups[3].Value };
        return null;
    }

    internal static string MakeUriComponent(string normalized) {
        string[] components = ExtractUriComponents(normalized);
        if (components is null)
            return null;
        return string.Join('/', components).ToLower();
    }

    internal static int YearFromUriComponent(string uri) {
        string year = Regex.Match(uri, @"/(\d+)/\d+$").Groups[1].Value;
        return int.Parse(year);
    }

    internal static int NumberFromUriComponent(string uri) {
        string num = Regex.Match(uri, @"/(\d+)$").Groups[1].Value;
        return int.Parse(num);
    }

}

}
