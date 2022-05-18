
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

    private XslCompiledTransform Transform = new XslCompiledTransform();

    public Tests() {
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        Transform.Load(xsltReader);
    }

    public static IEnumerable<object[]> indices = Enumerable.Concat(
        Enumerable.Range(1, 10), Enumerable.Range(12, 11)
    ).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(indices))]
    public void Test(int i) {
        var docx = ReadDocx(i);
        var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
        var expected = ReadXml(i);
        // Assert.NotEqual(expected, actual);
        actual = RemoveSomeMetadata(actual);
        expected = RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(11,"order")]
    public void TestWithAttachment(int i, string name) {
        var main = ReadDocx(i, "main");
        var attach = ReadDocx(i, name);
        Api.AttachmentType type;
        switch (name) {
            case "order":
                type = Api.AttachmentType.Order;
                break;
            default:
                throw new System.Exception();
        }
        List<Api.Attachment> attachments = new List<Api.Attachment>(1) { new Api.Attachment() { Content = attach, Type = type } };
        var actual = Api.Parser.Parse(new Api.Request(){ Content = main, Attachments = attachments }).Xml;
        var expected = ReadXml(i);
        // Assert.NotEqual(expected, actual);
        actual = RemoveSomeMetadata(actual);
        expected = RemoveSomeMetadata(expected);
        Assert.Equal(expected, actual);
    }

    private byte[] ReadDocx(int i) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream($"test{i}.docx");
        MemoryStream ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
    private byte[] ReadDocx(int i, string name) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream($"test{i}-{name}.docx");
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
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://caselaw.nationalarchives.gov.uk/akn'>
  <xsl:template match='akn:FRBRManifestation/akn:FRBRdate/@date'/>
  <xsl:template match='uk:parser/text()'/>
  <xsl:template match='@*|node()'>
    <xsl:copy>
      <xsl:apply-templates select='@*|node()'/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>";

    private string RemoveSomeMetadata(string akn) {
        using XmlReader reader = XmlReader.Create(new StringReader(akn));
        using StringWriter sWriter = new StringWriter();
        using XmlWriter xWriter = XmlWriter.Create(sWriter);
        Transform.Transform(reader, xWriter);
        return sWriter.ToString();
    }

    private string Serialize(XmlDocument judgment) {
        using MemoryStream memStrm = new MemoryStream();
        AkN.Serializer.Serialize(judgment, memStrm);
        return System.Text.Encoding.UTF8.GetString(memStrm.ToArray());
    }

}

}
