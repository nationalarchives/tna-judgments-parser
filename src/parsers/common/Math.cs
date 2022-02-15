
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;


using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OMML = DocumentFormat.OpenXml.Math;

namespace UK.Gov.Legislation.Judgments.Parse {

class Math2 {

    private static XslCompiledTransform xslt = new XslCompiledTransform();

    static Math2() {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream("math.omml2mml.xsl");
        using XmlReader reader = XmlReader.Create(stream);
        xslt.Load(reader);
    }

    internal static IMath Parse(MainDocumentPart main, OMML.OfficeMath e) {
        XmlDocument source = new XmlDocument();
        source.LoadXml(e.OuterXml);
        using XmlNodeReader reader = new XmlNodeReader(source);
        XmlDocument destination = new XmlDocument();
        XmlWriter writer = destination.CreateNavigator().AppendChild();
        xslt.Transform(reader, writer);
        writer.Close(); // necessary
        return new WMath(destination.DocumentElement);
    }

}

}
