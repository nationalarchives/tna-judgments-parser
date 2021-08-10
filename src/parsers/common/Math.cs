
using System.IO;
using System.Xml;
using System.Xml.Xsl;


using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OMML = DocumentFormat.OpenXml.Math;

namespace UK.Gov.Legislation.Judgments.Parse {

class Math2 {

    internal static IMath Parse(MainDocumentPart main, OMML.OfficeMath e) {
        XmlDocument source = new XmlDocument();
        source.LoadXml(e.OuterXml);
        XmlNodeReader reader = new XmlNodeReader(source);
        XmlDocument destination = new XmlDocument();
        XmlWriter writer = destination.CreateNavigator().AppendChild();
        XslCompiledTransform xslt = new XslCompiledTransform();
        xslt.Load("src/parsers/common/omml2mml.xsl");
        xslt.Transform(reader, writer);
        writer.Close(); // necessary
        return new WMath(destination.DocumentElement);
    }

}

}
