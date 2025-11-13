using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;

namespace UK.Gov.NationalArchives.CaseLaw;

public static class DocumentHelpers {

    private const string xslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://caselaw.nationalarchives.gov.uk/akn'>
  <xsl:template match='akn:FRBRManifestation/akn:FRBRdate/@date'/>
  <xsl:template match='uk:parser/text()'/>
  <xsl:template match='@*|node()'>
    <xsl:copy>
      <xsl:apply-templates select='@*|node()'/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>";

    public static byte[] ReadDocx(int i) {
        var resource = $"test.judgments.test{i}.docx";
        return ReadDocx(resource);
    }
    
    public static byte[] ReadDocx(int i, string name) {
        var resource = $"test.judgments.test{i}-{name}.docx";
        return ReadDocx(resource);
    }
    
    internal static byte[] ReadDocx(string resource) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(resource);
        MemoryStream ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
    
    public static string ReadXml(int i) {
        var resource = $"test.judgments.test{i}.xml";
        return ReadXml(resource);
    }
    
    internal static string ReadXml(string resource) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(resource);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string RemoveSomeMetadata(string akn) {
        var transform = GetTransformer();

        using XmlReader reader = XmlReader.Create(new StringReader(akn));
        using StringWriter sWriter = new StringWriter();
        using XmlWriter xWriter = XmlWriter.Create(sWriter);
        
        transform.Transform(reader, xWriter);
        return sWriter.ToString();
    }
    
    private static XslCompiledTransform GetTransformer()
    {
        var transform = new XslCompiledTransform();
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        transform.Load(xsltReader);
        return transform;
    }
}
