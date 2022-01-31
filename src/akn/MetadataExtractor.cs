
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class Meta {

    public string WorkUri { get; init; }

    public string WorkDate { get; init; }

    public string WorkName { get; init; }

    public string UKCourt { get; init; }

    public string UKCite { get; init; }

}

public class MetadataExtractor {

    public static Meta Extract(XmlDocument judgment) {
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(judgment.NameTable);
        nsmgr.AddNamespace("akn", Builder.ns);
        nsmgr.AddNamespace("uk", Metadata.ukns);
        return new Meta() {
            WorkUri = judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:identification/akn:FRBRWork/akn:FRBRthis/@value", nsmgr)?.Value,
            WorkDate = judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:identification/akn:FRBRWork/akn:FRBRdate/@date", nsmgr)?.Value,
            WorkName = judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:identification/akn:FRBRWork/akn:FRBRname/@value", nsmgr)?.Value,
            UKCourt = judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:proprietary/uk:court", nsmgr)?.InnerText,
            UKCite = judgment.SelectSingleNode("/akn:akomaNtoso/akn:judgment/akn:meta/akn:proprietary/uk:cite", nsmgr)?.InnerText
        };
    }

}

}
