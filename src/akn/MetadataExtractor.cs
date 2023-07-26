
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Meta {

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
        string uri = judgment.SelectSingleNode("/akn:akomaNtoso/akn:*/akn:meta/akn:identification/akn:FRBRWork/akn:FRBRthis/@value", nsmgr)?.Value;
        if (string.IsNullOrEmpty(uri))
            uri = null;
        string date = judgment.SelectSingleNode("/akn:akomaNtoso/akn:*/akn:meta/akn:identification/akn:FRBRWork/akn:FRBRdate/@date", nsmgr)?.Value;
        if (date == Metadata.DummyDate)
            date = null;
        return new Meta() {
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

}

public class ExternalRef {

    public string Href { get; init; }

    public string ShowAs { get; init; }

}

}
