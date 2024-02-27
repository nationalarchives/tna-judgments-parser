
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class Bookmarks {

    internal static bool IsBookmark(OpenXmlElement e) {
        if (e is BookmarkStart)
            return true;
        if (e is OpenXmlUnknownElement && e.LocalName == "bookmarkStart")
            return true;
        return false;
    }

    internal static WBookmark Parse(OpenXmlElement e) {
        if (e is BookmarkStart start)
            return Parse(start);
        if (e is OpenXmlUnknownElement unknown && e.LocalName == "bookmarkStart")
            return Parse(unknown);
        throw new System.ArgumentException(e.LocalName);
    }

    internal static WBookmark Parse(BookmarkStart start) {
        string name = start.Name;
        if (name is null)
            return null;
        return new() { Name = name };
    }

    internal static WBookmark Parse(OpenXmlUnknownElement start) {
        string name;
        try {
            name = start.GetAttribute("name", "http://schemas.openxmlformats.org/wordprocessingml/2006/main").Value;
        } catch (KeyNotFoundException) {
            return null;
        }
        if (name is null)
            return null;
        return new() { Name = name };
    }

    internal static List<WBookmark> Parse(IEnumerable<OpenXmlElement> skpdBkmrks) {
        List<WBookmark> parsed = new(skpdBkmrks.Count());
        foreach (var e in skpdBkmrks) {
            WBookmark made = Parse(e);
            if (made is null)
                continue;
            parsed.Add(made);
            continue;
        }
        return parsed;
    }

}

}
