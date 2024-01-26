
using System.Text.RegularExpressions;

namespace UK.Gov.NationalArchives.Judgments.Api {

public class URI {

    public static string Domain = "https://caselaw.nationalarchives.gov.uk/";

    public static string ExtractShortURIComponent(string uri) {
        if (uri is null)
            return null;
        if (uri.StartsWith(Domain))
            uri = uri.Substring(Domain.Length);
        if (uri.StartsWith("id/"))
            uri = uri.Substring(3);
        if (uri.EndsWith("/data.xml"))
            uri = uri.Substring(0, uri.Length - 9);
        if (string.IsNullOrWhiteSpace(uri))
            return null;
        return uri;
    }

    internal static bool IsEmpty(string uri) {
        string shortened = ExtractShortURIComponent(uri);
        return string.IsNullOrEmpty(shortened);
    }

}

}
