
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation {

interface IXmlDocument {

    string Id { get; }

    System.Xml.XmlDocument Document { get; }

    string Serialize();

    IEnumerable<IImage> Images { get; }

}

class XmlDocument_ : IXmlDocument {

    public string Id { get; }

    public System.Xml.XmlDocument Document { get; internal init; }

    public string Serialize() => UK.Gov.NationalArchives.Judgments.Api.Parser.SerializeXml(Document);

    public IEnumerable<IImage> Images { get; }

}

}