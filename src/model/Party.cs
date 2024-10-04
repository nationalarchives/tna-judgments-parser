using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments {

public class Party {

    public static string MakeName(string text) {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.StartsWith("Mr "))
            text = text.Substring(3);
        if (text.StartsWith("Mrs "))
            text = text.Substring(4);
        if (text.StartsWith("Miss "))
            text = text.Substring(5);
        Match match = Regex.Match(text, @"^\(\d+\) ?"); // EWCA/Civ/2005/450
        if (match.Success)
            text = text.Substring(match.Length);
        match = Regex.Match(text, @"^\d+\. ?");  // EWHC/Ch/2004/1504
        if (match.Success)
            text = text.Substring(match.Length);
        match = Regex.Match(text, @"^\d+\) ?"); // EWCA/Civ/2018/2167
        if (match.Success)
            text = text.Substring(match.Length);
        match = Regex.Match(text, @" \(\d+\)$");
        if (match.Success)
            text = text.Substring(0, text.Length - match.Length);
        match = Regex.Match(text, @" \(the “[^”]+”\)$");
        if (match.Success)
            text = text.Substring(0, match.Index);
        if (text.EndsWith(" (in administration)"))
            text = text.Substring(0, text.Length - 20);
        if (text.EndsWith(" in administration"))
            text = text.Substring(0, text.Length - 18);
        if (text == "R E G I N A")
            return "REGINA";
        match = Regex.Match(text, @"^\(?on the application of ([A-Z][A-Z0-9 ,]*)\)?$", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;
        match = Regex.Match(text, @"^The (King|Queen) \(?on the application of ([A-Z][A-Z0-9 ,]*)\)?$", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[2].Value;
        match = Regex.Match(text, @"^The (King|Queen) on the application of ([A-Z][A-Z0-9 ]*[A-Z]) \([A-Z]+\)$", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[2].Value;

        match = Regex.Match(text, @"^[A-Z]+ \(a child, by");
        if (match.Success)
            return text.Substring(0, text.IndexOf('(')) + "(a child)";
        // if (text.EndsWith(')')) {
        //     int i = text.LastIndexOf('(');
        //     if (i > 0) {
        //         string text2 = text.Substring(0, i).TrimEnd();
        //         if (!string.IsNullOrWhiteSpace(text2))
        //             return text2;
        //     }
        // }
        return text;
    }

}

}
