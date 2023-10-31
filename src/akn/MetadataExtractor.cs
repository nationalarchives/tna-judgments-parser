
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Meta {

    /// <summary>
    /// the name of the only child of the root <akomaNtoso> element
    /// but if it's "doc", then the value of the @name attribute
    /// e.g., 'judgment' or 'pressSummary'
    /// </summary>
    public string DocElementName { get; init; }

    public string WorkUri { get; init; }

    public string WorkDate { get; init; }

    public string WorkName { get; init; }

    public string UKCourt { get; init; }

    public string UKCite { get; init; }

    public IEnumerable<ExternalRef> ExternalAttachments { get; init; }

}

public class MetadataExtractor {

    public static Meta Extract(XmlDocument judgment) {
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(judgment.NameTable);
        nsmgr.AddNamespace("akn", Builder.ns);
        nsmgr.AddNamespace("uk", Metadata.ukns);
        string docName = (judgment.SelectSingleNode("/akn:akomaNtoso/akn:*", nsmgr) as XmlElement).LocalName;
        if (docName == "doc")
            docName = judgment.SelectSingleNode("/akn:akomaNtoso/akn:*/@name", nsmgr).Value;
        string uri = judgment.SelectSingleNode("/akn:akomaNtoso/akn:*/akn:meta/akn:identification/akn:FRBRWork/akn:FRBRthis/@value", nsmgr)?.Value;
        if (string.IsNullOrEmpty(uri))
            uri = null;
        string date = judgment.SelectSingleNode("/akn:akomaNtoso/akn:*/akn:meta/akn:identification/akn:FRBRWork/akn:FRBRdate/@date", nsmgr)?.Value;
        if (date == Metadata.DummyDate)
            date = null;
        return new Meta() {
            DocElementName = docName,
            WorkUri = uri,
            WorkDate = date,
            WorkName = judgment.SelectSingleNode("/akn:akomaNtoso/akn:*/akn:meta/akn:identification/akn:FRBRWork/akn:FRBRname/@value", nsmgr)?.Value,
            UKCourt = judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:proprietary/uk:court", nsmgr)?.InnerText,
            UKCite = judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:proprietary/uk:cite", nsmgr)?.InnerText,
            ExternalAttachments = judgment.SelectNodes("/akn:akomaNtoso/akn:judgment/akn:meta/akn:references/akn:hasAttachment", nsmgr).Cast<XmlNode>()
                .Select(e => new ExternalRef() { Href = e.Attributes["href"].Value, ShowAs = e.Attributes["showAs"]?.Value })
        };
    }

    public static string ExtractContentHash(XmlDocument judgment) {
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(judgment.NameTable);
        nsmgr.AddNamespace("akn", Builder.ns);
        nsmgr.AddNamespace("uk", Metadata.ukns);
        return judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:proprietary/uk:hash", nsmgr)?.InnerText;
    }

    public static string ExtractJurisdiction(XmlDocument judgment) {
        XmlNamespaceManager nsmgr = new (judgment.NameTable);
        nsmgr.AddNamespace("akn", Builder.ns);
        nsmgr.AddNamespace("uk", Metadata.ukns);
        return judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:proprietary/uk:jurisdiction", nsmgr)?.InnerText;
    }

}

public class ExternalRef {

    public string Href { get; init; }

    public string ShowAs { get; init; }

}

}
