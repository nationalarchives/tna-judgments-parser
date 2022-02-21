
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.NationalArchives.CaseLaw {

public class Tests {

    private static int N = 10;

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public Tests() {
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        Transform.Load(xsltReader);
    }

    public static IEnumerable<object[]> indices = Enumerable.Range(1, N).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i) {
        var docx = ReadDocx(i);
        var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
        var expected = ReadXml(i);
        Assert.NotEqual(expected, actual);
        actual = RemoveManifestationDate(actual);
        expected = RemoveManifestationDate(expected);
        Assert.Equal(expected, actual);
    }

    private byte[] ReadDocx(int i) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream($"test{i}.docx");
        MemoryStream ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
    private string ReadXml(int i) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream($"test{i}.xml");
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string xslt = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0'>
  <xsl:template match='akn:FRBRManifestation/akn:FRBRdate/@date'/>
  <xsl:template match='@*|node()'>
    <xsl:copy>
      <xsl:apply-templates select='@*|node()'/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>";

    private string RemoveManifestationDate(string akn) {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(akn);
        XmlDocument removed = RemoveManifestationDate(doc);
        return Serialize(removed);
    }

    private XmlDocument RemoveManifestationDate(XmlDocument akn) {
        using XmlReader aknReader = new XmlNodeReader(akn);
        XmlDocument destination = new XmlDocument();
        using XmlWriter writer = destination.CreateNavigator().AppendChild();
        Transform.Transform(aknReader, writer);
        return destination;
    }

    private string Serialize(XmlDocument judgment) {
        using MemoryStream memStrm = new MemoryStream();
        AkN.Serializer.Serialize(judgment, memStrm);
        return System.Text.Encoding.UTF8.GetString(memStrm.ToArray());
    }

}

}
