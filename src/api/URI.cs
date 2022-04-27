
using System.Text.RegularExpressions;

namespace UK.Gov.NationalArchives.Judgments.Api {

class URI {

    internal static string Domain = "https://caselaw.nationalarchives.gov.uk/";

    // public static bool IsValidURIOrComponent(string uri) {
    //     string component = ExtractShortURIComponent(uri);
    //     return IsValidURIComponent(component);
    // }

    // public static bool IsValidURIComponent(string component) {
    //     if (Regex.IsMatch(component, @"^[a-z]+/[a-z]+/\d{4}/[1-9]\d*$"))
    //         return true;
    //     if (Regex.IsMatch(component, @"^[a-z]+/\d{4}/[1-9]\d*$"))
    //         return true;
    //     return false;
    // }

    internal static string ExtractShortURIComponent(string uri) {
        if (uri is null)
            return null;
        if (uri.StartsWith(Domain))
            uri = uri.Substring(Domain.Length);
        if (uri.StartsWith("id/"))
            uri = uri.Substring(3);
        if (uri.EndsWith("/data.xml"))
            uri = uri.Substring(0, uri.Length - 9);
        if (uri == "")
            return null;
        return uri;
    }

    internal static bool IsEmpty(string uri) {
        string shortened = ExtractShortURIComponent(uri);
        return string.IsNullOrEmpty(shortened);
    }

}

}
