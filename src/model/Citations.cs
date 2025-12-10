
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments {

public class Citations {

    public static string Normalize(string cite) {
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
        match = Regex.Match(cite, @"^\[?(\d{4})\]? (EWHC|EWCH|EHWC) \[?(\d+)\]? \(?(Admin|Admlty|Ch|Comm|Costs|Fam|Pat)\)?$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[3].Value.TrimStart('0');
            string sub = match.Groups[4].Value.Substring(0,1).ToUpper() + match.Groups[4].Value.Substring(1).ToLower();
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { "EWHC" } { num } ({ sub })";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] (EWHC|EWCH) \[?(\d+)\]? \(?(IPEC|KB|QB|SCCO|TCC)\)?$", RegexOptions.IgnoreCase);
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
        match = Regex.Match(cite, @"^\[(\d{4})\] EWFC (\d+) \(B\)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[2].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] EWFC { num } (B)";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] EWCOP (\d+) \((T[1-3])\)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[2].Value.TrimStart('0');
            string sub = match.Groups[3].Value.ToUpper();
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] EWCOP { num } ({ sub })";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] EWCC (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[2].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] EWCC { num }";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] EWCR (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[2].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] EWCR { num }";
        }
        match = Regex.Match(cite, $@"^\[?(\d{{4}})[\]\[] UKUT (\d+) ?\(({Courts.UpperTribunalChamberCodesPattern})\)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[2].Value.TrimStart('0');
            string sub = match.Groups[3].Value.ToUpper();
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] UKUT { num } ({ sub })";
        }
        match = Regex.Match(cite, @"^\[?(\d{4})\]? (UKAIT) (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[3].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { match.Groups[2].Value.ToUpper() } { num }";
        }
        match = Regex.Match(cite, @"^\[?(\d{4})\]? (EAT) (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[3].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] { match.Groups[2].Value.ToUpper() } { num }";
        }
        match = Regex.Match(cite, $@"^\[(\d{{4}})\] UKFTT (\d+) \(({Courts.FirstTierTribunalChamberCodesPattern})\)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[2].Value.TrimStart('0');
            string sub = match.Groups[3].Value.ToUpper();
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] UKFTT { num } ({ sub })";
        }
        match = Regex.Match(cite, @"^\[(\d{4})\] UKIPTrib (\d+)$", RegexOptions.IgnoreCase);
        if (match.Success) {
            string num = match.Groups[2].Value.TrimStart('0');
            if (!string.IsNullOrEmpty(num))
                return $"[{ match.Groups[1].Value }] UKIPTrib { num }";
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
        match = Regex.Match(normalized, @"^\[(\d{4})\] (EWHC) (\d+) \((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\)$");
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[4].Value, match.Groups[1].Value, match.Groups[3].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] (EWFC|EWCOP) (\d+)$");
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[1].Value, match.Groups[3].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] EWFC (\d+) \(B\)$");
        if (match.Success)
            return new string[] { "EWFC", "B", match.Groups[1].Value, match.Groups[2].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] EWCOP (\d+) \((T[1-3])\)$");
        if (match.Success)
            return new string[] { "EWCOP", match.Groups[3].Value, match.Groups[1].Value, match.Groups[2].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] EWCC (\d+)$");
        if (match.Success)
            return [ "EWCC", match.Groups[1].Value, match.Groups[2].Value ];
        match = Regex.Match(normalized, @"^\[(\d{4})\] EWCR (\d+)$");
        if (match.Success)
            return [ "EWCR", match.Groups[1].Value, match.Groups[2].Value ];
        match = Regex.Match(normalized, $@"^\[(\d{{4}})\] (UKUT) (\d+) \(({Courts.UpperTribunalChamberCodesPattern})\)$");
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[4].Value, match.Groups[1].Value, match.Groups[3].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] (UKAIT) (\d+)$");
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[1].Value, match.Groups[3].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] (EAT) (\d+)$");
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[1].Value, match.Groups[3].Value };
        match = Regex.Match(normalized, $@"^\[(\d{{4}})\] (UKFTT) (\d+) \(({Courts.FirstTierTribunalChamberCodesPattern})\)$");
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[4].Value, match.Groups[1].Value, match.Groups[3].Value };
        match = Regex.Match(normalized, @"^\[(\d{4})\] (UKIPTrib) (\d+)$");
        if (match.Success)
            return new string[] { match.Groups[2].Value, match.Groups[1].Value, match.Groups[3].Value };
        return null;
    }

    public static string MakeUriComponent(string normalized) {
        string[] components = ExtractUriComponents(normalized);
        if (components is null)
            return null;
        return string.Join('/', components).ToLower();
    }

    public static bool IsValidUriComponent(string uri) {
        return Regex.IsMatch(uri, @"^[a-z]+(/[a-z]+[1-3]?)?/\d{4}/\d+(/press-summary/\d+)?$");
    }

    internal static int YearFromUriComponent(string uri) {
        string year = Regex.Match(uri, @"^[a-z]+(/[a-z]+[1-3]?)?/(\d{4})/\d+").Groups[2].Value;
        return int.Parse(year);
    }

    internal static int NumberFromUriComponent(string uri) {
        string num = Regex.Match(uri, @"^[a-z]+(/[a-z]+[1-3]?)?/\d{4}/(\d+)").Groups[2].Value;
        return int.Parse(num);
    }

}

}
